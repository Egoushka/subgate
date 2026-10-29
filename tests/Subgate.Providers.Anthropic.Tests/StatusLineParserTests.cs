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
}
