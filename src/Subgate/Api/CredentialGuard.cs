using System.Text;

namespace Subgate.Api;

/// <summary>
/// Refuses any request that carries a provider credential (<c>sk-ant-</c>) in a header, the query or the body, before it
/// reaches a handler, so nothing of it is stored or logged (ADR 0003, ADR 0006). It never echoes what it found.
/// </summary>
internal static class CredentialGuard
{
    public const long MaxBodyBytes = 64 * 1024;

    private const string Marker = "sk-ant-";
    private static readonly byte[] MarkerBytes = Encoding.ASCII.GetBytes(Marker);
    private static readonly object BodyKey = new();

    public static bool Contains(string? text) =>
        text is not null && text.Contains(Marker, StringComparison.OrdinalIgnoreCase);

    /// <summary>The body the guard read, for the handler to parse.</summary>
    public static byte[] Body(HttpContext context) => context.Items[BodyKey] as byte[] ?? [];

    public static async Task Invoke(HttpContext context, RequestDelegate next)
    {
        var request = context.Request;
        if (HasCredential(request))
        {
            await Refuse(context, StatusCodes.Status400BadRequest, "request carries a provider credential; subgate never accepts one");
            return;
        }

        var body = await ReadBody(request.Body, context.RequestAborted);
        if (body is null)
        {
            await Refuse(context, StatusCodes.Status413PayloadTooLarge, "request body is too large");
            return;
        }

        if (ContainsAscii(body))
        {
            await Refuse(context, StatusCodes.Status400BadRequest, "request carries a provider credential; subgate never accepts one");
            return;
        }

        context.Items[BodyKey] = body;
        await next(context);
    }

    public static Task Refuse(HttpContext context, int status, string message)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ErrorResponse(message), ApiJsonContext.Default.ErrorResponse);
    }

    private static bool HasCredential(HttpRequest request)
    {
        foreach (var (name, values) in request.Headers)
        {
            if (Contains(name))
            {
                return true;
            }

            foreach (var value in values)
            {
                if (Contains(value))
                {
                    return true;
                }
            }
        }

        var query = request.QueryString.Value;
        if (Contains(query))
        {
            return true;
        }

        try
        {
            return Contains(Uri.UnescapeDataString(query ?? ""));
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static async Task<byte[]?> ReadBody(Stream stream, CancellationToken cancellation)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellation)) > 0)
        {
            if (buffer.Length + read > MaxBodyBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static bool ContainsAscii(ReadOnlySpan<byte> body)
    {
        for (var start = 0; start + MarkerBytes.Length <= body.Length; start++)
        {
            var matched = 0;
            while (matched < MarkerBytes.Length && Lower(body[start + matched]) == MarkerBytes[matched])
            {
                matched++;
            }

            if (matched == MarkerBytes.Length)
            {
                return true;
            }
        }

        return false;
    }

    private static byte Lower(byte b) => b is >= (byte)'A' and <= (byte)'Z' ? (byte)(b + 32) : b;
}
