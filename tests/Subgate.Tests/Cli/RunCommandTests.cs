using Subgate.Tests.Api;

namespace Subgate.Tests.Cli;

/// <summary>T11 and T12: <c>subgate run</c> asks, runs the job, reports; and what it does when subgate is not there.</summary>
public sealed class RunCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();

    private static long FiveHourReset => ApiHost.Epoch.AddHours(3).ToUnixTimeSeconds();

    private static long SevenDayReset => ApiHost.Epoch.AddDays(3).ToUnixTimeSeconds();

    private string Job(int exitCode = 0) => _cli.Script(
        "job.sh",
        [
            "not json, a line the job printed",
            $$$"""{"type":"rate_limit_event","rate_limit_info":{"status":"allowed","rateLimitType":"five_hour","resetsAt":{{{FiveHourReset}}},"utilization":0.3}}""",
            $$$"""{"type":"rate_limit_event","rate_limit_info":{"status":"allowed","rateLimitType":"seven_day","resetsAt":{{{SevenDayReset}}},"utilization":0.12}}""",
            """{"type":"result","session_id":"session-placeholder","total_cost_usd":0.07,"modelUsage":{"model-a":{"costUSD":0.05},"model-b":{"costUSD":0.02}}}""",
        ],
        exitCode);

    [Fact]
    public async Task T11_Run_asks_then_reports_the_meter_and_the_usage_and_passes_the_exit_code_through()
    {
        await using var host = await ApiHost.Start();
        var env = _cli.Environment(ApiHost.JobToken, host.CreateHandler(), consumer: "job");

        var result = await CliHarness.Run(env, "run", "--", "sh", Job(exitCode: 3));

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("not json, a line the job printed", result.Output);
        Assert.Contains("\"type\":\"result\"", result.Output);
        var status = await host.Get("/v1/status");
        var job = status.GetProperty("consumers").GetProperty("job");
        Assert.Equal(0, job.GetProperty("open_leases").GetInt32());
        Assert.True(job.GetProperty("weighted_usd").GetDouble() > 0, "the job's usage was counted");
        Assert.Equal(0.3, status.GetProperty("windows").GetProperty("5h").GetProperty("utilization").GetDouble());
        Assert.Equal(0.12, status.GetProperty("windows").GetProperty("7d").GetProperty("utilization").GetDouble());
        Assert.False(File.Exists(_cli.QueueFile("job")), "nothing waits when subgate took the report");
    }

    [Fact]
    public async Task Run_names_the_consumer_with_an_option_as_well()
    {
        await using var host = await ApiHost.Start();
        var env = _cli.Environment(ApiHost.JobToken, host.CreateHandler());

        var result = await CliHarness.Run(env, "run", "--consumer=job", "--", "sh", Job());

        Assert.Equal(0, result.ExitCode);
        Assert.True((await host.Get("/v1/status")).GetProperty("consumers").GetProperty("job").GetProperty("weighted_usd").GetDouble() > 0);
    }

    [Fact]
    public async Task A_job_that_printed_no_result_still_closes_its_lease()
    {
        await using var host = await ApiHost.Start();
        var env = _cli.Environment(ApiHost.JobToken, host.CreateHandler(), consumer: "job");

        var result = await CliHarness.Run(env, "run", "--", "sh", _cli.Script("quiet.sh", ["no stream-json here"]));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, (await host.Get("/v1/status")).GetProperty("consumers").GetProperty("job").GetProperty("open_leases").GetInt32());
    }

    [Fact]
    public async Task T12_Run_when_subgate_is_down_runs_the_job_queues_the_report_and_sends_it_when_subgate_is_back()
    {
        await using var host = await ApiHost.Start();
        var transport = new SwitchHandler();
        var env = _cli.Environment(ApiHost.JobToken, transport, consumer: "job");

        var down = await CliHarness.Run(env, "run", "--", "sh", Job(exitCode: 2));

        Assert.Equal(2, down.ExitCode);
        Assert.Contains("\"type\":\"result\"", down.Output);
        Assert.Contains("running the job anyway", down.Error);
        Assert.True(File.Exists(_cli.QueueFile("job")));
        Assert.Single(File.ReadAllLines(_cli.QueueFile("job")));
        Assert.Equal(0, (await host.Get("/v1/status")).GetProperty("consumers").GetProperty("job").GetProperty("weighted_usd").GetDouble());

        transport.Up = host.CreateHandler();
        var back = await CliHarness.Run(env, "run", "--", "sh", _cli.Script("next.sh", ["the next job"]));

        Assert.Equal(0, back.ExitCode);
        Assert.False(File.Exists(_cli.QueueFile("job")), "the queue went out with the next run");
        var status = await host.Get("/v1/status");
        Assert.True(status.GetProperty("consumers").GetProperty("job").GetProperty("weighted_usd").GetDouble() > 0, "the first job's usage arrived");
        Assert.Equal(0.3, status.GetProperty("windows").GetProperty("5h").GetProperty("utilization").GetDouble());
    }

    [Fact]
    public async Task T12_Run_with_on_unavailable_hold_does_not_start_the_command_and_exits_75()
    {
        var marker = Path.Combine(_cli.Directory, "started");
        var env = _cli.Environment(ApiHost.JobToken, new DownHandler(), consumer: "job");

        var result = await CliHarness.Run(env, "run", "--on-unavailable=hold", "--", "sh", "-c", $"touch '{marker}'");

        Assert.Equal(75, result.ExitCode);
        Assert.False(File.Exists(marker));
        Assert.False(File.Exists(_cli.QueueFile("job")));
    }

    [Fact]
    public async Task A_wrong_token_is_a_configuration_error_when_the_job_must_not_start_without_subgate()
    {
        await using var host = await ApiHost.Start();
        var env = _cli.Environment("not-the-token", host.CreateHandler(), consumer: "job");

        var result = await CliHarness.Run(env, "run", "--on-unavailable", "hold", "--", "sh", "-c", "exit 0");

        Assert.Equal(78, result.ExitCode);
        Assert.Contains("401", result.Error);
        Assert.DoesNotContain("not-the-token", result.Error + result.Output);
    }

    [Fact]
    public async Task A_command_that_does_not_exist_exits_127()
    {
        var env = _cli.Environment(ApiHost.JobToken, new DownHandler(), consumer: "job");

        var result = await CliHarness.Run(env, "run", "--", Path.Combine(_cli.Directory, "no-such-command"));

        Assert.Equal(127, result.ExitCode);
    }

    [Theory]
    [InlineData("run")]
    [InlineData("run --")]
    [InlineData("run --consumer job")]
    [InlineData("run --on-unavailable=maybe -- true")]
    [InlineData("run --bogus -- true")]
    public async Task Run_without_a_command_or_with_unknown_options_prints_usage_and_exits_64(string line)
    {
        var env = _cli.Environment(ApiHost.JobToken, new DownHandler(), consumer: "job");

        var result = await CliHarness.Run(env, line.Split(' '));

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("usage:", result.Output);
    }

    [Fact]
    public async Task Run_needs_a_consumer_and_a_token()
    {
        var noConsumer = await CliHarness.Run(_cli.Environment(ApiHost.JobToken, new DownHandler()), "run", "--", "true");
        var noToken = await CliHarness.Run(_cli.Environment(null, new DownHandler(), consumer: "job"), "run", "--", "true");

        Assert.Equal(78, noConsumer.ExitCode);
        Assert.Equal(78, noToken.ExitCode);
    }

    public void Dispose() => _cli.Dispose();
}
