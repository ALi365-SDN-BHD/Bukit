using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.RepresentationModel;

namespace Bukit.Theme;

public class ThemeTokensLoader
{
    [YamlStaticContext]
    [YamlSerializable(typeof(ThemeTokens))]
    private partial class TokensYamlStaticContext : YamlDotNet.Serialization.StaticContext
    {
    }

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public ThemeTokens? Load(string themeRoot, string? tokensPath = null)
    {
        tokensPath ??= Path.Combine(themeRoot, "tokens.yaml");
        if (!File.Exists(tokensPath)) return null;

        try
        {
            var yaml = File.ReadAllText(tokensPath);
            ThemeTokens? tokens;
            try
            {
                tokens = Deserializer.Deserialize<ThemeTokens>(yaml);
            }
            catch
            {
                tokens = new ThemeTokens();
            }

            return MergeFlattened(tokens, yaml);
        }
        catch
        {
            return null;
        }
    }

    public ThemeTokens? LoadWithInheritance(string themeRoot, string? parentThemeRoot)
    {
        var child = Load(themeRoot);
        if (parentThemeRoot is null) return child;

        var parent = new ThemeTokensLoader().Load(parentThemeRoot);
        if (parent is null) return child;
        if (child is null) return parent;

        return child.DeepMerge(parent);
    }

    private static ThemeTokens MergeFlattened(ThemeTokens? tokens, string yaml)
    {
        tokens ??= new ThemeTokens();
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            return tokens;
        }

        tokens.Colors = MergeGroup(tokens.Colors, root, "colors");
        tokens.Font = MergeGroup(tokens.Font, root, "font");
        tokens.Radius = MergeGroup(tokens.Radius, root, "radius");
        tokens.Spacing = MergeGroup(tokens.Spacing, root, "spacing");
        tokens.Layout = MergeGroup(tokens.Layout, root, "layout");
        return tokens;
    }

    private static Dictionary<string, string>? MergeGroup(Dictionary<string, string>? existing, YamlMappingNode root, string key)
    {
        if (!root.Children.TryGetValue(new YamlScalarNode(key), out var node) || node is not YamlMappingNode map)
        {
            return existing;
        }

        var flattened = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Flatten(map, prefix: null, flattened);
        if (flattened.Count == 0)
        {
            return existing;
        }

        var merged = existing is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(existing, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in flattened)
        {
            merged[kv.Key] = kv.Value;
        }

        return merged;
    }

    private static void Flatten(YamlMappingNode map, string? prefix, Dictionary<string, string> output)
    {
        foreach (var kv in map.Children)
        {
            if (kv.Key is not YamlScalarNode keyNode || string.IsNullOrWhiteSpace(keyNode.Value))
            {
                continue;
            }

            var key = string.IsNullOrWhiteSpace(prefix) ? keyNode.Value.Trim() : prefix + "." + keyNode.Value.Trim();
            if (kv.Value is YamlScalarNode scalar && scalar.Value is not null)
            {
                output[key] = scalar.Value;
            }
            else if (kv.Value is YamlMappingNode nested)
            {
                Flatten(nested, key, output);
            }
        }
    }
}
