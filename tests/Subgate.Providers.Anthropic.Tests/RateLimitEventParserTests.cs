namespace Subgate.Providers.Anthropic.Tests;

public class RateLimitEventParserTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1700000000);
    private static readonly TimeProvider Clock = new FixedTimeProvider(Now);

    [Fact]
    public void UnifiedWindows_yields_a_five_hour_and_a_seven_day_observation()
    {
        var json = Fixtures.Load("rate-limit-event-unified-windows.json");

        var observations = RateLimitEventParser.Parse(json, Clock);

        Assert.Equal(2, observations.Count);
        var fiveHour = Assert.Single(observations, o => o.Window == Window.FiveHour);
        Assert.Equal(0.3, fiveHour.Utilization);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700003600), fiveHour.ResetsAt);
        var sevenDay = Assert.Single(observations, o => o.Window == Window.SevenDay);
        Assert.Equal(0.12, sevenDay.Utilization);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700500000), sevenDay.ResetsAt);
    }

    [Fact]
    public void RateLimitType_alone_yields_that_windows_observation()
    {
        var json = Fixtures.Load("rate-limit-event-type-only.json");

        var observations = RateLimitEventParser.Parse(json, Clock);

        var observation = Assert.Single(observations);
        Assert.Equal(Window.SevenDay, observation.Window);
        Assert.Equal(0.81, observation.Utilization);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700500000), observation.ResetsAt);
    }

    [Fact]
    public void Neither_field_and_a_near_reset_infers_five_hour()
    {
        var json = Fixtures.Load("rate-limit-event-untyped-near-reset.json");

        var observations = RateLimitEventParser.Parse(json, Clock);

        var observation = Assert.Single(observations);
        Assert.Equal(Window.FiveHour, observation.Window);
        Assert.Equal(0.55, observation.Utilization);
    }

    [Fact]
    public void Neither_field_and_a_far_reset_infers_seven_day()
    {
        var json = Fixtures.Load("rate-limit-event-untyped-far-reset.json");

        var observations = RateLimitEventParser.Parse(json, Clock);

        var observation = Assert.Single(observations);
        Assert.Equal(Window.SevenDay, observation.Window);
        Assert.Equal(0.2, observation.Utilization);
    }

    [Fact]
    public void Unknown_fields_are_ignored()
    {
        var json = Fixtures.Load("rate-limit-event-untyped-near-reset.json");

        Assert.Contains("unexpected_future_field", json);
        var observations = RateLimitEventParser.Parse(json, Clock);

        Assert.Single(observations);
    }
}
