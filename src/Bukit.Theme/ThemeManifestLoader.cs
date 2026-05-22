using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Bukit.Theme;

public static class ThemeManifestLoader
{
    private static IDeserializer? _deserializer;

    private static IDeserializer GetDeserializer()
    {
        if (_deserializer is not null) return _deserializer;

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        return _deserializer;
    }

    public static ThemeManifestV2? Load(string themeRoot)
    {
        var manifestPath = Path.Combine(themeRoot, "theme.yaml");
        if (!File.Exists(manifestPath)) return null;

        try
        {
            var yaml = File.ReadAllText(manifestPath);
            return GetDeserializer().Deserialize<ThemeManifestV2>(yaml);
        }
        catch
        {
            return null;
        }
    }
}
