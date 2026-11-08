using System.Globalization;
using System.Text.Json;
using Subgate.Api;
using Subgate.Config;
using Subgate.Providers.Anthropic;

namespace Subgate.Cli;

/// <summary>
/// <c>subgate statusline [--consumer name]</c> (ADR 0006, 0008, 0012): the owner's status-line command. Reads Claude Code's status-line JSON
/// from stdin, posts the meter and the session's cost, and prints one line. It never fails the status line: whatever goes wrong, it
/// prints the plain line within the deadline and exits 0.
/// </summary>
internal static class StatuslineCommand
{
    private const int ExitUsage = 64;

    /// <summary>ADR 0008: a status line waits 300 ms for subgate, no longer.</summary>
    internal static readonly TimeSpan Deadline = TimeSpan.FromMilliseconds(300);

    public static async Task<int> Run(string[] args, TextWriter output, TextWriter error, CliEnvironment env)
    {
        string? consumerOption = null;
        switch (args)
        {
            case []:
                break;
            case ["--consumer", { Length: > 0 } named]:
                consumerOption = named;
                break;
            case [{ } single] when single.StartsWith("--consumer=", StringComparison.Ordinal) && single.Length > "--consumer=".Length:
                consumerOption = single["--consumer=".Length..];
                break;
            default:
                output.WriteLine(Program.Usage);
                return ExitUsage;
        }

        var input = await ReadInput(env.Input);
        var observed = Observe(input);
        var model = Model(input);
        var line = Line(model, observed?.Windows.FirstOrDefault(w => w.Window == Window.SevenDay)?.Utilization, null, null);
        try
        {
            line = await Compose(model, observed, line, consumerOption, error, env);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // The status line must not fail, whatever the cause.
            error.WriteLine($"subgate: {e.GetType().Name}; printing the plain line");
        }

        output.WriteLine(line);
        return 0;
    }

    private static async Task<string> ReadInput(TextReader reader)
    {
        try
        {
            return await reader.ReadToEndAsync();
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    private static async Task<Call<StatusResponse>> Exchange(SubgateClient client, SignalBody? signal, UsageBody? usage)
    {
        await Task.WhenAll(
            signal is null ? Task.CompletedTask : client.Signal(signal),
            usage is null ? Task.CompletedTask : client.Usage(usage));
        return await client.Status();
    }

    private static StatusLineObservations? Observe(string input)
    {
        try
        {
            return StatusLineParser.Parse(input);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The line without subgate's part is what is printed when subgate is away, so the owner's own numbers still show.</summary>
    private static async Task<string> Compose(
        string model, StatusLineObservations? observed, string plain, string? consumerOption, TextWriter error, CliEnvironment env)
    {
        var consumer = env.Consumer(consumerOption);
        if (consumer is null || !ConfigLoader.IsName(consumer) || env.Url() is not { } url || env.Token() is not { } token)
        {
            error.WriteLine($"subgate: set {CliEnvironment.ConsumerVariable} and {CliEnvironment.TokenVariable} to the owner's; printing the plain line");
            return plain;
        }

        if (observed is null)
        {
            return plain;
        }

        using var client = new SubgateClient(url, token, Deadline, env.Handler);
        var signal = Reports.Signal(Reports.StatuslineSource, env.Clock.GetUtcNow(), observed.Windows.Select(w => Reports.Meter(w)));
        var usage = observed.OwnerUsage.SessionId is { Length: > 0 } session
            ? new UsageBody(null, session, consumer, null, null, observed.OwnerUsage.TotalCostUsd, true)
            : null;

        // The status is read after the posts, so the line shows this input too.
        var exchange = Exchange(client, signal, usage);
        using var timer = new CancellationTokenSource();
        var deadline = Task.Delay(Deadline, env.Clock, timer.Token);
        if (await Task.WhenAny(exchange, deadline) != exchange)
        {
            return plain;
        }

        await timer.CancelAsync();
        var current = exchange.Result;
        var week = observed.Windows.FirstOrDefault(w => w.Window == Window.SevenDay)?.Utilization
            ?? (current.IsOk ? current.Value?.Account.Utilization : null);
        var left = current.Value is { } snapshot && snapshot.Windows.ContainsKey("7d") && snapshot.Consumers.TryGetValue(consumer, out var mine)
            ? mine.Remaining
            : (double?)null;
        return Line(model, week, consumer, left);
    }

    private static string Line(string model, double? week, string? consumer, double? left)
    {
        var parts = new List<string> { model };
        if (week is { } used)
        {
            parts.Add($"week {Percent(used)}% used");
        }

        if (left is { } remaining)
        {
            parts.Add($"{consumer} {Percent(Math.Max(0, remaining))}% left");
        }

        return string.Join(" | ", parts);
    }

    private static string Percent(double fraction) =>
        Math.Round(Math.Clamp(fraction, 0, 1) * 100, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);

    private static string Model(string input)
    {
        try
        {
            using var document = JsonDocument.Parse(input);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("model", out var model) && model.ValueKind == JsonValueKind.Object)
            {
                foreach (var name in (ReadOnlySpan<string>)["display_name", "id"])
                {
                    if (model.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text)
                    {
                        return text;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON: nothing to name the model by.
        }

        return "claude";
    }
}
