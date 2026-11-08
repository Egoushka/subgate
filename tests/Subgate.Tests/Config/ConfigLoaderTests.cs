using System.Security.Cryptography;
using System.Text;
using Subgate.Config;
using Switchknife.Pace;

namespace Subgate.Tests.Config;

public class ConfigLoaderTests
{
    private static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Valid(string extra = "") => $"""
        owner: you
        {extra}
        consumers:
          - name: you
            share: 40
            token_sha256: "{Hash("t-you")}"
          - name: job
            share: 45
            action: hold
            on_stale: hold
            token_sha256: "{Hash("t-job")}"
        signal:
          token_sha256: "{Hash("t-signal")}"
        """;

    private static IReadOnlyList<string> Problems(string yaml) =>
        Assert.Throws<ConfigException>(() => ConfigLoader.Parse(yaml)).Problems;

    [Fact]
    public void A_valid_file_becomes_a_policy_and_the_tokens()
    {
        var config = ConfigLoader.Parse(Valid("""
            listen: 0.0.0.0:9000
            state: /data/subgate.db
            stale_after: 3h
            quiet_hours: { start: "01:00", end: "07:30", timezone: "UTC+02:00" }
            model_weights: { model-a: 1.5 }
            """));

        Assert.Equal("0.0.0.0:9000", config.Listen);
        Assert.Equal("/data/subgate.db", config.StatePath);
        Assert.Equal("you", config.Policy.Owner);
        Assert.Equal(0.40, config.Policy.Consumers["you"].Share, 9);
        Assert.Equal(0.45, config.Policy.Consumers["job"].Share, 9);
        Assert.Equal(Decision.Hold, config.Policy.Consumers["job"].Action);
        Assert.Equal(TimeSpan.FromHours(3), config.Policy.StaleAfter);
        Assert.Equal(new TimeOnly(7, 30), config.Policy.Quiet.End);
        Assert.Equal(1.5, config.Policy.ModelWeights["model-a"]);
        Assert.Equal(PaceMode.Observe, config.Policy.Mode);
        Assert.Equal(
            [("you", TokenKind.Consumer), ("job", TokenKind.Consumer), ("signal", TokenKind.Signal)],
            config.Tokens.Select(t => (t.Name, t.Kind)));
        Assert.Equal(Hash("t-signal"), Convert.ToHexStringLower(config.Tokens[2].Sha256));
    }

    [Fact]
    public void Defaults_listen_on_loopback()
    {
        var config = ConfigLoader.Parse(Valid());

        Assert.Equal("127.0.0.1:8070", config.Listen);
        Assert.Equal(TimeSpan.FromHours(6), config.Policy.StaleAfter);
        Assert.Equal(TimeSpan.FromHours(2), config.Policy.LeaseTtl);
    }

    [Fact]
    public void Shares_that_sum_past_100_percent_fail_with_the_sum()
    {
        var yaml = Valid().Replace("share: 45", "share: 65");

        Assert.Contains(Problems(yaml), p => p.Contains("shares sum to 105%, past 100%"));
    }

    [Fact]
    public void An_unknown_action_fails_and_names_it()
    {
        var yaml = Valid().Replace("action: hold", "action: nuke");

        Assert.Contains(Problems(yaml), p => p.Contains("consumer 'job': action 'nuke' must be hold, api or cheap"));
    }

    [Fact]
    public void A_duplicate_token_hash_fails_and_names_both_consumers()
    {
        var yaml = Valid().Replace(Hash("t-job"), Hash("t-you"));

        Assert.Contains(Problems(yaml), p => p.Contains("'you' and 'job' have the same token_sha256"));
    }

    [Fact]
    public void A_signal_token_that_repeats_a_consumers_fails()
    {
        var yaml = Valid().Replace(Hash("t-signal"), Hash("t-job"));

        Assert.Contains(Problems(yaml), p => p.Contains("'job' and 'signal' have the same token_sha256"));
    }

    [Fact]
    public void An_owner_with_an_action_fails()
    {
        var yaml = Valid().Replace("share: 40", "share: 40\n    action: hold");

        Assert.Contains(Problems(yaml), p => p.Contains("'you' is the owner, who is never gated"));
    }

    [Fact]
    public void An_owner_that_is_not_a_consumer_fails()
    {
        var yaml = Valid().Replace("owner: you", "owner: nobody");

        Assert.Contains(Problems(yaml), p => p.Contains("owner 'nobody' is not in consumers"));
    }

    [Fact]
    public void Enforce_mode_is_refused_in_v0_1()
    {
        Assert.Contains(Problems(Valid("mode: enforce")), p => p.Contains("v0.1 only has observe"));
    }

    [Fact]
    public void An_unknown_setting_fails_so_a_typo_does_not_pass_silently()
    {
        Assert.Contains(Problems(Valid("stale_afterr: 3h")), p => p.Contains("stale_afterr is not a known setting"));
    }

    [Fact]
    public void A_hash_that_is_not_64_hex_characters_fails()
    {
        var yaml = Valid().Replace(Hash("t-job"), "abc123");

        Assert.Contains(Problems(yaml), p => p.Contains("consumer 'job': token_sha256 must be 64 hex characters"));
    }

    [Fact]
    public void Every_problem_is_reported_together()
    {
        var yaml = Valid("mode: enforce").Replace("action: hold", "action: nuke").Replace("share: 45", "share: 65");

        Assert.True(Problems(yaml).Count >= 3);
    }

    [Fact]
    public void Text_that_is_not_a_mapping_fails()
    {
        Assert.Contains(Problems("- just\n- a list"), p => p.Contains("one YAML mapping"));
    }

    [Fact]
    public void A_missing_file_fails_with_the_path()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "config.yaml");

        Assert.Contains(Assert.Throws<ConfigException>(() => ConfigLoader.Load(path)).Problems, p => p.Contains(path));
    }
}
