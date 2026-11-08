using System.Security.Cryptography;
using System.Text;
using Subgate.Config;

namespace Subgate.Api;

/// <summary>Bearer tokens known only by their SHA-256, compared in fixed time (ADR 0006).</summary>
internal sealed class TokenAuth(IReadOnlyList<TokenEntry> tokens)
{
    private const int MaxTokenLength = 256;

    /// <summary>The entry the request's token belongs to, or null for a missing or unknown token.</summary>
    public TokenEntry? Authenticate(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Authorization", out var header) || header.Count != 1 || header[0] is not { } value)
        {
            return null;
        }

        const string scheme = "Bearer ";
        if (!value.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = value.AsSpan(scheme.Length).Trim();
        if (token.IsEmpty || token.Length > MaxTokenLength)
        {
            return null;
        }

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(token.ToString()), hash);

        // No early exit: the time taken does not say which entry matched or how far.
        TokenEntry? match = null;
        foreach (var entry in tokens)
        {
            if (CryptographicOperations.FixedTimeEquals(hash, entry.Sha256))
            {
                match = entry;
            }
        }

        return match;
    }
}
