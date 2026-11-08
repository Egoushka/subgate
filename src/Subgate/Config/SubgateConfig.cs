using Switchknife.Pace;

namespace Subgate.Config;

/// <summary>What a bearer token is allowed to do (ADR 0006).</summary>
public enum TokenKind
{
    /// <summary>Admit, usage and signal for the named consumer.</summary>
    Consumer,

    /// <summary>Signals only: the sampler's token.</summary>
    Signal,
}

/// <summary>A token the API accepts, known only by its hash.</summary>
/// <param name="Name">The consumer it speaks for, or <c>signal</c>.</param>
/// <param name="Kind">What it may do.</param>
/// <param name="Sha256">The SHA-256 of the token.</param>
public sealed record TokenEntry(string Name, TokenKind Kind, byte[] Sha256);

/// <summary>A validated config file: the pace policy plus the service settings around it.</summary>
/// <param name="Listen">Where the API listens, <c>host:port</c>.</param>
/// <param name="StatePath">The SQLite file.</param>
/// <param name="Policy">The rules the ledger runs by.</param>
/// <param name="Tokens">Every accepted token.</param>
public sealed record SubgateConfig(string Listen, string StatePath, Policy Policy, IReadOnlyList<TokenEntry> Tokens);

/// <summary>The config file cannot run; <see cref="Problems"/> says why, all of it.</summary>
public sealed class ConfigException(IReadOnlyList<string> problems)
    : Exception("invalid config: " + string.Join("; ", problems))
{
    public IReadOnlyList<string> Problems { get; } = problems;
}
