namespace Subgate.Providers.Anthropic.Tests;

public class StatusLineParserTests
{
    [Fact]
    public void With_rate_limits_yields_a_signal_per_window_and_the_owners_usage()
    {
        var json = Fixtures.Load("status-line-with-rate-limits.json");

        var result = StatusLineParser.Parse(json);

        Assert.Equal(2, result.Windows.Count);
        var fiveHour = Assert.Single(result.Windows, w => w.Window == Window.FiveHour);
        Assert.Equal(0.42, fiveHour.Utilization);
        var sevenDay = Assert.Single(result.Windows, w => w.Window == Window.SevenDay);
        Assert.Equal(0.10, sevenDay.Utilization);

        Assert.Equal("session-placeholder", result.OwnerUsage.SessionId);
        Assert.Equal(0.4, result.OwnerUsage.TotalCostUsd);
    }

    [Fact]
    public void Without_rate_limits_yields_no_signal_but_still_the_owners_usage()
    {
        var json = Fixtures.Load("status-line-without-rate-limits.json");

        var result = StatusLineParser.Parse(json);

        Assert.Empty(result.Windows);
        Assert.Equal("session-placeholder", result.OwnerUsage.SessionId);
        Assert.Equal(0.1, result.OwnerUsage.TotalCostUsd);
    }

    [Fact]
    public void Reads_resets_at_as_Unix_epoch_seconds_and_ignores_the_spend_limit()
    {
        var json = Fixtures.Load("status-line-unix-resets-at.json");

        var result = StatusLineParser.Parse(json);

        Assert.Equal(2, result.Windows.Count);
        var fiveHour = Assert.Single(result.Windows, w => w.Window == Window.FiveHour);
        Assert.Equal(0.235, fiveHour.Utilization, 6);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1738425600), fiveHour.ResetsAt);
        var sevenDay = Assert.Single(result.Windows, w => w.Window == Window.SevenDay);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1738857600), sevenDay.ResetsAt);
    }
}
