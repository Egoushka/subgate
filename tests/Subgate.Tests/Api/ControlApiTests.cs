using System.Net;
using System.Text.Json;
using Subgate.Store;

namespace Subgate.Tests.Api;

/// <summary>The control API's calls and answers (ADR 0007): what chargehand and the sampler rely on.</summary>
public sealed class ControlApiTests
{
    private const string SevenDay = """{"source":"example-sampler","sent_at":1793000000,"windows":[{"kind":"7d","utilization":0.42,"resets_at":1793600000}]}""";

    private static long InAWeek => ApiHost.Epoch.AddDays(5).ToUnixTimeSeconds();

    [Fact]
    public async Task Admit_in_observe_mode_allows_and_opens_a_lease()
    {
        await using var host = await ApiHost.Start();

        using var response = await host.Post("/v1/admit", """{"consumer":"job"}""", ApiHost.JobToken);
        var body = await ApiHost.Json(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("allow", body.GetProperty("decision").GetString());
        Assert.Equal("allow", body.GetProperty("would").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("lease_id").GetString()));
        Assert.Equal(7200, body.GetProperty("lease_ttl_s").GetInt64());
        Assert.Equal(0.45, body.GetProperty("share").GetDouble());
        Assert.Equal("OK", body.GetProperty("state").GetString());
        Assert.Equal(1, (await host.Get("/v1/status")).GetProperty("consumers").GetProperty("job").GetProperty("open_leases").GetInt32());
    }

    [Fact]
    public async Task The_owner_is_allowed_without_a_lease()
    {
        await using var host = await ApiHost.Start();

        using var response = await host.Post("/v1/admit", """{"consumer":"you"}""", ApiHost.OwnerToken);
        var body = await ApiHost.Json(response);

        Assert.Equal("allow", body.GetProperty("decision").GetString());
        Assert.False(body.TryGetProperty("lease_id", out _));
    }

