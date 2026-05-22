using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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
            return Deserializer.Deserialize<ThemeTokens>(yaml);
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

        return child.Merge(parent);
    }
}
