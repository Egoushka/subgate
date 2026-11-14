using System.Net;
using Subgate.Tests.Api;

namespace Subgate.Tests.Cli;

public class HealthcheckCommandTests
{
    [Fact]
    public async Task T22_exits_zero_on_200_and_asks_for_healthz()
    {
        using var harness = new CliHarness();
        var handler = new RecordingHandler(HttpStatusCode.OK);

        var result = await CliHarness.Run(harness.Environment(null, handler), "--healthcheck");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("http://subgate.test/healthz", handler.Uri);
    }

    [Fact]
    public async Task T22_exits_one_on_another_status()
    {
        using var harness = new CliHarness();

        var result = await CliHarness.Run(
            harness.Environment(null, new RecordingHandler(HttpStatusCode.ServiceUnavailable)), "--healthcheck");

        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task T22_exits_one_when_nothing_answers()
    {
        using var harness = new CliHarness();

        var result = await CliHarness.Run(harness.Environment(null, new DownHandler()), "--healthcheck");

        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task T22_exits_one_on_a_timeout()
    {
        using var harness = new CliHarness();

        var result = await CliHarness.Run(harness.Environment(null, new SlowHandler(TimeSpan.FromSeconds(30))), "--healthcheck");

        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task T22_exits_one_on_a_bad_url()
    {
        using var harness = new CliHarness();
        var environment = harness.Environment(null, new RecordingHandler(HttpStatusCode.OK), extra: (Subgate.Cli.CliEnvironment.UrlVariable, "not a url"));

        var result = await CliHarness.Run(environment, "--healthcheck");

        Assert.Equal(1, result.ExitCode);
    }

    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public string? Uri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }
}
