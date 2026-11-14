using System.Net;
using System.Text.Json;

namespace Subgate.Tests.Api;

/// <summary>GET /v1/status/{consumer} and releasing a lease: what a client reads before it asks to start a job.</summary>
public sealed class ConsumerStatusTests
{
    private static string Signal(double utilization, long resetsAt) =>
        $$"""{"source":"s","windows":[{"kind":"7d","utilization":{{utilization.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"resets_at":{{resetsAt}}}]}""";

    private static long InAWeek => ApiHost.Epoch.AddDays(5).ToUnixTimeSeconds();

    [Fact]
    public async Task A_consumer_under_its_pace_line_is_OK()
    {
        await using var host = await ApiHost.Start();
        using var signal = await host.Post("/v1/signal", Signal(0.1, InAWeek), ApiHost.SignalToken);

        var body = await host.Get("/v1/status/job");

        Assert.Equal("job", body.GetProperty("consumer").GetString());
        Assert.Equal("OK", body.GetProperty("state").GetString());
        Assert.Equal("observe", body.GetProperty("mode").GetString());
        Assert.Equal(0.45, body.GetProperty("share").GetDouble());
        Assert.Equal(InAWeek, body.GetProperty("reset_at").GetInt64());
    }

    [Fact]
    public async Task A_consumer_past_its_allowance_is_OVER_PACE()
    {
        await using var host = await ApiHost.Start();
        using var signal = await host.Post("/v1/signal", Signal(0.5, InAWeek), ApiHost.SignalToken);
        host.Clock.Advance(TimeSpan.FromMinutes(1));
        using var signal2 = await host.Post("/v1/signal", Signal(0.5, InAWeek), ApiHost.SignalToken);
        using var usage = await host.Post("/v1/usage", """{"consumer":"job","cost_usd":50}""", ApiHost.JobToken);
        using var signal3 = await host.Post("/v1/signal", Signal(0.9, InAWeek), ApiHost.SignalToken);

        var body = await host.Get("/v1/status/job");

        Assert.Equal("OVER_PACE", body.GetProperty("state").GetString());
    }

    [Fact]
    public async Task A_rejected_window_makes_every_consumer_LIMITED_with_the_time_it_ends()
    {
        await using var host = await ApiHost.Start();
        var until = ApiHost.Epoch.AddHours(2).ToUnixTimeSeconds();
        using var signal = await host.Post(
            "/v1/signal", $$"""{"source":"s","windows":[{"kind":"5h","utilization":1,"resets_at":{{until}},"status":"rejected"}]}""", ApiHost.SignalToken);

        var body = await host.Get("/v1/status/automation");

        Assert.Equal("LIMITED", body.GetProperty("state").GetString());
        Assert.Equal(until, body.GetProperty("limited_until").GetInt64());
    }

    [Fact]
    public async Task An_old_meter_makes_a_consumer_STALE()
    {
        await using var host = await ApiHost.Start();
        using var signal = await host.Post("/v1/signal", Signal(0.1, InAWeek), ApiHost.SignalToken);
        host.Clock.Advance(TimeSpan.FromHours(30));

        var body = await host.Get("/v1/status/job");

        Assert.Equal("STALE", body.GetProperty("state").GetString());
        Assert.True(body.GetProperty("meter_age_s").GetInt64() >= 30 * 3600);
    }

    [Fact]
    public async Task An_unknown_consumer_is_404()
    {
        await using var host = await ApiHost.Start();

        using var response = await host.Client.GetAsync("/v1/status/nobody");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_lease_is_released_by_reporting_zero_cost_and_it_adds_no_use()
    {
        await using var host = await ApiHost.Start();
        using var admit = await host.Post("/v1/admit", """{"consumer":"job","estimate_usd":2}""", ApiHost.JobToken);
        var lease = (await ApiHost.Json(admit)).GetProperty("lease_id").GetString();
        Assert.Equal(1, (await host.Get("/v1/status/job")).GetProperty("open_leases").GetInt32());

        using var release = await host.Post("/v1/usage", $$"""{"consumer":"job","lease_id":"{{lease}}","cost_usd":0}""", ApiHost.JobToken);

        var body = await host.Get("/v1/status/job");
        Assert.True((await ApiHost.Json(release)).GetProperty("lease_closed").GetBoolean());
        Assert.Equal(0, body.GetProperty("open_leases").GetInt32());
        Assert.Equal(0, body.GetProperty("reserved").GetDouble());
    }
}
