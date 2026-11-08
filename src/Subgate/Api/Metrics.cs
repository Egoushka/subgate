using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Switchknife.Pace;

namespace Subgate.Api;

/// <summary>The Prometheus text for <c>/metrics</c>. A value subgate does not know yet is <c>NaN</c>, so the series exists from the first scrape.</summary>
internal sealed class Metrics
{
    private const int MaxSources = 64;

    private readonly ConcurrentDictionary<(string Consumer, string Decision, string Would), long> _admits = new();
    private readonly ConcurrentDictionary<string, long> _signals = new();
    private readonly ConcurrentDictionary<string, double> _clockOffsets = new();

    public void CountAdmit(string consumer, string decision, string would) =>
        _admits.AddOrUpdate((consumer, decision, would), 1, (_, n) => n + 1);

    /// <param name="source">Who sent the signal.</param>
    /// <param name="clockOffsetSeconds">Receipt time minus the reporter's <c>sent_at</c>, when it sent one.</param>
    public void CountSignal(string source, double? clockOffsetSeconds)
    {
        if (!_signals.ContainsKey(source) && _signals.Count >= MaxSources)
        {
            source = "other";
        }

        _signals.AddOrUpdate(source, 1, (_, n) => n + 1);
        if (clockOffsetSeconds is { } offset)
        {
            _clockOffsets[source] = offset;
        }
    }

    public string Render(LedgerStatus status, LedgerState state, long storeErrors, DateTimeOffset now)
    {
        var text = new StringBuilder();
        var account = status.Account;

        Family(text, "subgate_utilization", "gauge", "The plan's meter, 0 to 1, by window.");
        var readings = state.Readings.Values.ToList();
        foreach (var window in new[] { MeterWindow.FiveHour, MeterWindow.SevenDay })
        {
            var reading = readings.FirstOrDefault(r => r.Window == window);
            Series(text, "subgate_utilization", $"window=\"{ApiNames.Window(window)}\"", reading?.Utilization ?? double.NaN);
        }

        foreach (var scoped in readings.Where(r => r.Window == MeterWindow.SevenDayScoped).OrderBy(r => r.Scope, StringComparer.Ordinal))
        {
            Series(text, "subgate_utilization", $"window=\"7d_scoped\",scope=\"{Escape(scoped.Scope ?? "")}\"", scoped.Utilization);
        }

        Family(text, "subgate_reset_seconds", "gauge", "Seconds until the weekly meter resets.");
        Series(text, "subgate_reset_seconds", null,
            account.ResetsAt is { } resets ? Math.Max(0, (resets - now).TotalSeconds) : double.NaN);

        Family(text, "subgate_signal_age_seconds", "gauge", "Age of the newest meter reading.");
        Series(text, "subgate_signal_age_seconds", null, account.MeterAge?.TotalSeconds ?? double.NaN);

        Family(text, "subgate_k", "gauge", "Meter rise per dollar of reported use, fitted in quiet hours.");
        Series(text, "subgate_k", null, account.K ?? double.NaN);

        var consumers = status.Consumers.Values.OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
        Family(text, "subgate_share_used", "gauge", "A consumer's attributed use this week, as a fraction of the weekly meter.");
        foreach (var c in consumers)
        {
            Series(text, "subgate_share_used", Consumer(c.Name), c.Used);
        }

        Family(text, "subgate_share_allowance", "gauge", "A consumer's pace line this week, as a fraction of the weekly meter.");
        foreach (var c in consumers)
        {
            Series(text, "subgate_share_allowance", Consumer(c.Name), c.Allowance);
        }

        Family(text, "subgate_leases_open", "gauge", "Admitted jobs not yet reported or expired.");
        foreach (var c in consumers)
        {
            Series(text, "subgate_leases_open", Consumer(c.Name), c.OpenLeases);
        }

        Family(text, "subgate_admit_total", "counter", "Admit answers: the decision given, and what enforce mode would have given.");
        var admits = new SortedDictionary<(string, string, string), long>();
        foreach (var c in consumers)
        {
            admits[(c.Name, "allow", "allow")] = 0;
        }

        foreach (var (key, count) in _admits)
        {
            admits[key] = count;
        }

        foreach (var ((consumer, decision, would), count) in admits)
        {
            Series(text, "subgate_admit_total", $"consumer=\"{Escape(consumer)}\",decision=\"{decision}\",would=\"{would}\"", count);
        }

        Family(text, "subgate_signal_source_total", "counter", "Signals received, by source.");
        foreach (var (source, count) in _signals.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            Series(text, "subgate_signal_source_total", $"source=\"{Escape(source)}\"", count);
        }

        Family(text, "subgate_clock_offset_seconds", "gauge", "Receipt time minus the reporter's own send time, by source.");
        foreach (var (source, offset) in _clockOffsets.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            Series(text, "subgate_clock_offset_seconds", $"source=\"{Escape(source)}\"", offset);
        }

        Family(text, "subgate_store_errors_total", "counter", "Writes to the state file that failed since start.");
        Series(text, "subgate_store_errors_total", null, storeErrors);

        return text.ToString();
    }

    private static string Consumer(string name) => $"consumer=\"{Escape(name)}\"";

    private static void Family(StringBuilder text, string name, string type, string help) =>
        text.Append("# HELP ").Append(name).Append(' ').Append(help).Append('\n')
            .Append("# TYPE ").Append(name).Append(' ').Append(type).Append('\n');

    private static void Series(StringBuilder text, string name, string? labels, double value)
    {
        text.Append(name);
        if (labels is not null)
        {
            text.Append('{').Append(labels).Append('}');
        }

        text.Append(' ').Append(Number(value)).Append('\n');
    }

    private static string Number(double value) =>
        double.IsNaN(value) ? "NaN"
        : double.IsPositiveInfinity(value) ? "+Inf"
        : double.IsNegativeInfinity(value) ? "-Inf"
        : value.ToString("R", CultureInfo.InvariantCulture);

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
}
