using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Subgate.Api;
using Subgate.Config;
using Subgate.Providers.Anthropic;

namespace Subgate.Cli;

/// <summary>
/// <c>subgate run [--consumer name] [--on-unavailable run|hold] -- command…</c> (ADR 0012): asks subgate, runs the job, reads the
/// stream-json it prints and reports the meter and the usage. The job's exit code is the command's.
/// </summary>
internal static class RunCommand
{
    private const int ExitUsage = 64;
    private const int ExitTempFail = 75;
    private const int ExitConfig = 78;
    private const int ExitNotFound = 127;

    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(5);

    public static async Task<int> Run(string[] args, TextWriter output, TextWriter error, CliEnvironment env)
    {
        if (Parse(args) is not { } options)
        {
            output.WriteLine(Program.Usage);
            return ExitUsage;
        }

        var consumer = env.Consumer(options.Consumer);
        if (consumer is null || !ConfigLoader.IsName(consumer))
        {
            error.WriteLine($"subgate: name the consumer with --consumer or {CliEnvironment.ConsumerVariable}");
            return ExitConfig;
        }

        if (env.Url() is not { } url || env.Token() is not { } token)
        {
            error.WriteLine($"subgate: set {CliEnvironment.TokenVariable} to the consumer's token (and {CliEnvironment.UrlVariable} to an http address if it is not the default)");
            return ExitConfig;
        }

        using var client = new SubgateClient(url, token, CallTimeout, env.Handler);

        var admit = await client.Admit(new AdmitBody(consumer, null, null));
        string? leaseId = null;
        if (admit is { Kind: CallKind.Ok, Value: { } admitted })
        {
            if (admitted.Decision == "hold")
            {
                error.WriteLine($"subgate: not admitted now{(admitted.RetryAfterS is { } seconds ? $"; try again in {seconds} s" : string.Empty)}");
                return ExitTempFail;
            }

            leaseId = admitted.LeaseId;
        }
        else
        {
            error.WriteLine($"subgate: {admit.Message}");
            if (options.Hold)
            {
                return admit.Kind == CallKind.Rejected ? ExitConfig : ExitTempFail;
            }

            error.WriteLine("subgate: running the job anyway");
        }

        var capture = new RunCapture(env.Clock);
        int exitCode;
        try
        {
            exitCode = await Execute(options.Command, output, capture);
        }
        catch (Win32Exception e)
        {
            error.WriteLine($"subgate: cannot start {options.Command[0]}: {e.Message}");
            return ExitNotFound;
        }

        await Deliver(client, new ReportQueue(env.QueueDirectory(), consumer), capture.ToReport(consumer, leaseId, env.Clock.GetUtcNow()), error);
        return exitCode;
    }

