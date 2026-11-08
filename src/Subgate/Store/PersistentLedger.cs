using Switchknife.Pace;

namespace Subgate.Store;

/// <summary>
/// The engine with a store behind it: every change is written down before the call returns, so a restart loses nothing
/// (ADR 0009). A store failure is counted and kept in memory, never thrown at the caller: the ledger in memory stays right
/// and the next write retries.
/// </summary>
public sealed class PersistentLedger : IDisposable
{
    private readonly object _gate = new();
    private readonly Ledger _ledger;
    private readonly ILedgerStore _store;
    private long _storeErrors;

    public PersistentLedger(Policy policy, ILedgerStore store, TimeProvider? clock = null)
    {
        _store = store;
        _ledger = new Ledger(policy, clock, store.Load());
    }

    /// <summary>How many writes to the store failed since start.</summary>
    public long StoreErrors => Interlocked.Read(ref _storeErrors);

    public void Observe(MeterReading reading) => Write(() =>
    {
        _ledger.Observe(reading);
        return 0;
    });

    public UsageResult Report(UsageReport report) => Write(() => _ledger.Report(report));

    public Admission Admit(AdmitRequest request) => Write(() => _ledger.Admit(request));

    public LedgerStatus Status() => Write(_ledger.Status);

    public LedgerState Snapshot() => _ledger.Snapshot();

    public void Dispose() => _store.Dispose();

    private T Write<T>(Func<T> change)
    {
        lock (_gate)
        {
            var result = change();
            try
            {
                _store.Save(_ledger.Snapshot());
                _store.Append(_ledger.DrainEvents());
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                Interlocked.Increment(ref _storeErrors);
            }

            return result;
        }
    }
}
