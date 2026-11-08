using Subgate.Api;
using Subgate.Providers.Anthropic;

namespace Subgate.Cli;

/// <summary>What the reporters read, as the bodies ADR 0007's <c>/v1/signal</c> and <c>/v1/usage</c> take.</summary>
internal static class Reports
{
    public const string RunSource = "rate_limit_event";
    public const string SampleSource = "usage_report";
    public const string StatuslineSource = "statusline";

    public static SignalBody? Signal(string source, DateTimeOffset now, IEnumerable<SignalWindowBody> windows)
    {
        var list = windows.ToList();
        return list.Count == 0 ? null : new SignalBody(source, now.ToUnixTimeSeconds(), list);
    }

    public static SignalWindowBody Meter(MeterObservation observation, bool rejected = false) =>
        new(
            observation.Window == Window.FiveHour ? "5h" : "7d", null, Fraction(observation.Utilization),
            observation.ResetsAt.ToUnixTimeSeconds(), rejected ? "rejected" : null);

    /// <summary>A scoped reading, or null when it has no scope to name it by.</summary>
    public static SignalWindowBody? Scoped(ScopedMeterObservation observation) =>
        string.IsNullOrEmpty(observation.Scope)
            ? null
            : new SignalWindowBody("7d_scoped", observation.Scope, Fraction(observation.Utilization), observation.ResetsAt.ToUnixTimeSeconds(), null);

    /// <summary>The API takes 0 to 1; a meter that reads a hair over (or not a number) must not void the whole signal.</summary>
    private static double Fraction(double utilization) => double.IsNaN(utilization) ? 0 : Math.Clamp(utilization, 0, 1);
}
