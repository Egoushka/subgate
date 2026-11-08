using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Subgate.Api;
using Subgate.Config;
using Subgate.Store;
using Subgate.Tests.Store;

namespace Subgate.Tests.Api;

/// <summary>The real host on an in-memory server, with a fake clock, a captured log and a store the test can inspect.</summary>
internal sealed class ApiHost : IAsyncDisposable
{
    public const string OwnerToken = "test-owner-token-aaaaaaaa";
    public const string JobToken = "test-job-token-bbbbbbbbbb";
    public const string AutomationToken = "test-automation-token-cccccc";
    public const string SignalToken = "test-signal-token-dddddddd";

    public static readonly DateTimeOffset Epoch = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly WebApplication _app;

    private ApiHost(WebApplication app, PersistentLedger ledger, FakeClock clock, CapturedLogs logs)
    {
        _app = app;
        Ledger = ledger;
        Clock = clock;
        Logs = logs;
        Client = app.GetTestClient();
    }

    public HttpClient Client { get; }

    public PersistentLedger Ledger { get; }

    public FakeClock Clock { get; }

    public CapturedLogs Logs { get; }

    public static async Task<ApiHost> Start(ILedgerStore? store = null, string? storeNote = null)
    {
        var config = ConfigLoader.Parse($"""
            owner: you
            signal:
              token_sha256: "{Hash(SignalToken)}"
            consumers:
              - name: you
                share: 40
                token_sha256: "{Hash(OwnerToken)}"
              - name: job
                share: 45
                action: api
                token_sha256: "{Hash(JobToken)}"
              - name: automation
                share: 10
                action: hold
                token_sha256: "{Hash(AutomationToken)}"
            """);
        var clock = new FakeClock(Epoch);
        var ledger = new PersistentLedger(config.Policy, store ?? new MemoryLedgerStore(), clock);
        var logs = new CapturedLogs();
        var app = SubgateHost.Create(config, ledger, clock, storeNote, builder =>
        {
            builder.WebHost.UseTestServer();
            builder.Logging.AddProvider(logs);
            builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Trace);
            builder.Logging.SetMinimumLevel(LogLevel.Trace);
        });
        await app.StartAsync();
        return new ApiHost(app, ledger, clock, logs);
    }

    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public Task<HttpResponseMessage> Post(string path, string json, string? token = null, params (string Name, string Value)[] headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        if (token is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
        }

        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        return Client.SendAsync(request);
    }

    public async Task<JsonElement> Get(string path)
    {
        using var response = await Client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    public static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        Ledger.Dispose();
    }
}

internal sealed class CapturedLogs : ILoggerProvider
{
    private readonly List<string> _lines = [];

    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (_lines)
            {
                return [.. _lines];
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new Capture(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class Capture(CapturedLogs owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var line = $"{category}: {formatter(state, exception)}{exception}";
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
            {
                line += " " + string.Join(' ', values.Select(v => $"{v.Key}={v.Value}"));
            }

            lock (owner._lines)
            {
                owner._lines.Add(line);
            }
        }
    }
}
