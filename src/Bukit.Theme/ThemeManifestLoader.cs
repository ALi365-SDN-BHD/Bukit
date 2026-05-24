using YamlDotNet.RepresentationModel;

namespace Bukit.Theme;

public static class ThemeManifestLoader
{
    public static ThemeManifestV2? Load(string themeRoot)
    {
        var manifestPath = Path.Combine(themeRoot, "theme.yaml");
        if (!File.Exists(manifestPath)) return null;

        try
        {
            var yaml = File.ReadAllText(manifestPath);
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));
            if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
            {
                return null;
            }

            return ParseManifest(root);
        }
        catch
        {
            return null;
        }
    }

    private static ThemeManifestV2 ParseManifest(YamlMappingNode root)
        => new()
        {
            Name = GetString(root, "name") ?? "",
            DisplayName = GetString(root, "display_name"),
            Version = GetString(root, "version"),
            Engine = GetString(root, "engine"),
            MinEngineVersion = GetString(root, "min_engine_version"),
            Description = GetString(root, "description"),
            Extends = GetString(root, "extends"),
            Capabilities = ParseCapabilities(GetMap(root, "capabilities")),
            Layouts = ParseStringMap(GetMap(root, "layouts")),
            PageTemplates = ParsePageTemplates(GetMap(root, "page_templates")),
            Sections = ParseSections(GetMap(root, "sections")),
            Components = ParseComponents(GetMap(root, "components")),
            Assets = ParseAssets(GetMap(root, "assets")),
            Tokens = GetString(root, "tokens")
        };

    private static ThemeCapabilities ParseCapabilities(YamlMappingNode? map)
        => new()
        {
            I18n = GetBool(map, "i18n"),
            Seo = GetBool(map, "seo"),
            Geo = GetBool(map, "geo"),
            DarkMode = GetBool(map, "dark_mode"),
            Search = GetBool(map, "search"),
            Taxonomy = GetBool(map, "taxonomy")
        };

    private static ThemeAssetsConfig ParseAssets(YamlMappingNode? map)
        => new()
        {
            Css = ParseStringList(GetNode(map, "css")),
            Js = ParseStringList(GetNode(map, "js"))
        };

    private static Dictionary<string, ThemePageTemplateDefinition>? ParsePageTemplates(YamlMappingNode? map)
    {
        if (map is null) return null;

        var result = new Dictionary<string, ThemePageTemplateDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in EnumerateMap(map))
        {
            if (value is not YamlMappingNode templateMap)
            {
                continue;
            }

            result[key] = new ThemePageTemplateDefinition
            {
                Template = GetString(templateMap, "template") ?? "",
                Label = GetString(templateMap, "label"),
                Accepts = ParseAccepts(GetMap(templateMap, "accepts")),
                RequiredFields = ParseStringList(GetNode(templateMap, "required_fields"))
            };
        }

        return result.Count == 0 ? null : result;
    }

    private static ThemePageTemplateAccept? ParseAccepts(YamlMappingNode? map)
        => map is null
            ? null
            : new ThemePageTemplateAccept
            {
                Type = GetString(map, "type"),
                Collection = GetString(map, "collection")
            };

    private static Dictionary<string, ThemeSectionDefinition>? ParseSections(YamlMappingNode? map)
    {
        if (map is null) return null;

        var result = new Dictionary<string, ThemeSectionDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in EnumerateMap(map))
        {
            if (value is not YamlMappingNode sectionMap)
            {
                continue;
            }

            result[key] = new ThemeSectionDefinition
            {
                Template = GetString(sectionMap, "template") ?? "",
                Schema = GetString(sectionMap, "schema"),
                Preview = GetString(sectionMap, "preview"),
                Description = GetString(sectionMap, "description"),
                Variants = ParseVariants(GetMap(sectionMap, "variants")),
                Data = ParseDataBinding(GetMap(sectionMap, "data")),
                Plugin = GetString(sectionMap, "plugin")
            };
        }

        return result.Count == 0 ? null : result;
    }

    private static Dictionary<string, ThemeVariantDefinition>? ParseVariants(YamlMappingNode? map)
    {
        if (map is null) return null;

        var result = new Dictionary<string, ThemeVariantDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in EnumerateMap(map))
        {
            if (value is not YamlMappingNode variantMap)
            {
                continue;
            }

            result[key] = new ThemeVariantDefinition
            {
                Template = GetString(variantMap, "template") ?? "",
                Label = GetString(variantMap, "label"),
                Description = GetString(variantMap, "description")
            };
        }

        return result.Count == 0 ? null : result;
    }

    private static ThemeDataBindingDefinition? ParseDataBinding(YamlMappingNode? map)
        => map is null
            ? null
            : new ThemeDataBindingDefinition
            {
                Source = GetString(map, "source"),
                Mode = GetString(map, "mode"),
                Limit = GetInt(map, "limit"),
                Sort = GetString(map, "sort"),
                Filters = ParseObjectMap(GetMap(map, "filters"))
            };

    private static Dictionary<string, ThemeComponentDefinition>? ParseComponents(YamlMappingNode? map)
    {
        if (map is null) return null;

        var result = new Dictionary<string, ThemeComponentDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in EnumerateMap(map))
        {
            if (value is not YamlMappingNode componentMap)
            {
                continue;
            }

            result[key] = new ThemeComponentDefinition
            {
                Template = GetString(componentMap, "template") ?? "",
                Props = ParseStringMap(GetMap(componentMap, "props"))
            };
        }

        return result.Count == 0 ? null : result;
    }

    private static Dictionary<string, string>? ParseStringMap(YamlMappingNode? map)
    {
        if (map is null) return null;

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in EnumerateMap(map))
        {
            if (value is YamlScalarNode scalar && scalar.Value is not null)
            {
                result[key] = scalar.Value;
            }
        }

        return result.Count == 0 ? null : result;
    }

    private static Dictionary<string, object?>? ParseObjectMap(YamlMappingNode? map)
    {
        if (map is null) return null;

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in EnumerateMap(map))
        {
            result[key] = ConvertNode(value);
        }

        return result.Count == 0 ? null : result;
    }

    private static object? ConvertNode(YamlNode node)
        => node switch
        {
            YamlMappingNode map => ParseObjectMap(map),
            YamlSequenceNode sequence => sequence.Children.Select(ConvertNode).ToList(),
            YamlScalarNode scalar => ConvertScalar(scalar.Value),
            _ => node.ToString()
        };

    private static object? ConvertScalar(string? value)
    {
        if (value is null) return null;
        if (bool.TryParse(value, out var boolean)) return boolean;
        if (int.TryParse(value, out var integer)) return integer;
        return value;
    }

    private static List<string>? ParseStringList(YamlNode? node)
        => node switch
        {
            null => null,
            YamlSequenceNode sequence => sequence.Children
                .OfType<YamlScalarNode>()
                .Select(item => item.Value)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
                .ToList(),
            YamlScalarNode scalar when !string.IsNullOrWhiteSpace(scalar.Value) => [scalar.Value],
            _ => null
        };

    private static IEnumerable<(string Key, YamlNode Value)> EnumerateMap(YamlMappingNode map)
    {
        foreach (var (key, value) in map.Children)
        {
            if (key is YamlScalarNode scalar && !string.IsNullOrWhiteSpace(scalar.Value))
            {
                yield return (scalar.Value, value);
            }
        }
    }

    private static YamlNode? GetNode(YamlMappingNode? map, string key)
        => map is not null && map.Children.TryGetValue(new YamlScalarNode(key), out var node)
            ? node
            : null;

    private static YamlMappingNode? GetMap(YamlMappingNode? map, string key)
        => GetNode(map, key) as YamlMappingNode;

    private static string? GetString(YamlMappingNode? map, string key)
        => GetNode(map, key) is YamlScalarNode scalar ? scalar.Value : null;

    private static bool GetBool(YamlMappingNode? map, string key)
        => bool.TryParse(GetString(map, key), out var value) && value;

    private static int? GetInt(YamlMappingNode? map, string key)
        => int.TryParse(GetString(map, key), out var value) ? value : null;
}
