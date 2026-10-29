namespace Subgate;

public static class Program
{
    public static int Main(string[] args) => Run(args, Console.Out);

    public static int Run(string[] args, TextWriter output)
    {
        if (args is ["--version"])
        {
            output.WriteLine(Version);
            return 0;
        }

        output.WriteLine("usage: subgate --version");
        return 64;
    }

    public static string Version =>
        typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";
}
