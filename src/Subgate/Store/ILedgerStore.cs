using Switchknife.Pace;

namespace Subgate.Store;

/// <summary>Where the ledger's state is written down between restarts (ADR 0009).</summary>
public interface ILedgerStore : IDisposable
{
    /// <summary>The last saved state, or null when nothing was saved yet.</summary>
    LedgerState? Load();

    /// <summary>Replaces the saved state.</summary>
    void Save(LedgerState state);

    /// <summary>Adds audit lines: jobs that never reported, rejections, new weeks.</summary>
    void Append(IReadOnlyList<LedgerEvent> events);
}
