using System.Net;
using Subgate.Store;

namespace Subgate.Tests.Api;

/// <summary>T14: admit and usage need a consumer token, a signal token posts signals only, tokens are never logged or stored.</summary>
public sealed class AuthTests
{
    private const string AdmitJob = """{"consumer":"job"}""";
    private const string Signal = """{"source":"s","sent_at":1,"windows":[{"kind":"7d","utilization":0.2,"resets_at":1793000000}]}""";

    [Fact]
    public async Task Admit_and_usage_without_a_token_are_401()
    {
        await using var host = await ApiHost.Start();

        using var admit = await host.Post("/v1/admit", AdmitJob);
        using var usage = await host.Post("/v1/usage", """{"consumer":"job","cost_usd":1}""");

        Assert.Equal(HttpStatusCode.Unauthorized, admit.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, usage.StatusCode);
    }

    [Theory]
    [InlineData("not-a-known-token")]
    [InlineData("")]
    public async Task An_unknown_token_is_401(string token)
    {
        await using var host = await ApiHost.Start();

        using var response = await host.Post("/v1/admit", AdmitJob, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_signal_token_posts_signals_only()
    {
        await using var host = await ApiHost.Start();

        using var admit = await host.Post("/v1/admit", AdmitJob, ApiHost.SignalToken);
        using var usage = await host.Post("/v1/usage", """{"consumer":"job","cost_usd":1}""", ApiHost.SignalToken);
        using var signal = await host.Post("/v1/signal", Signal, ApiHost.SignalToken);

        Assert.Equal(HttpStatusCode.Forbidden, admit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, usage.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, signal.StatusCode);
    }

    [Fact]
    public async Task A_consumer_token_cannot_speak_for_another_consumer()
    {
        await using var host = await ApiHost.Start();

        using var admit = await host.Post("/v1/admit", """{"consumer":"automation"}""", ApiHost.JobToken);
        using var usage = await host.Post("/v1/usage", """{"consumer":"automation","cost_usd":1}""", ApiHost.JobToken);

        Assert.Equal(HttpStatusCode.Forbidden, admit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, usage.StatusCode);
        Assert.Equal(0, host.Ledger.Status().Consumers["automation"].WeightedUsd);
    }

    [Fact]
    public async Task A_consumer_token_may_post_a_signal()
    {
        await using var host = await ApiHost.Start();

        using var signal = await host.Post("/v1/signal", Signal, ApiHost.OwnerToken);

        Assert.Equal(HttpStatusCode.NoContent, signal.StatusCode);
    }

    [Fact]
    public async Task Status_metrics_and_healthz_are_open()
    {
        await using var host = await ApiHost.Start();

        foreach (var path in new[] { "/v1/status", "/metrics", "/healthz" })
        {
            using var response = await host.Client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task Tokens_appear_in_no_log_line_and_no_store_row()
    {
        var dir = Directory.CreateTempSubdirectory("subgate-auth-");
        try
        {
            var tokens = new[] { ApiHost.OwnerToken, ApiHost.JobToken, ApiHost.AutomationToken, ApiHost.SignalToken };
            CapturedLogs logs;
            await using (var host = await ApiHost.Start(new SqliteLedgerStore(Path.Combine(dir.FullName, "state.db"))))
            {
                logs = host.Logs;
                using var admit = await host.Post("/v1/admit", AdmitJob, ApiHost.JobToken);
                using var denied = await host.Post("/v1/admit", AdmitJob, "an-unknown-token-it-must-not-log");
                using var forbidden = await host.Post("/v1/usage", """{"consumer":"job","cost_usd":1}""", ApiHost.SignalToken);
                using var usage = await host.Post("/v1/usage", """{"consumer":"job","cost_usd":1.5}""", ApiHost.JobToken);
                using var signal = await host.Post("/v1/signal", Signal, ApiHost.SignalToken);
                await host.Get("/v1/status");
            }

            Assert.NotEmpty(logs.Lines);
            foreach (var token in tokens.Append("an-unknown-token-it-must-not-log"))
            {
                Assert.DoesNotContain(logs.Lines, line => line.Contains(token, StringComparison.Ordinal));
            }

            var stored = dir.EnumerateFiles().SelectMany(f => File.ReadAllBytes(f.FullName)).ToArray();
            Assert.NotEmpty(stored);
            foreach (var token in tokens)
            {
                Assert.Equal(-1, stored.AsSpan().IndexOf(System.Text.Encoding.UTF8.GetBytes(token)));
            }
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
