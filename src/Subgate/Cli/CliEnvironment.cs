using System.Text;

namespace Subgate.Cli;

/// <summary>
/// What the client commands (<c>run</c>, <c>sample</c>, <c>statusline</c>) take from outside: environment variables, stdin, the clock
/// and, in tests, the HTTP transport.
/// </summary>
/// <param name="Variable">Reads an environment variable; null when it is not set.</param>
/// <param name="Input">What <c>statusline</c> reads its JSON from.</param>
/// <param name="Clock">The time of reports and of the sampler's pauses.</param>
/// <param name="Handler">Replaces the network; null for real HTTP.</param>
public sealed record CliEnvironment(Func<string, string?> Variable, TextReader Input, TimeProvider Clock, HttpMessageHandler? Handler = null)
{
    /// <summary>The variables a client reads. None of them is needed by <c>serve</c>.</summary>
    public const string UrlVariable = "SUBGATE_URL";
    public const string TokenVariable = "SUBGATE_TOKEN";
    public const string ConsumerVariable = "SUBGATE_CONSUMER";
    public const string QueueDirectoryVariable = "SUBGATE_QUEUE_DIR";
    public const string ClaudeVariable = "SUBGATE_CLAUDE";

    private const string DefaultUrl = "http://127.0.0.1:8070";

    public static CliEnvironment FromProcess() => new(
        Environment.GetEnvironmentVariable,
        new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false)),
        TimeProvider.System);

    /// <summary>The control API's address; null when SUBGATE_URL is not an http or https address.</summary>
    internal Uri? Url()
    {
        var text = Variable(UrlVariable) is { Length: > 0 } set ? set : DefaultUrl;
        if (!Uri.TryCreate(text.EndsWith('/') ? text : text + "/", UriKind.Absolute, out var url)
            || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return url;
    }

    internal string? Token() => Variable(TokenVariable) is { Length: > 0 } token ? token : null;

    /// <summary>The consumer a report is for: the option, else SUBGATE_CONSUMER.</summary>
    internal string? Consumer(string? option) =>
        option ?? (Variable(ConsumerVariable) is { Length: > 0 } consumer ? consumer : null);

    /// <summary>Where unsent reports wait: SUBGATE_QUEUE_DIR, else the user's state directory.</summary>
    internal string QueueDirectory()
    {
        if (Variable(QueueDirectoryVariable) is { Length: > 0 } set)
        {
            return set;
        }

        if (Variable("XDG_STATE_HOME") is { Length: > 0 } state)
        {
            return Path.Combine(state, "subgate");
        }

        return Variable("HOME") is { Length: > 0 } home
            ? Path.Combine(home, ".local", "state", "subgate")
            : Path.Combine(Path.GetTempPath(), "subgate");
    }
}
