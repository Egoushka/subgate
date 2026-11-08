using System.Security.Cryptography;
using System.Text;
using Subgate.Config;

namespace Subgate.Tests.Cli;

public sealed class TokenCommandTests
{
    private static (int ExitCode, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        return (Program.Run(args, output, error), output.ToString(), error.ToString());
    }

    [Fact]
    public void New_prints_a_token_and_the_hash_the_config_takes()
    {
        var (exitCode, output, error) = Run("token", "new", "example-job");

        Assert.Equal(0, exitCode);
        var lines = output.ReplaceLineEndings("\n").TrimEnd().Split('\n');
        Assert.Equal("consumer: example-job", lines[0]);
        var token = lines[1].Replace("token: ", string.Empty, StringComparison.Ordinal);
        var hash = lines[2].Replace("token_sha256: ", string.Empty, StringComparison.Ordinal);
        Assert.Equal(43, token.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", token);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token))), hash);
        Assert.DoesNotContain(token, error);

        var other = Run("token", "new", "example-signal").Output.ReplaceLineEndings("\n").Split('\n')[2].Replace("token_sha256: ", string.Empty, StringComparison.Ordinal);
        var config = ConfigLoader.Parse($"""
            owner: you
            signal:
              token_sha256: "{other}"
            consumers:
              - name: you
                share: 40
                token_sha256: "{hash}"
            """);
        Assert.Equal(2, config.Tokens.Count);
    }

    [Fact]
    public void Two_tokens_differ()
    {
        var first = Run("token", "new", "example-job").Output;
        var second = Run("token", "new", "example-job").Output;

        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData("token")]
    [InlineData("token new")]
    [InlineData("token new bad/name")]
    [InlineData("token old example-job")]
    [InlineData("token new a b")]
    public void Anything_else_prints_usage_and_exits_64(string line)
    {
        var (exitCode, output, _) = Run(line.Split(' '));

        Assert.Equal(64, exitCode);
        Assert.Contains("usage:", output);
    }
}
