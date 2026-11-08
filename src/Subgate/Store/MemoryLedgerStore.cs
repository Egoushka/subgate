using Switchknife.Pace;

namespace Subgate.Store;

/// <summary>Keeps the state in memory: for tests, and for a start where the SQLite file cannot be used.</summary>
public sealed class MemoryLedgerStore : ILedgerStore
{
    private readonly object _gate = new();
    private LedgerState? _state;
    private readonly List<LedgerEvent> _events = [];

    public IReadOnlyList<LedgerEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return [.. _events];
            }
        }
    }

    public LedgerState? Load()
    {
        lock (_gate)
        {
            return _state;
        }
    }

    public void Save(LedgerState state)
    {
        lock (_gate)
        {
            _state = state;
        }
    }

    public void Append(IReadOnlyList<LedgerEvent> events)
    {
        lock (_gate)
        {
            _events.AddRange(events);
        }
    }

    public void Dispose()
    {
    }
}
