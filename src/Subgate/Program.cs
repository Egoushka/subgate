using Subgate.Cli;

namespace Subgate;

public static class Program
{
    internal const string Usage = """
        usage: subgate --version
               subgate serve --config <file>
               subgate run [--consumer <name>] [--on-unavailable run|hold] -- <command…>
               subgate statusline [--consumer <name>]
               subgate sample [--once] [--interval <n>s|m|h]
               subgate token new <name>
        """;

    public static int Main(string[] args)
    {
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            stop.Cancel();
        };
        return Run(args, Console.Out, Console.Error, null, stop.Token);
    }

    /// <param name="environment">What the client commands read from outside; the process's own when null.</param>
    public static int Run(
        string[] args, TextWriter output, TextWriter? error = null, CliEnvironment? environment = null, CancellationToken stop = default)
    {
        error ??= TextWriter.Null;
        switch (args)
        {
            case ["--version"]:
                output.WriteLine(Version);
                return 0;
            case ["serve", ..]:
                return ServeCommand.Run(args[1..], output, error);
            case ["token", ..]:
                return TokenCommand.Run(args[1..], output, error);
            case ["run", ..]:
                return RunClient(env => RunCommand.Run(args[1..], output, error, env), environment);
            case ["statusline", ..]:
                return RunClient(env => StatuslineCommand.Run(args[1..], output, error, env), environment);
            case ["sample", ..]:
                return RunClient(env => SampleCommand.Run(args[1..], output, error, env, stop), environment);
            default:
                output.WriteLine(Usage);
                return 64;
        }
    }

    public static string Version =>
        typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    private static int RunClient(Func<CliEnvironment, Task<int>> command, CliEnvironment? environment) =>
        command(environment ?? CliEnvironment.FromProcess()).GetAwaiter().GetResult();
}
