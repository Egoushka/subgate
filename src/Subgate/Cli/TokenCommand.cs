using System.Security.Cryptography;
using System.Text;
using Subgate.Config;

namespace Subgate.Cli;

/// <summary><c>subgate token new &lt;name&gt;</c> (ADR 0006): a random bearer token and the hash that goes in the config.</summary>
internal static class TokenCommand
{
    private const int ExitUsage = 64;

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args is not ["new", { } name] || !ConfigLoader.IsName(name))
        {
            output.WriteLine(Program.Usage);
            return ExitUsage;
        }

        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        output.WriteLine($"consumer: {name}");
        output.WriteLine($"token: {token}");
        output.WriteLine($"token_sha256: {hash}");
        error.WriteLine("subgate: give the token to the consumer; only token_sha256 goes in the config file");
        return 0;
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
