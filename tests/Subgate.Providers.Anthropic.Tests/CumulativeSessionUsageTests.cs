namespace Subgate.Providers.Anthropic.Tests;

public class CumulativeSessionUsageTests
{
    [Fact]
    public void A_sessions_running_totals_count_once_at_the_high_water_mark()
    {
        var usage = new CumulativeSessionUsage();

        usage.Observe("session-1", 0.10);
        usage.Observe("session-1", 0.25);
        usage.Observe("session-1", 0.25);
        var counted = usage.Observe("session-1", 0.40);

        Assert.Equal(0.40, counted);
    }

    [Fact]
    public void A_lower_total_is_ignored()
    {
        var usage = new CumulativeSessionUsage();
        usage.Observe("session-1", 0.40);

        var counted = usage.Observe("session-1", 0.20);

        Assert.Equal(0.40, counted);
    }
}
