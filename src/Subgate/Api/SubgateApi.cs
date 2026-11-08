using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Subgate.Config;
using Subgate.Store;
using Switchknife.Pace;

namespace Subgate.Api;

/// <summary>The web host for <c>subgate serve</c>: the ledger behind the control API (ADR 0007).</summary>
public static class SubgateHost
{
    /// <param name="config">The validated config.</param>
    /// <param name="ledger">The ledger and its store; the host disposes it when it stops.</param>
    /// <param name="clock">The same clock the ledger runs on.</param>
    /// <param name="storeNote">Set when the state file could not be used, so <c>/v1/status</c> says so (ADR 0008).</param>
    /// <param name="configure">Lets a test swap the server.</param>
    public static WebApplication Create(
        SubgateConfig config, PersistentLedger ledger, TimeProvider? clock = null, string? storeNote = null,
        Action<WebApplicationBuilder>? configure = null)
    {
        clock ??= TimeProvider.System;
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://{config.Listen}");
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = CredentialGuard.MaxBodyBytes;
        });
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        configure?.Invoke(builder);

        var app = builder.Build();
        app.Lifetime.ApplicationStopped.Register(ledger.Dispose);
        app.Use(CredentialGuard.Invoke);
        new ApiEndpoints(config, ledger, clock, storeNote).Map(app);
        return app;
    }
}

internal sealed class ApiEndpoints(SubgateConfig config, PersistentLedger ledger, TimeProvider clock, string? storeNote)
{
    private const int MaxIdLength = 128;
    private const int MaxWindows = 8;
    private const long MaxUnixSeconds = 253402300799;

    private readonly TokenAuth _auth = new(config.Tokens);
    private readonly Metrics _metrics = new();

    public void Map(WebApplication app)
    {
        app.MapGet("/healthz", () => Results.Text("ok\n"));
        app.MapGet("/v1/status", Status);
        app.MapGet("/metrics", MetricsText);
        app.MapPost("/v1/admit", (HttpContext context) => Admit(context));
        app.MapPost("/v1/usage", (HttpContext context) => Usage(context));
        app.MapPost("/v1/signal", (HttpContext context) => Signal(context));
    }

    private IResult Admit(HttpContext context)
    {
        if (_auth.Authenticate(context.Request) is not { } entry)
        {
            return Denied(context);
        }

        if (entry.Kind == TokenKind.Signal)
        {
            return Error(StatusCodes.Status403Forbidden, "a signal token posts signals only");
        }

        if (!TryRead(context, ApiJsonContext.Default.AdmitBody, out var body))
        {
            return Error(StatusCodes.Status400BadRequest, "body must be a JSON object");
        }

        if (!IsId(body.Consumer))
        {
            return Error(StatusCodes.Status400BadRequest, "consumer is required");
        }

        if (entry.Name != body.Consumer)
        {
            return Error(StatusCodes.Status403Forbidden, "the token belongs to another consumer");
        }

        if (body.Model is not null && !IsId(body.Model))
        {
            return Error(StatusCodes.Status400BadRequest, "model is not valid");
        }

        if (body.EstimateUsd is { } estimate && (!double.IsFinite(estimate) || estimate < 0))
        {
            return Error(StatusCodes.Status400BadRequest, "estimate_usd must be 0 or more");
        }

        Admission admission;
        try
        {
            admission = ledger.Admit(new AdmitRequest(body.Consumer, body.EstimateUsd));
        }
        catch (UnknownConsumerException)
        {
            return Error(StatusCodes.Status404NotFound, "unknown consumer");
        }

        _metrics.CountAdmit(body.Consumer, ApiNames.Of(admission.Decision), ApiNames.Of(admission.Would));
        var status = admission.Status;
        var resetAt = ledger.Snapshot().Week?.ResetsAt?.ToUnixTimeSeconds();
        return Results.Json(
            new AdmitResponse(
                ApiNames.Of(admission.Decision), ApiNames.Of(admission.Would), admission.LeaseId,
                (long)admission.LeaseTtl.TotalSeconds, status.Share, status.Used, status.Allowance, status.Remaining,
                ApiNames.Of(status.State), resetAt, admission.RetryAfter is { } retry ? (long)Math.Ceiling(retry.TotalSeconds) : null),
            ApiJsonContext.Default.AdmitResponse);
    }

