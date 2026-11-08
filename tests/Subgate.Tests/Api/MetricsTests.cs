using System.Text.RegularExpressions;

namespace Subgate.Tests.Api;

/// <summary>C4 and C6: the gauges the dashboards and alerts read exist from the first scrape.</summary>
public sealed partial class MetricsTests
{
    private static readonly string[] Names =
    [
        "subgate_utilization", "subgate_reset_seconds", "subgate_share_used", "subgate_share_allowance", "subgate_leases_open",
        "subgate_signal_age_seconds", "subgate_k", "subgate_admit_total",
    ];

    [Fact]
    public async Task Every_gauge_the_spec_names_exists_before_any_signal()
    {
        await using var host = await ApiHost.Start();

        var text = await host.Client.GetStringAsync("/metrics");

        var found = SeriesName().Matches(text).Select(m => m.Value).ToHashSet();
        foreach (var name in Names)
        {
            Assert.Contains(name, found);
        }

        Assert.Contains("subgate_signal_age_seconds NaN", text, StringComparison.Ordinal);
        Assert.Contains("subgate_k NaN", text, StringComparison.Ordinal);
        Assert.Contains("subgate_reset_seconds NaN", text, StringComparison.Ordinal);
        Assert.Contains("subgate_utilization{window=\"7d\"} NaN", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_seven_day_utilization_is_one_line_with_the_meter_s_value()
    {
        await using var host = await ApiHost.Start();
        using var signal = await host.Post(
            "/v1/signal",
            $$$"""{"source":"example-sampler","sent_at":1,"windows":[{"kind":"7d","utilization":0.42,"resets_at":{{{ApiHost.Epoch.AddDays(5).ToUnixTimeSeconds()}}}}]}""",
            ApiHost.SignalToken);
        host.Clock.Advance(TimeSpan.FromMinutes(10));

        var text = await host.Client.GetStringAsync("/metrics");

        Assert.Single(Regex.Matches(text, "(?m)^subgate_utilization\\{window=\"7d\"\\}"));
        Assert.Contains("subgate_utilization{window=\"7d\"} 0.42", text, StringComparison.Ordinal);
        Assert.Contains("subgate_signal_age_seconds 600", text, StringComparison.Ordinal);
        Assert.Contains("subgate_signal_source_total{source=\"example-sampler\"} 1", text, StringComparison.Ordinal);
        Assert.Matches("subgate_reset_seconds [0-9]", text);
    }

    [Fact]
    public async Task Admits_are_counted_by_consumer_decision_and_would()
    {
        await using var host = await ApiHost.Start();
        using var first = await host.Post("/v1/admit", """{"consumer":"job"}""", ApiHost.JobToken);
        using var second = await host.Post("/v1/admit", """{"consumer":"job"}""", ApiHost.JobToken);

        var text = await host.Client.GetStringAsync("/metrics");

        Assert.Contains("subgate_admit_total{consumer=\"job\",decision=\"allow\",would=\"allow\"} 2", text, StringComparison.Ordinal);
        Assert.Contains("subgate_admit_total{consumer=\"automation\",decision=\"allow\",would=\"allow\"} 0", text, StringComparison.Ordinal);
        Assert.Contains("subgate_leases_open{consumer=\"job\"} 2", text, StringComparison.Ordinal);
    }

    [GeneratedRegex("(?m)^subgate_[a-z_]+")]
    private static partial Regex SeriesName();
}
