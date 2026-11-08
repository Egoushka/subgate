using Subgate.Cli;
using Subgate.Tests.Api;
using Subgate.Tests.Store;

namespace Subgate.Tests.Cli;

/// <summary>Runs <c>Program.Run</c> as a client would: its own environment, stdin, queue directory and transport.</summary>
internal sealed class CliHarness : IDisposable
{
    public CliHarness()
    {
        Directory = System.IO.Directory.CreateTempSubdirectory("subgate-cli-").FullName;
    }

    public string Directory { get; }

    public string QueueDirectory => Path.Combine(Directory, "queue");

    public string QueueFile(string consumer) => Path.Combine(QueueDirectory, $"queue-{consumer}.jsonl");

    /// <summary>A stand-in for Claude Code: a shell script that prints these lines, then exits with <paramref name="exitCode"/>.</summary>
    public string Script(string name, IEnumerable<string> lines, int exitCode = 0)
    {
        var path = Path.Combine(Directory, name);
        File.WriteAllText(path, string.Concat(lines.Select(line => $"printf '%s\\n' '{line}'\n")) + $"exit {exitCode}\n");
        return path;
    }

    /// <summary>A stand-in for the <c>claude</c> executable itself (the sampler starts it directly).</summary>
    public string Executable(string name, IEnumerable<string> lines)
    {
        var path = Script(name, lines);
        File.WriteAllText(path, "#!/bin/sh\n" + File.ReadAllText(path));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return path;
    }

    public CliEnvironment Environment(
        string? token, HttpMessageHandler? handler, string? consumer = null, string stdin = "", TimeProvider? clock = null,
        params (string Name, string Value)[] extra)
    {
        var variables = new Dictionary<string, string>
        {
            [CliEnvironment.UrlVariable] = "http://subgate.test",
            [CliEnvironment.QueueDirectoryVariable] = QueueDirectory,
        };
        if (token is not null)
        {
            variables[CliEnvironment.TokenVariable] = token;
        }

        if (consumer is not null)
        {
            variables[CliEnvironment.ConsumerVariable] = consumer;
        }

        foreach (var (name, value) in extra)
        {
            variables[name] = value;
        }

        return new CliEnvironment(
            name => variables.GetValueOrDefault(name), new StringReader(stdin), clock ?? new FakeClock(ApiHost.Epoch), handler);
    }

    /// <summary>Runs off the test's synchronization context: the commands block until they are done.</summary>
    public static Task<CliResult> Run(CliEnvironment environment, params string[] args) =>
        Task.Run(() =>
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = Program.Run(args, output, error, environment);
            return new CliResult(exitCode, output.ToString(), error.ToString());
        });

    public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
}

internal sealed record CliResult(int ExitCode, string Output, string Error);

/// <summary>subgate being down: nothing answers.</summary>
internal sealed class DownHandler : HttpMessageHandler
{
    public int Requests { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests++;
        throw new HttpRequestException("connection refused");
    }
}

/// <summary>subgate answering slowly (and giving up when the caller does).</summary>
internal sealed class SlowHandler(TimeSpan delay) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Delay(delay, cancellationToken);
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
    }
}

/// <summary>Switches between subgate being down and being there, for a run before and a run after the outage.</summary>
internal sealed class SwitchHandler : HttpMessageHandler
{
    public HttpMessageHandler? Up { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Up is null
            ? throw new HttpRequestException("connection refused")
            : new HttpMessageInvoker(Up).SendAsync(request, cancellationToken);
}
