using Subgate.Cli;
using Subgate.Tests.Api;

namespace Subgate.Tests.Cli;

/// <summary>ADR 0012: the sampler reads the plan's meter from <c>claude -p "/usage"</c> and posts it with the signal token.</summary>
public sealed class SampleCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();

    private static string UsageReport => $$$$"""
        {"type":"result","usage_report":{"rate_limits":{"limits":[{"kind":"session","percent":30.0,"resets_at":"{{{{ApiHost.Epoch.AddHours(3):O}}}}"},{"kind":"weekly_all","percent":12.5,"resets_at":"{{{{ApiHost.Epoch.AddDays(3):O}}}}"},{"kind":"weekly_scoped","scope":"opus","percent":8.0,"resets_at":"{{{{ApiHost.Epoch.AddDays(3):O}}}}"}]}}}
        """;

    [Fact]
    public async Task Once_posts_the_meter_the_usage_report_carries()
    {
        await using var host = await ApiHost.Start();
        var claude = _cli.Executable("claude", ["""{"type":"system"}""", UsageReport.Trim()]);
        var env = _cli.Environment(ApiHost.SignalToken, host.CreateHandler(), extra: (CliEnvironment.ClaudeVariable, claude));

        var result = await CliHarness.Run(env, "sample", "--once");

        Assert.Equal(0, result.ExitCode);
        var windows = (await host.Get("/v1/status")).GetProperty("windows");
        Assert.Equal(0.3, windows.GetProperty("5h").GetProperty("utilization").GetDouble(), 6);
        Assert.Equal(0.125, windows.GetProperty("7d").GetProperty("utilization").GetDouble(), 6);
        Assert.Equal(0.08, windows.GetProperty("7d_scoped:opus").GetProperty("utilization").GetDouble(), 6);
    }

    [Fact]
    public async Task Once_fails_with_exit_1_when_claude_prints_no_usage_report()
    {
        await using var host = await ApiHost.Start();
        var claude = _cli.Executable("claude", ["""{"type":"system"}"""]);
        var env = _cli.Environment(ApiHost.SignalToken, host.CreateHandler(), extra: (CliEnvironment.ClaudeVariable, claude));

        var result = await CliHarness.Run(env, "sample", "--once");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("no usage_report", result.Error);
    }

    [Fact]
    public async Task Once_fails_with_exit_1_when_claude_is_not_there()
    {
        await using var host = await ApiHost.Start();
        var env = _cli.Environment(
            ApiHost.SignalToken, host.CreateHandler(), extra: (CliEnvironment.ClaudeVariable, Path.Combine(_cli.Directory, "no-such-claude")));

        var result = await CliHarness.Run(env, "sample", "--once");

        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task Once_fails_with_exit_1_when_subgate_is_down()
    {
        var claude = _cli.Executable("claude", [UsageReport.Trim()]);
        var env = _cli.Environment(ApiHost.SignalToken, new DownHandler(), extra: (CliEnvironment.ClaudeVariable, claude));

        var result = await CliHarness.Run(env, "sample", "--once");

        Assert.Equal(1, result.ExitCode);
    }

    [Theory]
    [InlineData("--interval")]
    [InlineData("--interval=0m")]
    [InlineData("--interval=soon")]
    [InlineData("--every=30m")]
    public async Task Bad_options_print_usage_and_exit_64(string option)
    {
        var env = _cli.Environment(ApiHost.SignalToken, new DownHandler());

        var result = await CliHarness.Run(env, "sample", option);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("usage:", result.Output);
    }

    public void Dispose() => _cli.Dispose();
}
