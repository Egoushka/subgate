namespace Subgate.Providers.Anthropic;

/// <summary>
/// A status line reports a session's running total cost each time it is drawn, not a delta.
/// Tracks the high-water mark per session so the same total is counted once (T16); a lower
/// total (a new session re-using an id, or an out-of-order read) is ignored.
/// </summary>
public sealed class CumulativeSessionUsage
{
    private readonly Dictionary<string, double> _highWaterMarks = new();

    public double Observe(string sessionId, double totalCostUsd)
    {
        if (_highWaterMarks.TryGetValue(sessionId, out var current) && totalCostUsd <= current)
        {
            return current;
        }

        _highWaterMarks[sessionId] = totalCostUsd;
        return totalCostUsd;
    }
}
