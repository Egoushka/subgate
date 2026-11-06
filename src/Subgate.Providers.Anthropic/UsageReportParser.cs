using System.Text.Json;

namespace Subgate.Providers.Anthropic;

/// <summary>Parses the `usage_report` carried by a `claude -p "/usage"` stream-json run (ADR 0012).</summary>
public static class UsageReportParser
{
    public static UsageReportObservations Parse(string json)
    {
        var line = JsonSerializer.Deserialize(json, AnthropicJsonContext.Default.UsageReportLineDto);
        var limits = line?.UsageReport?.RateLimits?.Limits;
        if (limits is null)
        {
            return new UsageReportObservations([], []);
        }

        var windows = new List<MeterObservation>();
        var scoped = new List<ScopedMeterObservation>();

        foreach (var limit in limits)
        {
            if (limit.Percent is not { } percent || limit.ResetsAt is not { } resetsAt)
            {
                continue;
            }

            var utilization = percent / 100.0;

            switch (limit.Kind)
            {
                case "session":
                    windows.Add(new MeterObservation(Window.FiveHour, utilization, resetsAt));
                    break;
                case "weekly_all":
                    windows.Add(new MeterObservation(Window.SevenDay, utilization, resetsAt));
                    break;
                case "weekly_scoped":
                    scoped.Add(new ScopedMeterObservation(limit.Scope ?? "", utilization, resetsAt));
                    break;
            }
        }

        return new UsageReportObservations(windows, scoped);
    }
}
