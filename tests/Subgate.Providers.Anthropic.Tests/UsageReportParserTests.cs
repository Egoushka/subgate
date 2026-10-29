namespace Subgate.Providers.Anthropic.Tests;

public class UsageReportParserTests
{
    [Fact]
    public void Session_and_weekly_all_map_to_the_two_windows_and_weekly_scoped_keeps_its_scope()
    {
        var json = Fixtures.Load("usage-report.json");

        var result = UsageReportParser.Parse(json);

        Assert.Equal(2, result.Windows.Count);
        var fiveHour = Assert.Single(result.Windows, w => w.Window == Window.FiveHour);
        Assert.Equal(0.3, fiveHour.Utilization);
        Assert.Equal(DateTimeOffset.Parse("2026-10-29T12:00:00Z"), fiveHour.ResetsAt);
        var sevenDay = Assert.Single(result.Windows, w => w.Window == Window.SevenDay);
        Assert.Equal(0.125, sevenDay.Utilization);

        var scoped = Assert.Single(result.Scoped);
        Assert.Equal("opus", scoped.Scope);
        Assert.Equal(0.08, scoped.Utilization);
    }

    [Fact]
    public void Null_rate_limits_yields_no_observations_and_no_error()
    {
        var json = Fixtures.Load("usage-report-null-rate-limits.json");

        var result = UsageReportParser.Parse(json);

        Assert.Empty(result.Windows);
        Assert.Empty(result.Scoped);
    }
}
