using Subgate.Cli;
using Subgate.Tests.Api;

namespace Subgate.Tests.Cli;

/// <summary>T13 and the line the owner sees (ADR 0008, 0012).</summary>
public sealed class StatuslineCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();

    internal static string Input(double fiveHour = 20, double sevenDay = 41.5) => FormattableString.Invariant($$"""
        {
          "session_id": "session-placeholder",
          "model": { "id": "model-a", "display_name": "Model A" },
          "cost": { "total_cost_usd": 0.4 },
          "rate_limits": {
            "five_hour": { "used_percentage": {{fiveHour}}, "resets_at": {{ApiHost.Epoch.AddHours(3).ToUnixTimeSeconds()}} },
            "seven_day": { "used_percentage": {{sevenDay}}, "resets_at": {{ApiHost.Epoch.AddDays(3).ToUnixTimeSeconds()}} }
          }
        }
        """);

    [Fact]
    public async Task The_line_shows_the_model_the_plan_s_week_and_what_is_left_of_the_owner_s_share()
    {
        await using var host = await ApiHost.Start();
        var env = _cli.Environment(ApiHost.OwnerToken, host.CreateHandler(), consumer: "you", stdin: Input());

        var result = await CliHarness.Run(env, "statusline");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Model A | week 42% used | you 23% left\n", result.Output.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task It_posts_the_meter_and_the_session_s_cost_to_subgate()
    {
        await using var host = await ApiHost.Start();
        var env = _cli.Environment(ApiHost.OwnerToken, host.CreateHandler(), consumer: "you", stdin: Input(fiveHour: 20, sevenDay: 41.5));

        await CliHarness.Run(env, "statusline");

        var status = await host.Get("/v1/status");
        Assert.Equal(0.2, status.GetProperty("windows").GetProperty("5h").GetProperty("utilization").GetDouble(), 6);
        Assert.Equal(0.415, status.GetProperty("windows").GetProperty("7d").GetProperty("utilization").GetDouble(), 6);
        Assert.True(status.GetProperty("consumers").GetProperty("you").GetProperty("weighted_usd").GetDouble() > 0, "the owner's session was counted");
    }

    [Fact]
    public async Task Posting_the_same_session_again_counts_only_what_is_new()
    {
        await using var host = await ApiHost.Start();
        var env = () => _cli.Environment(ApiHost.OwnerToken, host.CreateHandler(), consumer: "you", stdin: Input());

        await CliHarness.Run(env(), "statusline");
        var once = (await host.Get("/v1/status")).GetProperty("consumers").GetProperty("you").GetProperty("weighted_usd").GetDouble();
        await CliHarness.Run(env(), "statusline");
        var twice = (await host.Get("/v1/status")).GetProperty("consumers").GetProperty("you").GetProperty("weighted_usd").GetDouble();

        Assert.Equal(once, twice, 9);
    }

    [Fact]
    public async Task Input_with_no_rate_limits_still_prints_the_line()
    {
        await using var host = await ApiHost.Start();
        var env = _cli.Environment(
            ApiHost.OwnerToken, host.CreateHandler(), consumer: "you",
            stdin: """{"session_id":"session-placeholder","model":{"display_name":"Model A"},"cost":{"total_cost_usd":0.1}}""");

        var result = await CliHarness.Run(env, "statusline");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Model A", result.Output.Trim());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    public async Task Whatever_it_is_fed_it_prints_a_line_and_exits_0(string stdin)
    {
        await using var host = await ApiHost.Start();
        var env = _cli.Environment(ApiHost.OwnerToken, host.CreateHandler(), consumer: "you", stdin: stdin);

        var result = await CliHarness.Run(env, "statusline");

        Assert.Equal(0, result.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Output));
    }

    [Fact]
    public async Task Without_a_token_it_prints_the_plain_line_and_says_why_on_stderr()
    {
        var env = _cli.Environment(null, new DownHandler(), consumer: "you", stdin: Input());

        var result = await CliHarness.Run(env, "statusline");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Model A | week 42% used", result.Output.Trim());
        Assert.Contains(CliEnvironment.TokenVariable, result.Error);
    }

    [Fact]
    public async Task A_wrong_token_prints_the_plain_line_and_never_the_token()
    {
        await using var host = await ApiHost.Start();
        var env = _cli.Environment("not-the-token", host.CreateHandler(), consumer: "you", stdin: Input());

        var result = await CliHarness.Run(env, "statusline");

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("not-the-token", result.Output + result.Error);
    }

    public void Dispose() => _cli.Dispose();
}
