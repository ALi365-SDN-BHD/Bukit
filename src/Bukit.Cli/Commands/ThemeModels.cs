using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Bukit.Cli.Commands;

public sealed record ThemeManifest
{
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Description { get; set; }
    public string? Author { get; set; }
    public string? License { get; set; }
    public string? Homepage { get; set; }
    public string? Thumbnail { get; set; }
    public List<string> Tags { get; set; } = [];
    public string? RequiresBukit { get; set; }
    public List<ThemeParam> Params { get; set; } = [];

    private static readonly IDeserializer Deserializer = new StaticDeserializerBuilder(new ThemeYamlStaticContext())
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static ThemeManifest? Load(string themeRoot)
    {
        var manifestPath = Path.Combine(themeRoot, "theme.yaml");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var yaml = File.ReadAllText(manifestPath);
            return Deserializer.Deserialize<ThemeManifest>(yaml);
        }
        catch
        {
            return null;
        }
    }

    public int DeclaredParamCount => Params?.Count ?? 0;
}

public sealed record ThemeParam
{
    public string Key { get; set; } = "";
    public string? Label { get; set; }
    public string? Type { get; set; }
    public string? Default { get; set; }
}

public sealed record RegistryDownload
{
    public string Url { get; set; } = "";
    public string? Sha256 { get; set; }
}

public sealed record RegistryThemeEntry
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string? Description { get; set; }
    public string? Author { get; set; }
    public string? License { get; set; }
    public string? Homepage { get; set; }
    public string? Thumbnail { get; set; }
    public List<string> Tags { get; set; } = [];
    public string? RequiresBukit { get; set; }
    public List<ThemeParam> Params { get; set; } = [];
    public RegistryDownload? Download { get; set; }
}

public sealed record RegistryMeta
{
    public string? Updated { get; set; }
    public string? BukitMinVersion { get; set; }
}

public sealed record RegistryIndex
{
    public RegistryMeta? Registry { get; set; }
    public List<RegistryThemeEntry> Themes { get; set; } = [];

    private static readonly IDeserializer IndexDeserializer = new StaticDeserializerBuilder(new ThemeYamlStaticContext())
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static RegistryIndex? Parse(string yaml)
    {
        try
        {
            return IndexDeserializer.Deserialize<RegistryIndex>(yaml);
        }
        catch
        {
            return null;
        }
    }
}

[YamlStaticContext]
[YamlSerializable(typeof(ThemeManifest))]
[YamlSerializable(typeof(ThemeParam))]
[YamlSerializable(typeof(RegistryIndex))]
[YamlSerializable(typeof(RegistryMeta))]
[YamlSerializable(typeof(RegistryThemeEntry))]
[YamlSerializable(typeof(RegistryDownload))]
public partial class ThemeYamlStaticContext : YamlDotNet.Serialization.StaticContext
{
}
