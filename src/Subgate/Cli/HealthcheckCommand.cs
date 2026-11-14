namespace Subgate.Cli;

/// <summary>
/// <c>subgate --healthcheck</c>: the image has no shell, curl or wget, so a container healthcheck runs the binary itself.
/// Asks <c>/healthz</c> on SUBGATE_URL (default the loopback port) and exits 0 only on HTTP 200.
/// </summary>
internal static class HealthcheckCommand
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public static async Task<int> Run(TextWriter error, CliEnvironment environment)
    {
        if (environment.Url() is not { } url)
        {
            error.WriteLine($"{CliEnvironment.UrlVariable} is not an http or https address");
            return 1;
        }

        using var http = environment.Handler is null
            ? new HttpClient { Timeout = Timeout }
            : new HttpClient(environment.Handler, disposeHandler: false) { Timeout = Timeout };
        try
        {
            using var response = await http.GetAsync(new Uri(url, "healthz"));
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                return 0;
            }

            error.WriteLine($"healthz answered {(int)response.StatusCode}");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            error.WriteLine($"healthz did not answer: {e.Message}");
        }

        return 1;
    }
}
