using Subgate.Api;
using Subgate.Config;
using Subgate.Store;

namespace Subgate.Cli;

/// <summary><c>subgate serve --config &lt;file&gt;</c>: run the ledger behind the control API until stopped.</summary>
internal static class ServeCommand
{
    private const int ExitUsage = 64;
    private const int ExitConfig = 78;

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args is not ["--config", { Length: > 0 } path])
        {
            output.WriteLine("usage: subgate serve --config <file>");
            return ExitUsage;
        }

        SubgateConfig config;
        try
        {
            config = ConfigLoader.Load(path);
        }
        catch (ConfigException e)
        {
            foreach (var problem in e.Problems)
            {
                error.WriteLine($"subgate: {problem}");
            }

            return ExitConfig;
        }

        var ledger = OpenLedger(config, error, out var note);
        SubgateHost.Create(config, ledger, TimeProvider.System, note).Run();
        return 0;
    }

    /// <summary>
    /// The SQLite state when it opens, else memory with a note that <c>/v1/status</c> repeats: a broken state file
    /// must not stop the owner's jobs (ADR 0008).
    /// </summary>
    private static PersistentLedger OpenLedger(SubgateConfig config, TextWriter error, out string? note)
    {
        SqliteLedgerStore? store = null;
        try
        {
            store = new SqliteLedgerStore(config.StatePath);
            note = null;
            return new PersistentLedger(config.Policy, store);
        }
        catch (Exception e)
        {
            store?.Dispose();
            error.WriteLine($"subgate: the state file cannot be used ({e.GetType().Name}); counters start empty and are not kept");
            note = "the state file could not be used; counters are in memory and reset on restart";
            return new PersistentLedger(config.Policy, new MemoryLedgerStore());
        }
    }
}
