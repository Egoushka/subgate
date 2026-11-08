using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Switchknife.Pace;

namespace Subgate.Store;

/// <summary>
/// One SQLite file holds the engine's state as a single JSON row, plus an append-only audit table. The state is the
/// engine's own snapshot (Ledger.Snapshot), so a restart continues the week, the open leases and the last meter.
/// </summary>
public sealed class SqliteLedgerStore : ILedgerStore
{
    private const int SchemaVersion = 1;
    private static readonly TimeSpan EventRetention = TimeSpan.FromDays(Ledger.HistoryWeeks * 7);

    private readonly object _gate = new();
    private readonly SqliteConnection _db;

    public SqliteLedgerStore(string path, TimeProvider? clock = null)
    {
        if (Path.GetDirectoryName(Path.GetFullPath(path)) is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
        }

        _db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        _db.Open();
        try
        {
            Initialize((clock ?? TimeProvider.System).GetUtcNow());
        }
        catch
        {
            _db.Dispose();
            throw;
        }
    }

    public LedgerState? Load()
    {
        lock (_gate)
        {
            using var command = _db.CreateCommand();
            command.CommandText = "SELECT json FROM ledger_state WHERE id = 1";
            return command.ExecuteScalar() is string json
                ? JsonSerializer.Deserialize(json, StateJsonContext.Default.LedgerState)
                : null;
        }
    }

    public void Save(LedgerState state)
    {
        var json = JsonSerializer.Serialize(state, StateJsonContext.Default.LedgerState);
        lock (_gate)
        {
            using var command = _db.CreateCommand();
            command.CommandText =
                "INSERT INTO ledger_state (id, json) VALUES (1, $json) ON CONFLICT (id) DO UPDATE SET json = excluded.json";
            command.Parameters.AddWithValue("$json", json);
            command.ExecuteNonQuery();
        }
    }

    public void Append(IReadOnlyList<LedgerEvent> events)
    {
        if (events.Count == 0)
        {
            return;
        }

        lock (_gate)
        {
            using var transaction = _db.BeginTransaction();
            foreach (var e in events)
            {
                using var command = _db.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO events (at, kind, consumer, detail) VALUES ($at, $kind, $consumer, $detail)";
                command.Parameters.AddWithValue("$at", e.At.ToString("O", CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("$kind", e.Kind);
                command.Parameters.AddWithValue("$consumer", (object?)e.Consumer ?? DBNull.Value);
                command.Parameters.AddWithValue("$detail", e.Detail);
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _db.Dispose();
        }
    }

    private void Initialize(DateTimeOffset now)
    {
        Execute("PRAGMA journal_mode = WAL");
        Execute("PRAGMA synchronous = NORMAL");

        using (var command = _db.CreateCommand())
        {
            command.CommandText = "PRAGMA user_version";
            var version = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            if (version > SchemaVersion)
            {
                throw new InvalidOperationException(
                    $"the state file has schema {version}, newer than this subgate understands ({SchemaVersion})");
            }
        }

        Execute("CREATE TABLE IF NOT EXISTS ledger_state (id INTEGER PRIMARY KEY CHECK (id = 1), json TEXT NOT NULL)");
        Execute(
            "CREATE TABLE IF NOT EXISTS events (id INTEGER PRIMARY KEY AUTOINCREMENT, at TEXT NOT NULL, kind TEXT NOT NULL, consumer TEXT, detail TEXT NOT NULL)");
        Execute($"PRAGMA user_version = {SchemaVersion}");

        using var prune = _db.CreateCommand();
        prune.CommandText = "DELETE FROM events WHERE at < $cutoff";
        prune.Parameters.AddWithValue("$cutoff", (now - EventRetention).ToString("O", CultureInfo.InvariantCulture));
        prune.ExecuteNonQuery();
    }

    private void Execute(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
