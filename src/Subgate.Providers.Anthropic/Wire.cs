using System.Text.Json.Serialization;

namespace Subgate.Providers.Anthropic;

// Wire shapes, kept separate from the Observation types above so a change in Anthropic's
// JSON never leaks past the parser. Field names and casing follow ADR 0012: the stream-json
// envelope and `/usage` report are snake_case; `rate_limit_info` (the Agent SDK's
// SDKRateLimitEvent, including the undocumented `unifiedWindows` spike S1 found) is camelCase.

internal sealed class RateLimitEventLineDto
{
    [JsonPropertyName("rate_limit_info")]
    public RateLimitInfoDto? RateLimitInfo { get; set; }
}

internal sealed class RateLimitInfoDto
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("rateLimitType")]
    public string? RateLimitType { get; set; }

    [JsonPropertyName("resetsAt")]
    public long? ResetsAt { get; set; }

    [JsonPropertyName("utilization")]
    public double? Utilization { get; set; }

    [JsonPropertyName("unifiedWindows")]
    public UnifiedWindowsDto? UnifiedWindows { get; set; }
}

internal sealed class UnifiedWindowsDto
{
    [JsonPropertyName("five_hour")]
    public WindowReadingDto? FiveHour { get; set; }

    [JsonPropertyName("seven_day")]
    public WindowReadingDto? SevenDay { get; set; }
}

internal sealed class WindowReadingDto
{
    [JsonPropertyName("utilization")]
    public double Utilization { get; set; }

    [JsonPropertyName("resetsAt")]
    public long ResetsAt { get; set; }
}

internal sealed class UsageReportLineDto
{
    [JsonPropertyName("usage_report")]
    public UsageReportDto? UsageReport { get; set; }
}

internal sealed class UsageReportDto
{
    [JsonPropertyName("rate_limits")]
    public RateLimitsDto? RateLimits { get; set; }
}

internal sealed class RateLimitsDto
{
    [JsonPropertyName("limits")]
    public List<UsageLimitDto>? Limits { get; set; }
}

internal sealed class UsageLimitDto
{
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("percent")]
    public double? Percent { get; set; }

    [JsonPropertyName("resets_at")]
    public DateTimeOffset? ResetsAt { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}

internal sealed class StatusLineDto
{
    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("cost")]
    public StatusLineCostDto? Cost { get; set; }

    [JsonPropertyName("rate_limits")]
    public StatusLineRateLimitsDto? RateLimits { get; set; }
}

internal sealed class StatusLineCostDto
{
    [JsonPropertyName("total_cost_usd")]
    public double? TotalCostUsd { get; set; }
}

internal sealed class StatusLineRateLimitsDto
{
    [JsonPropertyName("five_hour")]
    public StatusLineWindowDto? FiveHour { get; set; }

    [JsonPropertyName("seven_day")]
    public StatusLineWindowDto? SevenDay { get; set; }
}

internal sealed class StatusLineWindowDto
{
    [JsonPropertyName("used_percentage")]
    public double? UsedPercentage { get; set; }

    [JsonPropertyName("resets_at")]
    public DateTimeOffset? ResetsAt { get; set; }
}

internal sealed class RunResultDto
{
    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("total_cost_usd")]
    public double? TotalCostUsd { get; set; }

    [JsonPropertyName("modelUsage")]
    public Dictionary<string, ModelUsageDto>? ModelUsage { get; set; }
}

internal sealed class ModelUsageDto
{
    [JsonPropertyName("costUSD")]
    public double CostUsd { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = false)]
[JsonSerializable(typeof(RateLimitEventLineDto))]
[JsonSerializable(typeof(UsageReportLineDto))]
[JsonSerializable(typeof(StatusLineDto))]
[JsonSerializable(typeof(RunResultDto))]
internal partial class AnthropicJsonContext : JsonSerializerContext;
