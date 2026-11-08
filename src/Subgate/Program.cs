using Subgate.Cli;

namespace Subgate;

public static class Program
{
    public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    public static int Run(string[] args, TextWriter output, TextWriter? error = null)
    {
        if (args is ["--version"])
        {
            output.WriteLine(Version);
            return 0;
        }

        if (args is ["serve", ..])
        {
            return ServeCommand.Run(args[1..], output, error ?? TextWriter.Null);
        }

        output.WriteLine("usage: subgate --version | serve --config <file>");
        return 64;
    }

    public static string Version =>
        typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";
}
