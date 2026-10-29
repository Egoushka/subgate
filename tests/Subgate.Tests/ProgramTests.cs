namespace Subgate.Tests;

public class ProgramTests
{
    [Fact]
    public void Version_prints_a_non_empty_line_and_exits_zero()
    {
        var output = new StringWriter();

        var exitCode = Program.Run(["--version"], output);

        Assert.Equal(0, exitCode);
        Assert.False(string.IsNullOrWhiteSpace(output.ToString()));
    }

    [Fact]
    public void No_arguments_prints_usage_and_exits_non_zero() =>
        AssertUsage([]);

    [Fact]
    public void An_unknown_argument_prints_usage_and_exits_non_zero() =>
        AssertUsage(["--unknown"]);

    private static void AssertUsage(string[] args)
    {
        var output = new StringWriter();

        var exitCode = Program.Run(args, output);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("--version", output.ToString());
    }
}