    private IResult Usage(HttpContext context)
    {
        if (_auth.Authenticate(context.Request) is not { } entry)
        {
            return Denied(context);
        }

        if (entry.Kind == TokenKind.Signal)
        {
            return Error(StatusCodes.Status403Forbidden, "a signal token posts signals only");
        }

        if (!TryRead(context, ApiJsonContext.Default.UsageBody, out var body))
        {
            return Error(StatusCodes.Status400BadRequest, "body must be a JSON object");
        }

        if (!IsId(body.Consumer))
        {
            return Error(StatusCodes.Status400BadRequest, "consumer is required");
        }

        if (entry.Name != body.Consumer)
        {
            return Error(StatusCodes.Status403Forbidden, "the token belongs to another consumer");
        }

        if ((body.LeaseId is not null && !IsId(body.LeaseId)) || (body.SessionId is not null && !IsId(body.SessionId)))
        {
            return Error(StatusCodes.Status400BadRequest, "lease_id and session_id must be short plain ids");
        }

        UsageRoute route;
        switch (body.Route)
        {
            case null or "plan":
                route = UsageRoute.Plan;
                break;
            case "api":
                route = UsageRoute.Api;
                break;
            default:
                return Error(StatusCodes.Status400BadRequest, "route must be plan or api");
        }

        var costs = new Dictionary<string, double>();
        if (body.Models is { Count: > 0 })
        {
            foreach (var (model, usage) in body.Models)
            {
                if (!IsId(model) || usage?.CostUsd is not { } modelCost || !double.IsFinite(modelCost) || modelCost < 0)
                {
                    return Error(StatusCodes.Status400BadRequest, "each model needs a plain name and a cost_usd of 0 or more");
                }

                costs[model] = modelCost;
            }
        }
        else if (body.CostUsd is { } cost && double.IsFinite(cost) && cost >= 0)
        {
            costs["total"] = cost;
        }
        else
        {
            return Error(StatusCodes.Status400BadRequest, "send models, or cost_usd of 0 or more");
        }

        UsageResult result;
        try
        {
            result = ledger.Report(new UsageReport(body.Consumer, costs, body.LeaseId, body.SessionId, body.Cumulative ?? false, route));
        }
        catch (UnknownConsumerException)
        {
            return Error(StatusCodes.Status404NotFound, "unknown consumer");
        }

        return Results.Json(new UsageResponse(result.WeightedUsd, result.LeaseClosed), ApiJsonContext.Default.UsageResponse);
    }

    private IResult Signal(HttpContext context)
    {
        if (_auth.Authenticate(context.Request) is null)
        {
            return Denied(context);
        }

        if (!TryRead(context, ApiJsonContext.Default.SignalBody, out var body))
        {
            return Error(StatusCodes.Status400BadRequest, "body must be a JSON object");
        }

        if (!IsId(body.Source))
        {
            return Error(StatusCodes.Status400BadRequest, "source is required");
        }

        if (body.Windows is not { Count: > 0 and <= MaxWindows } windows)
        {
            return Error(StatusCodes.Status400BadRequest, $"windows must list 1 to {MaxWindows} readings");
        }

        var readings = new List<MeterReading>(windows.Count);
        foreach (var window in windows)
        {
            var reading = ToReading(window);
            if (reading is null)
            {
                return Error(StatusCodes.Status400BadRequest,
                    "each window needs kind (5h, 7d or 7d_scoped with a scope), utilization from 0 to 1 and resets_at in Unix seconds");
            }

            readings.Add(reading);
        }

        foreach (var reading in readings)
        {
            ledger.Observe(reading);
        }

        var now = clock.GetUtcNow();
        _metrics.CountSignal(body.Source, body.SentAt is { } sentAt ? now.ToUnixTimeSeconds() - sentAt : null);
        return Results.NoContent();
    }

