namespace Subgate.Providers.Anthropic;

// Stand-ins for Switchknife.Pace's observation types (ADR 0004). These map one-to-one
// onto the engine's own types and are meant to be replaced once pace/v0.1.0 ships.

/// <summary>A limit period the plan reports.</summary>
public enum Window
{
    FiveHour,
    SevenDay,
}

/// <summary>A meter reading for one window: utilization (0-1) and when it resets.</summary>
public sealed record MeterObservation(Window Window, double Utilization, DateTimeOffset ResetsAt);

/// <summary>A meter reading scoped to something narrower than a window (e.g. a model family).</summary>
public sealed record ScopedMeterObservation(string Scope, double Utilization, DateTimeOffset ResetsAt);

/// <summary>One model's share of a usage report's cost.</summary>
public sealed record ModelUsage(string Model, double CostUsd);

/// <summary>A consumer's or the owner's usage, with cost broken out per model when known.</summary>
public sealed record UsageObservation(string? SessionId, double TotalCostUsd, IReadOnlyList<ModelUsage> ModelUsages);

/// <summary>What a `/usage` report carries: the account-wide windows and any scoped readings.</summary>
public sealed record UsageReportObservations(
    IReadOnlyList<MeterObservation> Windows,
    IReadOnlyList<ScopedMeterObservation> Scoped);

/// <summary>What a status line carries: the owner's windows (if sent) and their session's usage.</summary>
public sealed record StatusLineObservations(IReadOnlyList<MeterObservation> Windows, UsageObservation OwnerUsage);
