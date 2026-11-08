using System.Text.Json.Serialization;

namespace Subgate.Api;

/// <summary>POST /v1/admit (ADR 0007). Fields are nullable so that a missing one is a 400, not a default.</summary>
public sealed record AdmitBody(string? Consumer, string? Model, double? EstimateUsd);

public sealed record ModelUsageBody(long? Input, long? Output, long? CacheRead, long? CacheWrite, double? CostUsd);

/// <summary>POST /v1/usage (ADR 0007).</summary>
public sealed record UsageBody(
    string? LeaseId, string? SessionId, string? Consumer, string? Route,
    Dictionary<string, ModelUsageBody>? Models, double? CostUsd, bool? Cumulative);

public sealed record SignalWindowBody(string? Kind, string? Scope, double? Utilization, long? ResetsAt, string? Status);

/// <summary>POST /v1/signal (ADR 0007).</summary>
public sealed record SignalBody(string? Source, long? SentAt, List<SignalWindowBody>? Windows);

public sealed record AdmitResponse(
    string Decision, string Would, string? LeaseId, long LeaseTtlS, double Share, double Used, double Allowance,
    double Remaining, string State, long? ResetAt, long? RetryAfterS);

public sealed record UsageResponse(double CountedUsd, bool LeaseClosed);

public sealed record ErrorResponse(string Error);

public sealed record WindowStatus(double Utilization, long ResetsAt, long ObservedAt, bool Rejected);

public sealed record AccountStatusBody(
    string State, double? Utilization, long? ResetsAt, double Elapsed, double PaceLine, double Untracked,
    long? MeterAgeS, long? LimitedUntil, double? K);

public sealed record ConsumerStatusBody(
    double Share, double Used, double Reserved, double Allowance, double Remaining, string State, int OpenLeases,
    double WeightedUsd, double ApiUsd, string Estimator, double Apportioned, double? Calibrated);

public sealed record StoreStatus(bool Ok, long Errors, string? Note);

/// <summary>GET /v1/status: open on the listen address (ADR 0007).</summary>
public sealed record StatusResponse(
    string Mode, StoreStatus Store, Dictionary<string, WindowStatus> Windows, AccountStatusBody Account,
    Dictionary<string, ConsumerStatusBody> Consumers);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AdmitBody))]
[JsonSerializable(typeof(UsageBody))]
[JsonSerializable(typeof(SignalBody))]
[JsonSerializable(typeof(AdmitResponse))]
[JsonSerializable(typeof(UsageResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(StatusResponse))]
internal sealed partial class ApiJsonContext : JsonSerializerContext;