    private static async Task<int> Execute(string[] command, TextWriter output, RunCapture capture)
    {
        var start = new ProcessStartInfo(command[0]) { UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in command.Skip(1))
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new Win32Exception("the process did not start");
        while (await process.StandardOutput.ReadLineAsync() is { } line)
        {
            output.WriteLine(line);
            capture.Observe(line);
        }

        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    /// <summary>Older waiting reports first, then this one; whatever subgate cannot take now is queued for the next run.</summary>
    private static async Task Deliver(SubgateClient client, ReportQueue queue, QueuedReport report, TextWriter error)
    {
        var away = !await queue.Drain(async waiting => await Send(client, waiting, error) is null);
        var unsent = away ? report : await Send(client, report, error);
        if (unsent is null)
        {
            return;
        }

        error.WriteLine(queue.TryAppend(unsent)
            ? "subgate: cannot reach subgate; the report is queued and goes out with the next run"
            : "subgate: cannot reach subgate and the report could not be queued; it is lost");
    }

    /// <summary>Sends the meter reading, then the usage. Null when subgate took it (or refused it for good); else what is left to send.</summary>
    private static async Task<QueuedReport?> Send(SubgateClient client, QueuedReport report, TextWriter error)
    {
        if (report.Signal is { } signal)
        {
            var call = await client.Signal(signal);
            if (call.Kind == CallKind.Unavailable)
            {
                return report;
            }

            if (call.Kind == CallKind.Rejected)
            {
                error.WriteLine($"subgate: the meter reading was refused: {call.Message}");
            }
        }

        if (report.Usage is { } usage)
        {
            var call = await client.Usage(usage);
            if (call.Kind == CallKind.Unavailable)
            {
                return report with { Signal = null };
            }

            if (call.Kind == CallKind.Rejected)
            {
                error.WriteLine($"subgate: the usage was refused: {call.Message}");
            }
        }

        return null;
    }

    private sealed record Options(string? Consumer, bool Hold, string[] Command);

    private static Options? Parse(string[] args)
    {
        string? consumer = null;
        var hold = false;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--")
            {
                return i + 1 < args.Length ? new Options(consumer, hold, args[(i + 1)..]) : null;
            }

            string name = args[i];
            string? value = null;
            if (name.IndexOf('=') is var equals and > 0)
            {
                value = name[(equals + 1)..];
                name = name[..equals];
            }
            else if (i + 1 < args.Length)
            {
                value = args[++i];
            }

            switch (name, value)
            {
                case ("--consumer", { Length: > 0 }):
                    consumer = value;
                    break;
                case ("--on-unavailable", "run"):
                    hold = false;
                    break;
                case ("--on-unavailable", "hold"):
                    hold = true;
                    break;
                default:
                    return null;
            }
        }

        return null;
    }
}

/// <summary>What a job's stream-json output says: the plan's meter, and what the job used.</summary>
internal sealed class RunCapture(TimeProvider clock)
{
    private readonly Dictionary<Window, MeterObservation> _meter = [];
    private readonly HashSet<Window> _rejected = [];
    private UsageObservation? _result;

    public void Observe(string line)
    {
        if (line.Length == 0 || line[0] != '{'
            || !(line.Contains("rate_limit_event", StringComparison.Ordinal) || line.Contains("\"result\"", StringComparison.Ordinal)))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            switch (root.ValueKind == JsonValueKind.Object ? Text(root, "type") : null)
            {
                case "rate_limit_event":
                    ObserveEvent(line, root);
                    break;
                case "result":
                    _result = RunResultParser.Parse(line);
                    break;
            }
        }
        catch (JsonException)
        {
            // Not what Claude Code prints: the line was passed through, and there is nothing to read from it.
        }
    }

    public QueuedReport ToReport(string consumer, string? leaseId, DateTimeOffset now)
    {
        var signal = Reports.Signal(Reports.RunSource, now, _meter.Values.Select(o => Reports.Meter(o, _rejected.Contains(o.Window))));

        var models = new Dictionary<string, ModelUsageBody>();
        foreach (var usage in _result?.ModelUsages ?? [])
        {
            var before = models.TryGetValue(usage.Model, out var seen) ? seen.CostUsd ?? 0 : 0;
            models[usage.Model] = new ModelUsageBody(null, null, null, null, before + usage.CostUsd);
        }

        // A job that printed no result still closes its lease, with nothing counted.
        var usageBody = new UsageBody(
            leaseId, _result?.SessionId, consumer, null, models.Count > 0 ? models : null, models.Count > 0 ? null : _result?.TotalCostUsd ?? 0, null);
        return new QueuedReport(signal, usageBody);
    }

    private void ObserveEvent(string line, JsonElement root)
    {
        var info = root.TryGetProperty("rate_limit_info", out var found) && found.ValueKind == JsonValueKind.Object ? found : default;
        var rejected = info.ValueKind == JsonValueKind.Object && Text(info, "status") == "rejected";
        var type = info.ValueKind == JsonValueKind.Object ? Text(info, "rateLimitType") : null;

        foreach (var observation in RateLimitEventParser.Parse(line, clock))
        {
            _meter[observation.Window] = observation;
            var named = type switch
            {
                "five_hour" => Window.FiveHour,
                "seven_day" => Window.SevenDay,
                _ => (Window?)null,
            };
            if (rejected && (named is null || named == observation.Window))
            {
                _rejected.Add(observation.Window);
            }
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
