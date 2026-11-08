using Subgate.Store;
using Switchknife.Pace;

namespace Subgate.Tests.Store;

public sealed class SqliteLedgerStoreTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("subgate-store-");
    private string DbPath => Path.Combine(_dir.FullName, "state", "subgate.db");

    private static Policy Policy() => new()
    {
        Owner = "owner",
        Consumers = new Dictionary<string, ConsumerPolicy>
        {
            ["owner"] = new(0.40),
            ["job"] = new(0.45, Decision.Hold),
        },
    };

    public void Dispose() => _dir.Delete(recursive: true);

    [Fact]
    public void Counters_open_leases_and_the_last_meter_survive_a_restart()
    {
        var clock = new FakeClock(Start);
        LedgerStatus before;
        string leaseId;

        using (var first = new PersistentLedger(Policy(), new SqliteLedgerStore(DbPath), clock))
        {
            first.Observe(new MeterReading(MeterWindow.SevenDay, 0.10, Start.AddDays(5)));
            clock.Advance(TimeSpan.FromHours(10));
            first.Observe(new MeterReading(MeterWindow.FiveHour, 0.30, clock.GetUtcNow().AddHours(2)));
            first.Report(new UsageReport("job", new Dictionary<string, double> { ["model-a"] = 2.5 }));
            first.Report(new UsageReport("owner", new Dictionary<string, double> { ["model-a"] = 1.0 }, SessionId: "s1", Cumulative: true));
            first.Observe(new MeterReading(MeterWindow.SevenDay, 0.12, Start.AddDays(5)));
            leaseId = first.Admit(new AdmitRequest("job", 0.75)).LeaseId!;
            before = first.Status();
        }

        using var second = new PersistentLedger(Policy(), new SqliteLedgerStore(DbPath), clock);
        var after = second.Status();

        Assert.NotNull(leaseId);
        Assert.Equal(before.Account, after.Account);
        Assert.Equal(before.Consumers.Keys, after.Consumers.Keys);
        foreach (var (name, status) in before.Consumers)
        {
            Assert.Equal(status, after.Consumers[name]);
        }

        Assert.Equal(1, after.Consumers["job"].OpenLeases);
        Assert.Equal(0.12, after.Account.Utilization);
        Assert.True(second.Snapshot().Leases.ContainsKey(leaseId));
        Assert.Equal(0.75, second.Snapshot().Leases[leaseId].EstimateUsd);
        Assert.Equal(1.0, second.Snapshot().Sessions["s1"]);
    }

    [Fact]
    public void A_restored_lease_still_closes_with_its_report()
    {
        var clock = new FakeClock(Start);
        string leaseId;
        using (var first = new PersistentLedger(Policy(), new SqliteLedgerStore(DbPath), clock))
        {
            leaseId = first.Admit(new AdmitRequest("job", 1)).LeaseId!;
        }

        using var second = new PersistentLedger(Policy(), new SqliteLedgerStore(DbPath), clock);
        var result = second.Report(new UsageReport("job", new Dictionary<string, double> { ["model-a"] = 0.5 }, LeaseId: leaseId));

        Assert.True(result.LeaseClosed);
        Assert.Equal(0, second.Status().Consumers["job"].OpenLeases);
    }

    [Fact]
    public void An_empty_file_starts_an_empty_ledger()
    {
        using var ledger = new PersistentLedger(Policy(), new SqliteLedgerStore(DbPath), new FakeClock(Start));

        var status = ledger.Status();

        Assert.Null(status.Account.Utilization);
        Assert.Equal(PaceState.Ok, status.Account.State);
    }

    [Fact]
    public void An_expired_lease_is_written_to_the_audit_table_with_its_consumer()
    {
        var clock = new FakeClock(Start);
        using (var ledger = new PersistentLedger(Policy(), new SqliteLedgerStore(DbPath), clock))
        {
            ledger.Admit(new AdmitRequest("job", 1));
            clock.Advance(TimeSpan.FromHours(3));
            ledger.Status();
        }

        using var db = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={DbPath};Pooling=False");
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT kind, consumer FROM events";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("lease_expired", reader.GetString(0));
        Assert.Equal("job", reader.GetString(1));
    }

    [Fact]
    public void A_file_from_a_newer_schema_is_refused()
    {
        using (var store = new SqliteLedgerStore(DbPath))
        {
        }

        using (var db = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={DbPath};Pooling=False"))
        {
            db.Open();
            using var command = db.CreateCommand();
            command.CommandText = "PRAGMA user_version = 99";
            command.ExecuteNonQuery();
        }

        var e = Assert.Throws<InvalidOperationException>(() => new SqliteLedgerStore(DbPath));
        Assert.Contains("schema 99", e.Message);
    }

    [Fact]
    public void The_memory_store_round_trips_for_tests_that_need_no_file()
    {
        var clock = new FakeClock(Start);
        var store = new MemoryLedgerStore();
        using (var first = new PersistentLedger(Policy(), store, clock))
        {
            first.Report(new UsageReport("job", new Dictionary<string, double> { ["model-a"] = 2 }));
        }

        using var second = new PersistentLedger(Policy(), store, clock);

        Assert.Equal(2, second.Status().Consumers["job"].WeightedUsd);
    }
}