    [Fact]
    public async Task Admit_answers_with_the_week_s_reset_once_a_meter_is_known()
    {
        await using var host = await ApiHost.Start();
        using var signal = await host.Post("/v1/signal", $$$"""{"source":"s","windows":[{"kind":"7d","utilization":0.1,"resets_at":{{{InAWeek}}}}]}""", ApiHost.SignalToken);

        using var response = await host.Post("/v1/admit", """{"consumer":"job"}""", ApiHost.JobToken);

        Assert.Equal(InAWeek, (await ApiHost.Json(response)).GetProperty("reset_at").GetInt64());
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"consumer":""}""")]
    [InlineData("""{"consumer":"job","estimate_usd":-1}""")]
    [InlineData("""not json""")]
    [InlineData("")]
    public async Task A_bad_admit_body_is_400(string body)
    {
        await using var host = await ApiHost.Start();

        using var response = await host.Post("/v1/admit", body, ApiHost.JobToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admit_does_not_need_a_JSON_content_type()
    {
        await using var host = await ApiHost.Start();
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/admit")
        {
            Content = new StringContent("""{"consumer":"job"}""", System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"),
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {ApiHost.JobToken}");

        using var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Usage_closes_the_lease_and_counts_the_cost()
    {
        await using var host = await ApiHost.Start();
        using var admit = await host.Post("/v1/admit", """{"consumer":"job","estimate_usd":1}""", ApiHost.JobToken);
        var lease = (await ApiHost.Json(admit)).GetProperty("lease_id").GetString();

        using var response = await host.Post(
            "/v1/usage",
            """{"consumer":"job","lease_id":"LEASE","route":"plan","models":{"model-a":{"input":10,"output":5,"cost_usd":0.75},"model-b":{"cost_usd":0.25}}}""".Replace("LEASE", lease, StringComparison.Ordinal),
            ApiHost.JobToken);
        var body = await ApiHost.Json(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1.0, body.GetProperty("counted_usd").GetDouble(), 6);
        Assert.True(body.GetProperty("lease_closed").GetBoolean());
        var job = (await host.Get("/v1/status")).GetProperty("consumers").GetProperty("job");
        Assert.Equal(0, job.GetProperty("open_leases").GetInt32());
        Assert.Equal(1.0, job.GetProperty("weighted_usd").GetDouble(), 6);
    }

    [Fact]
    public async Task A_status_line_s_running_total_counts_once_however_often_it_repeats()
    {
        await using var host = await ApiHost.Start();
        const string Report = """{"consumer":"you","session_id":"s-1","cost_usd":2.0,"cumulative":true}""";

        using var first = await host.Post("/v1/usage", Report, ApiHost.OwnerToken);
        using var again = await host.Post("/v1/usage", Report, ApiHost.OwnerToken);
        using var more = await host.Post("/v1/usage", """{"consumer":"you","session_id":"s-1","cost_usd":2.5,"cumulative":true}""", ApiHost.OwnerToken);

        Assert.Equal(2.0, (await ApiHost.Json(first)).GetProperty("counted_usd").GetDouble(), 6);
        Assert.Equal(0.0, (await ApiHost.Json(again)).GetProperty("counted_usd").GetDouble(), 6);
        Assert.Equal(0.5, (await ApiHost.Json(more)).GetProperty("counted_usd").GetDouble(), 6);
    }

    [Fact]
    public async Task Api_route_use_is_kept_apart_from_the_plan()
    {
        await using var host = await ApiHost.Start();

        using var response = await host.Post("/v1/usage", """{"consumer":"job","route":"api","cost_usd":3}""", ApiHost.JobToken);

        var job = (await host.Get("/v1/status")).GetProperty("consumers").GetProperty("job");
        Assert.Equal(3.0, job.GetProperty("api_usd").GetDouble(), 6);
        Assert.Equal(0.0, job.GetProperty("weighted_usd").GetDouble(), 6);
    }

    [Theory]
    [InlineData("""{"consumer":"job"}""")]
    [InlineData("""{"consumer":"job","cost_usd":-1}""")]
    [InlineData("""{"consumer":"job","route":"free","cost_usd":1}""")]
    [InlineData("""{"consumer":"job","models":{"a":{"input":1}}}""")]
    public async Task A_bad_usage_body_is_400(string body)
    {
        await using var host = await ApiHost.Start();

        using var response = await host.Post("/v1/usage", body, ApiHost.JobToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_signal_moves_the_meter_in_the_status()
    {
        await using var host = await ApiHost.Start();

        using var response = await host.Post("/v1/signal", SevenDay, ApiHost.SignalToken);
        var status = await host.Get("/v1/status");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0.42, status.GetProperty("windows").GetProperty("7d").GetProperty("utilization").GetDouble());
        Assert.Equal(1793600000, status.GetProperty("windows").GetProperty("7d").GetProperty("resets_at").GetInt64());
        Assert.Equal("observe", status.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task A_scoped_window_is_listed_with_its_scope()
    {
        await using var host = await ApiHost.Start();

        using var response = await host.Post(
            "/v1/signal",
            """{"source":"s","windows":[{"kind":"7d_scoped","scope":"opus","utilization":0.3,"resets_at":1793600000}]}""",
            ApiHost.SignalToken);

        var windows = (await host.Get("/v1/status")).GetProperty("windows");
        Assert.Equal(0.3, windows.GetProperty("7d_scoped:opus").GetProperty("utilization").GetDouble());
    }

    [Fact]
    public async Task A_rejected_window_makes_the_account_limited_until_its_reset()
    {
        await using var host = await ApiHost.Start();

        using var response = await host.Post(
            "/v1/signal",
            $$"""{"source":"s","windows":[{"kind":"5h","utilization":1,"resets_at":{{ApiHost.Epoch.AddHours(2).ToUnixTimeSeconds()}},"status":"rejected"}]}""",
            ApiHost.SignalToken);

        var account = (await host.Get("/v1/status")).GetProperty("account");
        Assert.Equal("LIMITED", account.GetProperty("state").GetString());
        Assert.Equal(ApiHost.Epoch.AddHours(2).ToUnixTimeSeconds(), account.GetProperty("limited_until").GetInt64());
    }

    [Theory]
    [InlineData("""{"source":"s","windows":[]}""")]
    [InlineData("""{"windows":[{"kind":"7d","utilization":0.1,"resets_at":1793600000}]}""")]
    [InlineData("""{"source":"s","windows":[{"kind":"7d","utilization":1.5,"resets_at":1793600000}]}""")]
    [InlineData("""{"source":"s","windows":[{"kind":"7d","utilization":-0.1,"resets_at":1793600000}]}""")]
    [InlineData("""{"source":"s","windows":[{"kind":"1h","utilization":0.1,"resets_at":1793600000}]}""")]
    [InlineData("""{"source":"s","windows":[{"kind":"7d","utilization":0.1}]}""")]
    [InlineData("""{"source":"s","windows":[{"kind":"7d_scoped","utilization":0.1,"resets_at":1793600000}]}""")]
    [InlineData("""{"source":"s","windows":[{"kind":"7d","utilization":0.1,"resets_at":99999999999999}]}""")]
    public async Task A_bad_signal_is_400_and_moves_nothing(string body)
    {
        await using var host = await ApiHost.Start();

        using var response = await host.Post("/v1/signal", body, ApiHost.SignalToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await host.Get("/v1/status")).GetProperty("windows").EnumerateObject());
    }

    [Fact]
    public async Task The_admin_api_does_not_exist_in_v0_1()
    {
        await using var host = await ApiHost.Start();

        using var put = await host.Client.PutAsync("/v1/consumers/x", new StringContent("{}"));
        using var get = await host.Client.GetAsync("/v1/consumers");
        using var delete = await host.Client.DeleteAsync("/v1/consumers/x/override");

        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task The_status_says_when_the_state_file_could_not_be_used()
    {
        await using var host = await ApiHost.Start(storeNote: "the state file could not be used");

        var store = (await host.Get("/v1/status")).GetProperty("store");

        Assert.False(store.GetProperty("ok").GetBoolean());
        Assert.Equal("the state file could not be used", store.GetProperty("note").GetString());
    }

    [Fact]
    public async Task A_failing_store_is_counted_and_the_ledger_keeps_answering()
    {
        await using var host = await ApiHost.Start(new FailingStore());

        using var admit = await host.Post("/v1/admit", """{"consumer":"job"}""", ApiHost.JobToken);
        var store = (await host.Get("/v1/status")).GetProperty("store");

        Assert.Equal(HttpStatusCode.OK, admit.StatusCode);
        Assert.False(store.GetProperty("ok").GetBoolean());
        Assert.True(store.GetProperty("errors").GetInt64() > 0);
    }

    private sealed class FailingStore : ILedgerStore
    {
        public Switchknife.Pace.LedgerState? Load() => null;

        public void Save(Switchknife.Pace.LedgerState state) => throw new IOException("disk full");

        public void Append(IReadOnlyList<Switchknife.Pace.LedgerEvent> events) => throw new IOException("disk full");

        public void Dispose()
        {
        }
    }
}
