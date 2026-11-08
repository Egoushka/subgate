using System.ComponentModel;
using System.Diagnostics;
using Subgate.Api;
using Subgate.Providers.Anthropic;

namespace Subgate.Cli;

/// <summary>
/// <c>subgate sample [--once] [--interval 30m]</c> (ADR 0012): reads the plan's meter by running <c>claude -p "/usage"</c> (which costs
/// nothing) and posts it as a signal with the signal token. Runs until stopped; <c>--once</c> samples once and exits 1 when it could not.
/// </summary>
internal static class SampleCommand
{
    private const int ExitUsage = 64;
    private const int ExitConfig = 78;

    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ClaudeTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(5);

    public static async Task<int> Run(string[] args, TextWriter output, TextWriter error, CliEnvironment env, CancellationToken stop)
    {
        if (Parse(args) is not { } options)
        {
            output.WriteLine(Program.Usage);
            return ExitUsage;
        }

        if (env.Url() is not { } url || env.Token() is not { } token)
        {
            error.WriteLine($"subgate: set {CliEnvironment.TokenVariable} to the signal token (and {CliEnvironment.UrlVariable} to an http address if it is not the default)");
            return ExitConfig;
        }

        var claude = env.Variable(CliEnvironment.ClaudeVariable) is { Length: > 0 } set ? set : "claude";
        using var client = new SubgateClient(url, token, CallTimeout, env.Handler);

        while (true)
        {
            var failure = await Sample(client, claude, env.Clock, stop);
            if (failure is null)
            {
                output.WriteLine("subgate: meter sampled");
            }
            else
            {
                error.WriteLine($"subgate: sample failed: {failure}");
            }

            if (options.Once)
            {
                return failure is null ? 0 : 1;
            }

            try
            {
                await Task.Delay(options.Interval, env.Clock, stop);
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
        }
    }

    /// <summary>Null when the meter reached subgate; else why not.</summary>
    private static async Task<string?> Sample(SubgateClient client, string claude, TimeProvider clock, CancellationToken stop)
    {
        string? report;
        try
        {
            report = await RunUsage(claude, stop);
        }
        catch (Win32Exception e)
        {
            return $"cannot start {claude}: {e.Message}";
        }
        catch (OperationCanceledException) when (!stop.IsCancellationRequested)
        {
            return $"{claude} did not answer within {ClaudeTimeout.TotalSeconds:0} s";
        }
        catch (OperationCanceledException)
        {
            return "stopped";
        }

        if (report is null)
        {
            return "no usage_report in what claude printed";
        }

        UsageReportObservations observed;
        try
        {
            observed = UsageReportParser.Parse(report);
        }
        catch (System.Text.Json.JsonException)
        {
            return "the usage_report is not what was expected";
        }

        var windows = observed.Windows.Select(w => Reports.Meter(w))
            .Concat(observed.Scoped.Select(Reports.Scoped).OfType<SignalWindowBody>());
        if (Reports.Signal(Reports.SampleSource, clock.GetUtcNow(), windows) is not { } signal)
        {
            return "the usage_report has no weekly or five-hour reading";
        }

        var call = await client.Signal(signal);
        return call.IsOk ? null : call.Message;
    }

    /// <summary>The line of claude's stream-json that holds the usage report, or null.</summary>
    private static async Task<string?> RunUsage(string claude, CancellationToken stop)
    {
        var start = new ProcessStartInfo(claude) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardInput = true };
        foreach (var argument in (ReadOnlySpan<string>)["-p", "/usage", "--output-format", "stream-json", "--verbose"])
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new Win32Exception("the process did not start");
        process.StandardInput.Close();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(stop);
        limit.CancelAfter(ClaudeTimeout);
        try
        {
            string? found = null;
            while (await process.StandardOutput.ReadLineAsync(limit.Token) is { } line)
            {
                if (line.Contains("usage_report", StringComparison.Ordinal))
                {
                    found = line;
                }
            }

            await process.WaitForExitAsync(limit.Token);
            return found;
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
    }

    private sealed record Options(bool Once, TimeSpan Interval);

    private static Options? Parse(string[] args)
    {
        var once = false;
        var interval = DefaultInterval;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--once":
                    once = true;
                    break;
                case "--interval" when i + 1 < args.Length && Duration(args[i + 1]) is { } next:
                    interval = next;
                    i++;
                    break;
                case { } text when text.StartsWith("--interval=", StringComparison.Ordinal) && Duration(text["--interval=".Length..]) is { } inline:
                    interval = inline;
                    break;
                default:
                    return null;
            }
        }

        return new Options(once, interval);
    }

    /// <summary>A number and a unit: <c>90s</c>, <c>30m</c>, <c>2h</c>.</summary>
    internal static TimeSpan? Duration(string text)
    {
        if (text.Length < 2 || !int.TryParse(text[..^1], out var amount) || amount <= 0)
        {
            return null;
        }

        return text[^1] switch
        {
            's' => TimeSpan.FromSeconds(amount),
            'm' => TimeSpan.FromMinutes(amount),
            'h' => TimeSpan.FromHours(amount),
            _ => null,
        };
    }
}
