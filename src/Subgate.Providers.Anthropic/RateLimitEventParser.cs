using System.Text.Json;

namespace Subgate.Providers.Anthropic;

/// <summary>Parses a `rate_limit_event` line from Claude Code's stream-json output (ADR 0012).</summary>
public static class RateLimitEventParser
{
    public static IReadOnlyList<MeterObservation> Parse(string json, TimeProvider timeProvider)
    {
        var line = JsonSerializer.Deserialize(json, AnthropicJsonContext.Default.RateLimitEventLineDto);
        var info = line?.RateLimitInfo;
        if (info is null)
        {
            return [];
        }

        if (info.UnifiedWindows is { } windows)
        {
            var observations = new List<MeterObservation>();
            if (windows.FiveHour is { } fiveHour)
            {
                observations.Add(new MeterObservation(
                    Window.FiveHour, fiveHour.Utilization, DateTimeOffset.FromUnixTimeSeconds(fiveHour.ResetsAt)));
            }

            if (windows.SevenDay is { } sevenDay)
            {
                observations.Add(new MeterObservation(
                    Window.SevenDay, sevenDay.Utilization, DateTimeOffset.FromUnixTimeSeconds(sevenDay.ResetsAt)));
            }

            return observations;
        }

        if (info.ResetsAt is not { } resetsAtUnix)
        {
            return [];
        }

        var resetsAt = DateTimeOffset.FromUnixTimeSeconds(resetsAtUnix);
        var window = ResolveWindow(info.RateLimitType, resetsAt, timeProvider);
        return [new MeterObservation(window, info.Utilization ?? 0, resetsAt)];
    }

    private static Window ResolveWindow(string? rateLimitType, DateTimeOffset resetsAt, TimeProvider timeProvider) =>
        rateLimitType switch
        {
            "five_hour" => Window.FiveHour,
            "seven_day" => Window.SevenDay,
            _ => resetsAt - timeProvider.GetUtcNow() <= TimeSpan.FromHours(5) ? Window.FiveHour : Window.SevenDay,
        };
}
