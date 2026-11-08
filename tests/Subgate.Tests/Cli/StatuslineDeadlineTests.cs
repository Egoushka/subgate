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
    private const int LimitMs = 400;

    /// <summary>
    /// A shared CI runner can stall a timer by 100 ms or more (a macOS run took 451 ms for a 300 ms wait, alone and warm). A code
    /// path that ignores the deadline takes seconds on every attempt, so one attempt under the limit still proves the deadline works.
    /// </summary>
    private const int Attempts = 5;

    private readonly CliHarness _cli = new();

    [Fact]
    public async Task T13_With_subgate_down_it_prints_the_plain_line_within_400_ms_and_exits_0() =>
        await PrintsWithinTheLimit(new DownHandler());

    [Fact]
    public async Task T13_With_subgate_slower_than_300_ms_it_prints_the_plain_line_within_400_ms_and_exits_0() =>
        await PrintsWithinTheLimit(new SlowHandler(TimeSpan.FromSeconds(5)));

    public void Dispose() => _cli.Dispose();

    private async Task PrintsWithinTheLimit(HttpMessageHandler handler)
    {
        CliEnvironment Environment() =>
            _cli.Environment(ApiHost.OwnerToken, handler, consumer: "you", stdin: StatuslineCommandTests.Input(), clock: TimeProvider.System);

        // Not timed: the first run pays for the JIT, which a status line that runs all day does once.
        await CliHarness.Run(Environment(), "statusline");

        var timings = new List<long>();
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var timer = Stopwatch.StartNew();
            var result = await CliHarness.Run(Environment(), "statusline");
            timings.Add(timer.ElapsedMilliseconds);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("Model A | week 42% used", result.Output.Trim());
            if (timings[^1] < LimitMs)
            {
                return;
            }
        }

        Assert.Fail($"no run printed within {LimitMs} ms; took {string.Join(", ", timings)} ms");
    }
}
