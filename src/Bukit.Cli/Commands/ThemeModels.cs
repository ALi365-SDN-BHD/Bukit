using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Bukit.Cli.Commands;

public sealed record ThemeManifest
{
    public string? Name { get; init; }
    public string? Version { get; init; }
    public string? Description { get; init; }
    public string? Author { get; init; }
    public string? License { get; init; }
    public string? Homepage { get; init; }
    public string? Thumbnail { get; init; }
    public List<string> Tags { get; init; } = [];
    public string? RequiresBukit { get; init; }
    public List<ThemeParam> Params { get; init; } = [];

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
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
    public string Key { get; init; } = "";
    public string? Label { get; init; }
    public string? Type { get; init; }
    public string? Default { get; init; }
}
