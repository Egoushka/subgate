using System.Text.Json;

namespace Subgate.Providers.Anthropic;

/// <summary>Parses the owner's status-line input JSON (ADR 0012): the meter, if sent, and session cost.</summary>
public static class StatusLineParser
{
    public static StatusLineObservations Parse(string json)
    {
        var dto = JsonSerializer.Deserialize(json, AnthropicJsonContext.Default.StatusLineDto)
            ?? throw new JsonException("empty status line input");

        var windows = new List<MeterObservation>();
        if (dto.RateLimits is { } rateLimits)
        {
            if (rateLimits.FiveHour is { UsedPercentage: { } usedFiveHour, ResetsAt: { } resetsFiveHour })
            {
                windows.Add(new MeterObservation(Window.FiveHour, usedFiveHour / 100.0, resetsFiveHour));
            }

            if (rateLimits.SevenDay is { UsedPercentage: { } usedSevenDay, ResetsAt: { } resetsSevenDay })
            {
                windows.Add(new MeterObservation(Window.SevenDay, usedSevenDay / 100.0, resetsSevenDay));
            }
        }

        var ownerUsage = new UsageObservation(dto.SessionId, dto.Cost?.TotalCostUsd ?? 0, []);
        return new StatusLineObservations(windows, ownerUsage);
    }
}
