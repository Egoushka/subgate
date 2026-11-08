using System.Diagnostics;
using Subgate.Cli;
using Subgate.Tests.Api;

namespace Subgate.Tests.Cli;

/// <summary>Runs alone: a deadline measured in a test process is only meaningful when no other test competes for its threads.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DeadlineCollection
{
    public const string Name = "deadline";
}

/// <summary>T13 (ADR 0008): the status line prints within 400 ms and exits 0 whatever subgate is doing.</summary>
[Collection(DeadlineCollection.Name)]
public sealed class StatuslineDeadlineTests : IDisposable
{
    private readonly CliHarness _cli = new();

    [Fact]
    public async Task T13_With_subgate_down_it_prints_the_plain_line_within_400_ms_and_exits_0()
    {
        var (result, elapsed) = await Timed(new DownHandler());

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Model A | week 42% used", result.Output.Trim());
        Assert.True(elapsed < 400, $"took {elapsed} ms");
    }

    [Fact]
    public async Task T13_With_subgate_slower_than_300_ms_it_prints_the_plain_line_within_400_ms_and_exits_0()
    {
        var (result, elapsed) = await Timed(new SlowHandler(TimeSpan.FromSeconds(5)));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Model A | week 42% used", result.Output.Trim());
        Assert.True(elapsed < 400, $"took {elapsed} ms");
    }

    public void Dispose() => _cli.Dispose();

    /// <summary>A first run warms the JIT, as the second run of a real status line does not have to; only the second is timed.</summary>
    private async Task<(CliResult Result, long ElapsedMs)> Timed(HttpMessageHandler handler)
    {
        CliEnvironment Environment() =>
            _cli.Environment(ApiHost.OwnerToken, handler, consumer: "you", stdin: StatuslineCommandTests.Input(), clock: TimeProvider.System);

        await CliHarness.Run(Environment(), "statusline");

        var timer = Stopwatch.StartNew();
        var result = await CliHarness.Run(Environment(), "statusline");
        return (result, timer.ElapsedMilliseconds);
    }
}
