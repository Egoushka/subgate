using System.Net;

namespace Subgate.Tests.Api;

/// <summary>T15: a request carrying an <c>sk-ant-</c> value is refused with 400 and nothing of it is stored or logged.</summary>
public sealed class NoCredentialsTests
{
    private const string Credential = "sk-ant-api03-not-a-real-credential";

    [Theory]
    [InlineData("/v1/admit")]
    [InlineData("/v1/usage")]
    [InlineData("/v1/signal")]
    public async Task A_credential_in_a_body_is_400(string path)
    {
        await using var host = await ApiHost.Start();

        using var response = await host.Post(path, $$"""{"consumer":"job","note":"{{Credential}}"}""", ApiHost.JobToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("SK-ANT-api03-shouting")]
    [InlineData("sk-Ant-mixed-case")]
    public async Task The_match_ignores_case(string credential)
    {
        await using var host = await ApiHost.Start();

        using var response = await host.Post("/v1/usage", $$"""{"consumer":"job","note":"{{credential}}"}""", ApiHost.JobToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_credential_after_non_ASCII_text_is_still_found()
    {
        await using var host = await ApiHost.Start();

        using var response = await host.Post("/v1/usage", $$"""{"consumer":"job","note":"привіт {{Credential}}"}""", ApiHost.JobToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_credential_in_a_header_is_400()
    {
        await using var host = await ApiHost.Start();

        using var response = await host.Post("/v1/admit", """{"consumer":"job"}""", ApiHost.JobToken, ("x-api-key", Credential));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_credential_as_the_bearer_token_is_400_not_401()
    {
        await using var host = await ApiHost.Start();

        using var response = await host.Post("/v1/admit", """{"consumer":"job"}""", Credential);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/v1/status?key=sk-ant-abc")]
    [InlineData("/metrics?key=sk%2Dant%2Dabc")]
    public async Task A_credential_in_the_query_is_400(string path)
    {
        await using var host = await ApiHost.Start();

        using var response = await host.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_credential_hidden_by_a_JSON_escape_in_an_id_is_400()
    {
        await using var host = await ApiHost.Start();

        using var response = await host.Post("/v1/usage", """{"consumer":"job","session_id":"sk-ant-abc","cost_usd":1}""", ApiHost.JobToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_refused_request_is_not_stored_or_logged_or_echoed()
    {
        await using var host = await ApiHost.Start();

        using var response = await host.Post("/v1/usage", $$"""{"consumer":"job","session_id":"{{Credential}}","cost_usd":2}""", ApiHost.JobToken);
        var reply = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("sk-ant-", reply, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(host.Ledger.Snapshot().Sessions);
        Assert.Equal(0, host.Ledger.Status().Consumers["job"].WeightedUsd);
        Assert.DoesNotContain(host.Logs.Lines, line => line.Contains("sk-ant-", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_body_past_the_limit_is_413()
    {
        await using var host = await ApiHost.Start();

        using var response = await host.Post("/v1/usage", $$"""{"consumer":"job","note":"{{new string('a', 70 * 1024)}}"}""", ApiHost.JobToken);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Text_that_only_looks_similar_passes()
    {
        await using var host = await ApiHost.Start();

        using var response = await host.Post("/v1/admit", """{"consumer":"job","model":"sk-another-thing"}""", ApiHost.JobToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
