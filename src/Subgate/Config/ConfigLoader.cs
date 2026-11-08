using System.Globalization;
using Switchknife.Pace;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Subgate.Config;

/// <summary>Reads the YAML config (ADR 0009) and refuses to start on anything it does not understand.</summary>
public static class ConfigLoader
{
    public const string DefaultListen = "127.0.0.1:8070";
    public const string DefaultStatePath = "subgate.db";

    public static SubgateConfig Load(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new ConfigException([$"cannot read {path}: {e.Message}"]);
        }

        return Parse(text);
    }

    public static SubgateConfig Parse(string yaml)
    {
        var problems = new List<string>();
        var root = ReadRoot(yaml, problems);
        if (root is null)
        {
            throw new ConfigException(problems);
        }

        var top = new Section(root, "", problems);
        var listen = top.String("listen") ?? DefaultListen;
        var statePath = top.String("state") ?? DefaultStatePath;
        var owner = top.String("owner") ?? "owner";

        if (!IsHostAndPort(listen))
        {
            problems.Add($"listen '{listen}' must be host:port");
        }

        if (top.String("mode") is { } mode && mode != "observe")
        {
            problems.Add($"mode '{mode}' is not available: v0.1 only has observe");
        }

        var staleAfter = top.Duration("stale_after") ?? TimeSpan.FromHours(6);
        var leaseTtl = top.Duration("lease_ttl") ?? TimeSpan.FromHours(2);
        var quiet = ReadQuietHours(top.Child("quiet_hours"), problems);
        var weights = ReadModelWeights(top.Child("model_weights"), problems);

        var tokens = new List<TokenEntry>();
        var consumers = new Dictionary<string, ConsumerPolicy>();
        foreach (var item in top.Items("consumers"))
        {
            var consumer = ReadConsumer(item, consumers, tokens, problems);
            if (consumer is { } c)
            {
                consumers[c.Name] = c.Policy;
            }
        }

        if (top.Child("signal") is { } signal)
        {
            if (ReadHash(signal, "signal", problems) is { } hash)
            {
                tokens.Add(new TokenEntry("signal", TokenKind.Signal, hash));
            }
            else if (!signal.Has("token_sha256"))
            {
                problems.Add("signal: token_sha256 is required");
            }

            signal.RejectUnknown();
        }

        top.RejectUnknown();
        CheckShares(consumers, problems);
        CheckDuplicateHashes(tokens, problems);

        if (consumers.Count == 0)
        {
            problems.Add("consumers: list at least one");
        }
        else if (!consumers.ContainsKey(owner))
        {
            problems.Add($"owner '{owner}' is not in consumers: its status line needs a token");
        }

        var policy = new Policy
        {
            Consumers = consumers,
            Owner = owner,
            Mode = PaceMode.Observe,
            StaleAfter = staleAfter,
            LeaseTtl = leaseTtl,
            Quiet = quiet,
            ModelWeights = weights,
        };

        if (problems.Count == 0)
        {
            problems.AddRange(policy.Validate());
        }

        if (problems.Count > 0)
        {
            throw new ConfigException(problems);
        }

        return new SubgateConfig(listen, statePath, policy, tokens);
    }

    private static YamlMappingNode? ReadRoot(string yaml, List<string> problems)
    {
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(yaml));
        }
        catch (Exception e) when (e is YamlException or ArgumentException)
        {
            problems.Add($"not valid YAML: {e.Message}");
            return null;
        }

        if (stream.Documents.Count == 1 && stream.Documents[0].RootNode is YamlMappingNode root)
        {
            return root;
        }

        problems.Add("the file must hold one YAML mapping");
        return null;
    }

    private static (string Name, ConsumerPolicy Policy)? ReadConsumer(
        Section item, Dictionary<string, ConsumerPolicy> seen, List<TokenEntry> tokens, List<string> problems)
    {
        var name = item.String("name");
        if (name is null || !IsName(name))
        {
            problems.Add($"{item.Path}: name is required and uses letters, digits, '.', '_' or '-' (up to 64)");
            item.RejectUnknown();
            return null;
        }

        if (seen.ContainsKey(name))
        {
            problems.Add($"consumer '{name}' is listed twice");
        }

        var label = $"consumer '{name}'";
        var share = item.Number("share");
        if (share is null)
        {
            problems.Add($"{label}: share is required, a percent of the week");
        }
        else if (double.IsNaN(share.Value) || share < 0 || share > 100)
        {
            problems.Add($"{label}: share {share.Value.ToString(CultureInfo.InvariantCulture)} is outside 0 to 100");
        }

        var action = ReadDecision(item, "action", label, problems);
        var onStale = ReadDecision(item, "on_stale", label, problems);

        if (ReadHash(item, label, problems) is { } hash)
        {
            tokens.Add(new TokenEntry(name, TokenKind.Consumer, hash));
        }
        else if (!item.Has("token_sha256"))
        {
            problems.Add($"{label}: token_sha256 is required");
        }

        item.RejectUnknown();
        return (name, new ConsumerPolicy((share ?? 0) / 100, action, onStale));
    }

    private static Decision? ReadDecision(Section item, string key, string label, List<string> problems)
    {
        var value = item.String(key);
        if (value is null)
        {
            return null;
        }

        switch (value)
        {
            case "hold": return Decision.Hold;
            case "api": return Decision.Api;
            case "cheap": return Decision.Cheap;
            default:
                problems.Add($"{label}: {key} '{value}' must be hold, api or cheap");
                return null;
        }
    }

    private static byte[]? ReadHash(Section section, string label, List<string> problems)
    {
        var value = section.String("token_sha256");
        if (value is null)
        {
            return null;
        }

        if (value.Length != 64 || !value.All(Uri.IsHexDigit))
        {
            problems.Add($"{label}: token_sha256 must be 64 hex characters, the SHA-256 of the token");
            return null;
        }

        return Convert.FromHexString(value);
    }

    private static void CheckShares(Dictionary<string, ConsumerPolicy> consumers, List<string> problems)
    {
        var total = consumers.Values.Sum(c => c.Share) * 100;
        if (total > 100 + 1e-9)
        {
            problems.Add($"shares sum to {total.ToString("0.####", CultureInfo.InvariantCulture)}%, past 100%");
        }
    }

    private static void CheckDuplicateHashes(List<TokenEntry> tokens, List<string> problems)
    {
        var owners = new Dictionary<string, string>();
        foreach (var token in tokens)
        {
            var hex = Convert.ToHexStringLower(token.Sha256);
            if (owners.TryGetValue(hex, out var first))
            {
                problems.Add($"'{first}' and '{token.Name}' have the same token_sha256: a token must name one consumer");
            }
            else
            {
                owners[hex] = token.Name;
            }
        }
    }

    private static QuietHours ReadQuietHours(Section? section, List<string> problems)
    {
        if (section is null)
        {
            return QuietHours.Default;
        }

        var start = section.Clock("start") ?? QuietHours.Default.Start;
        var end = section.Clock("end") ?? QuietHours.Default.End;
        var zone = QuietHours.Default.Zone;
        if (section.String("timezone") is { } name)
        {
            if (TryZone(name, out var found))
            {
                zone = found;
            }
            else
            {
                problems.Add($"quiet_hours: timezone '{name}' is not known here; use an IANA name the host has, or a fixed offset like UTC+02:00");
            }
        }

        section.RejectUnknown();
        return new QuietHours(start, end, zone);
    }

    private static bool TryZone(string name, out TimeZoneInfo zone)
    {
        if (name == "UTC")
        {
            zone = TimeZoneInfo.Utc;
            return true;
        }

        if (name.StartsWith("UTC", StringComparison.Ordinal) && name.Length > 4 && name[3] is '+' or '-'
            && TimeSpan.TryParseExact(name[4..], @"hh\:mm", CultureInfo.InvariantCulture, out var offset)
            && offset <= TimeSpan.FromHours(14))
        {
            zone = TimeZoneInfo.CreateCustomTimeZone(name, name[3] == '-' ? -offset : offset, name, name);
            return true;
        }

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(name);
            return true;
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            zone = TimeZoneInfo.Utc;
            return false;
        }
    }

    private static Dictionary<string, double> ReadModelWeights(Section? section, List<string> problems)
    {
        var weights = new Dictionary<string, double>();
        if (section is null)
        {
            return weights;
        }

        foreach (var key in section.Keys)
        {
            if (section.Number(key) is { } weight)
            {
                weights[key] = weight;
            }
            else
            {
                problems.Add($"model_weights: '{key}' must be a number");
            }
        }

        return weights;
    }

    internal static bool IsName(string name) =>
        name.Length is > 0 and <= 64 && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');

    private static bool IsHostAndPort(string listen)
    {
        var colon = listen.LastIndexOf(':');
        return colon > 0 && int.TryParse(listen[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            && port is > 0 and <= 65535;
    }

    /// <summary>One YAML mapping, read key by key so that what is left over can be reported.</summary>
    private sealed class Section
    {
        private readonly YamlMappingNode _node;
        private readonly List<string> _problems;
        private readonly HashSet<string> _read = [];

        public Section(YamlMappingNode node, string path, List<string> problems)
        {
            _node = node;
            Path = path;
            _problems = problems;
        }

        public string Path { get; }

        public IEnumerable<string> Keys => _node.Children.Keys.OfType<YamlScalarNode>().Select(k => k.Value ?? "");

        public bool Has(string key) => _node.Children.ContainsKey(key);

        public string? String(string key)
        {
            if (!TryGet(key, out var node))
            {
                return null;
            }

            if (node is YamlScalarNode { Value: { Length: > 0 } value })
            {
                return value;
            }

            _problems.Add($"{Where(key)} must be a plain value");
            return null;
        }

        public double? Number(string key)
        {
            var text = String(key);
            if (text is null)
            {
                return null;
            }

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value))
            {
                return value;
            }

            _problems.Add($"{Where(key)} '{text}' must be a number");
            return null;
        }

        public TimeSpan? Duration(string key)
        {
            var text = String(key);
            if (text is null)
            {
                return null;
            }

            if (text.Length > 1 && int.TryParse(text[..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0)
            {
                switch (text[^1])
                {
                    case 's': return TimeSpan.FromSeconds(n);
                    case 'm': return TimeSpan.FromMinutes(n);
                    case 'h': return TimeSpan.FromHours(n);
                    case 'd': return TimeSpan.FromDays(n);
                }
            }

            _problems.Add($"{Where(key)} '{text}' must be a positive duration such as 30m, 6h or 2d");
            return null;
        }

        public TimeOnly? Clock(string key)
        {
            var text = String(key);
            if (text is null)
            {
                return null;
            }

            if (TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            {
                return time;
            }

            _problems.Add($"{Where(key)} '{text}' must be a time like 08:00");
            return null;
        }

        public Section? Child(string key)
        {
            if (!TryGet(key, out var node))
            {
                return null;
            }

            if (node is YamlMappingNode map)
            {
                return new Section(map, Join(key), _problems);
            }

            _problems.Add($"{Where(key)} must be a mapping");
            return null;
        }

        public IEnumerable<Section> Items(string key)
        {
            if (!TryGet(key, out var node))
            {
                yield break;
            }

            if (node is not YamlSequenceNode list)
            {
                _problems.Add($"{Where(key)} must be a list");
                yield break;
            }

            var index = 0;
            foreach (var entry in list.Children)
            {
                var path = $"{Join(key)}[{index++}]";
                if (entry is YamlMappingNode map)
                {
                    yield return new Section(map, path, _problems);
                }
                else
                {
                    _problems.Add($"{path} must be a mapping");
                }
            }
        }

        public void RejectUnknown()
        {
            foreach (var key in Keys.Where(k => !_read.Contains(k)))
            {
                _problems.Add($"{Where(key)} is not a known setting");
            }
        }

        private bool TryGet(string key, out YamlNode node)
        {
            _read.Add(key);
            return _node.Children.TryGetValue(key, out node!);
        }

        private string Join(string key) => Path.Length == 0 ? key : $"{Path}.{key}";

        private string Where(string key) => Join(key);
    }
}