    private IResult Status()
    {
        var status = ledger.Status();
        var state = ledger.Snapshot();
        var now = clock.GetUtcNow();

        var windows = new Dictionary<string, WindowStatus>();
        foreach (var reading in state.Readings.Values.OrderBy(ApiNames.Key, StringComparer.Ordinal))
        {
            windows[ApiNames.Key(reading)] = new WindowStatus(
                reading.Utilization, reading.ResetsAt.ToUnixTimeSeconds(), (reading.ObservedAt ?? now).ToUnixTimeSeconds(), reading.Rejected);
        }

        var account = status.Account;
        var consumers = new Dictionary<string, ConsumerStatusBody>();
        foreach (var (name, c) in status.Consumers.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            consumers[name] = new ConsumerStatusBody(
                c.Share, c.Used, c.Reserved, c.Allowance, c.Remaining, ApiNames.Of(c.State), c.OpenLeases, c.WeightedUsd, c.ApiUsd,
                ApiNames.Of(c.Estimator), c.Apportioned, c.Calibrated);
        }

        var errors = ledger.StoreErrors;
        return Results.Json(
            new StatusResponse(
                ApiNames.Of(config.Policy.Mode),
                new StoreStatus(storeNote is null && errors == 0, errors, storeNote),
                windows,
                new AccountStatusBody(
                    ApiNames.Of(account.State), account.Utilization, account.ResetsAt?.ToUnixTimeSeconds(), account.Elapsed,
                    account.PaceLine, account.Untracked, account.MeterAge is { } age ? (long)age.TotalSeconds : null,
                    account.LimitedUntil?.ToUnixTimeSeconds(), account.K),
                consumers),
            ApiJsonContext.Default.StatusResponse);
    }

    private IResult MetricsText()
    {
        var text = _metrics.Render(ledger.Status(), ledger.Snapshot(), ledger.StoreErrors, clock.GetUtcNow());
        return Results.Text(text, "text/plain; version=0.0.4; charset=utf-8");
    }

    private static IResult Denied(HttpContext context)
    {
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return Error(StatusCodes.Status401Unauthorized, "a valid bearer token is required");
    }

    private static IResult Error(int status, string message) =>
        Results.Json(new ErrorResponse(message), ApiJsonContext.Default.ErrorResponse, statusCode: status);

    private static bool TryRead<T>(HttpContext context, JsonTypeInfo<T> info, out T body)
        where T : class
    {
        try
        {
            body = JsonSerializer.Deserialize(CredentialGuard.Body(context), info)!;
            return body is not null;
        }
        catch (JsonException)
        {
            body = null!;
            return false;
        }
    }

    /// <summary>A short plain id: printable ASCII, no spaces, and never a provider credential in disguise.</summary>
    private static bool IsId([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? value) =>
        value is { Length: > 0 and <= MaxIdLength }
        && value.All(c => c is > ' ' and < '\u007f')
        && !CredentialGuard.Contains(value);

    private static MeterReading? ToReading(SignalWindowBody window)
    {
        if (window.Utilization is not { } utilization || !double.IsFinite(utilization) || utilization < 0 || utilization > 1
            || window.ResetsAt is not { } resetsAt || resetsAt <= 0 || resetsAt > MaxUnixSeconds)
        {
            return null;
        }

        MeterWindow kind;
        switch (window.Kind)
        {
            case "5h":
                kind = MeterWindow.FiveHour;
                break;
            case "7d":
                kind = MeterWindow.SevenDay;
                break;
            case "7d_scoped":
                kind = MeterWindow.SevenDayScoped;
                break;
            default:
                return null;
        }

        if ((kind == MeterWindow.SevenDayScoped) != (window.Scope is not null) || (window.Scope is not null && !IsId(window.Scope)))
        {
            return null;
        }

        return new MeterReading(
            kind, utilization, DateTimeOffset.FromUnixTimeSeconds(resetsAt), window.Status == "rejected", Scope: window.Scope);
    }
}
