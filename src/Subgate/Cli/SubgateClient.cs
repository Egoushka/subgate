using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Subgate.Api;

namespace Subgate.Cli;

internal enum CallKind
{
    Ok,

    /// <summary>No answer in time, or a server error: try again later.</summary>
    Unavailable,

    /// <summary>subgate answered and said no (a wrong token, an unknown consumer, a bad body): sending it again changes nothing.</summary>
    Rejected,
}

internal readonly record struct Call<T>(CallKind Kind, T? Value = default, string? Message = null)
{
    public bool IsOk => Kind == CallKind.Ok;
}

/// <summary>The control API from the client's side (ADR 0007). Never throws for a network or server problem; says which kind it was.</summary>
internal sealed class SubgateClient : IDisposable
{
    private readonly HttpClient _http;

    public SubgateClient(Uri baseUrl, string token, TimeSpan timeout, HttpMessageHandler? handler)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        _http.BaseAddress = baseUrl;
        _http.Timeout = timeout;
        _http.DefaultRequestHeaders.Authorization = new("Bearer", token);
    }

    public Task<Call<AdmitResponse>> Admit(AdmitBody body) =>
        Post("v1/admit", body, ApiJsonContext.Default.AdmitBody, ApiJsonContext.Default.AdmitResponse);

    public Task<Call<UsageResponse>> Usage(UsageBody body) =>
        Post("v1/usage", body, ApiJsonContext.Default.UsageBody, ApiJsonContext.Default.UsageResponse);

    public async Task<Call<bool>> Signal(SignalBody body)
    {
        var call = await Post<SignalBody, object>("v1/signal", body, ApiJsonContext.Default.SignalBody, responseInfo: null);
        return new Call<bool>(call.Kind, call.IsOk, call.Message);
    }

    public async Task<Call<StatusResponse>> Status()
    {
        try
        {
            using var response = await _http.GetAsync("v1/status");
            return await Read(response, ApiJsonContext.Default.StatusResponse);
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException)
        {
            return new(CallKind.Unavailable, Message: Describe(e));
        }
    }

    public void Dispose() => _http.Dispose();

    private async Task<Call<TResponse>> Post<TBody, TResponse>(
        string path, TBody body, JsonTypeInfo<TBody> bodyInfo, JsonTypeInfo<TResponse>? responseInfo)
    {
        try
        {
            using var content = new StringContent(JsonSerializer.Serialize(body, bodyInfo), Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(path, content);
            return await Read(response, responseInfo);
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException)
        {
            return new(CallKind.Unavailable, Message: Describe(e));
        }
    }

    private static async Task<Call<T>> Read<T>(HttpResponseMessage response, JsonTypeInfo<T>? info)
    {
        var status = (int)response.StatusCode;
        if (status is >= 200 and < 300)
        {
            if (info is null)
            {
                return new(CallKind.Ok);
            }

            try
            {
                return new(CallKind.Ok, JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(), info));
            }
            catch (JsonException)
            {
                return new(CallKind.Unavailable, Message: "the answer is not what subgate sends");
            }
        }

        var message = await ErrorMessage(response);
        return new(status >= 500 ? CallKind.Unavailable : CallKind.Rejected, Message: $"{status} {message}");
    }

    private static async Task<string> ErrorMessage(HttpResponseMessage response)
    {
        try
        {
            var error = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(), ApiJsonContext.Default.ErrorResponse);
            return error?.Error ?? response.ReasonPhrase ?? string.Empty;
        }
        catch (JsonException)
        {
            return response.ReasonPhrase ?? string.Empty;
        }
    }

    private static string Describe(Exception e) =>
        e is OperationCanceledException ? "no answer in time" : "cannot reach subgate";
}
