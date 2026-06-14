using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace Bukit.Theme;

public static class ThemeManifestLoader
{
    private static readonly HashSet<string> KnownRootFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "display_name",
        "version",
        "engine",
        "min_engine_version",
        "description",
        "extends",
        "capabilities",
        "layouts",
        "templates",
        "page_templates",
        "sections",
        "components",
        "assets",
        "tokens"
    };

    private static readonly HashSet<string> KnownCapabilitiesFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "i18n",
        "seo",
        "geo",
        "dark_mode",
        "search",
        "taxonomy"
    };

    private static readonly HashSet<string> KnownTemplateDefinitionFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "template",
        "required",
        "label",
        "accepts",
        "required_fields"
    };

    private static readonly HashSet<string> KnownTemplateAcceptFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "type",
        "collection",
        "kind"
    };

    private static readonly HashSet<string> KnownPageTemplateDefinitionFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "template",
        "label",
        "accepts",
        "required_fields"
    };

    private static readonly HashSet<string> KnownPageTemplateAcceptFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "type",
        "collection"
    };

    private static readonly HashSet<string> KnownSectionDefinitionFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "template",
        "schema",
        "preview",
        "description",
        "variants",
        "data",
        "plugin"
    };

    private static readonly HashSet<string> KnownSectionVariantFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "template",
        "label",
        "description"
    };

    private static readonly HashSet<string> KnownDataBindingFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "source",
        "mode",
        "limit",
        "sort",
        "filters"
    };

    private static readonly HashSet<string> KnownComponentDefinitionFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "template",
        "props"
    };

    private static readonly HashSet<string> KnownAssetFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "css",
        "js"
    };

    public static ThemeManifestV2? Load(string themeRoot, bool required = false)
    {
        var manifestPath = Path.Combine(themeRoot, "theme.yaml");
        if (!File.Exists(manifestPath))
        {
            if (required)
            {
                throw new ThemeManifestException($"theme.yaml not found: {manifestPath}");
            }

            return null;
        }

        try
        {
            var yaml = File.ReadAllText(manifestPath);
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));
            if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
            {
                if (required)
                {
                    throw new ThemeManifestException($"theme.yaml is invalid at {manifestPath}: root is not a YAML mapping.");
                }

                return null;
            }

            return ParseManifest(root);
        }
        catch (ThemeManifestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ThemeManifestException($"Failed to load theme.yaml: {manifestPath}", ex);
        }
    }

    private static ThemeManifestV2 ParseManifest(YamlMappingNode root)
    {
        EnsureOnlyKnownFields(root, KnownRootFields, "theme.yaml");

        return new()
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
            Templates = ParseTemplates(GetMap(root, "templates")),
            PageTemplates = ParsePageTemplates(GetMap(root, "page_templates")),
            Sections = ParseSections(GetMap(root, "sections")),
            Components = ParseComponents(GetMap(root, "components")),
            Assets = ParseAssets(GetMap(root, "assets")),
            Tokens = GetString(root, "tokens")
        };
    }

    private static ThemeCapabilities ParseCapabilities(YamlMappingNode? map)
    {
        if (map is null)
        {
            return new();
        }

        EnsureOnlyKnownFields(map, KnownCapabilitiesFields, "theme.yaml.capabilities");

        return new()
        {
            I18n = GetBool(map, "i18n"),
            Seo = GetBool(map, "seo"),
            Geo = GetBool(map, "geo"),
            DarkMode = GetBool(map, "dark_mode"),
            Search = GetBool(map, "search"),
            Taxonomy = GetBool(map, "taxonomy")
        };
    }

    private static ThemeAssetsConfig ParseAssets(YamlMappingNode? map)
    {
        if (map is null)
        {
            return new();
        }

        EnsureOnlyKnownFields(map, KnownAssetFields, "theme.yaml.assets");

        return new()
        {
            Css = ParseStringList(ThemeYaml.GetNode(map, "css")),
            Js = ParseStringList(ThemeYaml.GetNode(map, "js"))
        };
    }

    private static Dictionary<string, ThemePageTemplateDefinition>? ParsePageTemplates(YamlMappingNode? map)
    {
        if (map is null) return null;

        var result = new Dictionary<string, ThemePageTemplateDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in ThemeYaml.EnumerateMap(map))
        {
            if (value is not YamlMappingNode templateMap)
            {
                continue;
            }

            var templatePath = $"theme.yaml.page_templates.{key}";
            EnsureOnlyKnownFields(templateMap, KnownPageTemplateDefinitionFields, templatePath);

            result[key] = new ThemePageTemplateDefinition
            {
                Template = GetString(templateMap, "template") ?? "",
                Label = GetString(templateMap, "label"),
                Accepts = ParseAccepts(GetMap(templateMap, "accepts"), $"{templatePath}.accepts"),
                RequiredFields = ParseStringList(ThemeYaml.GetNode(templateMap, "required_fields"))
            };
        }

        return result.Count == 0 ? null : result;
    }

    private static Dictionary<string, ThemeTemplateDefinition>? ParseTemplates(YamlMappingNode? map)
    {
        if (map is null) return null;

        var result = new Dictionary<string, ThemeTemplateDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in ThemeYaml.EnumerateMap(map))
        {
            if (value is not YamlMappingNode templateMap)
            {
                continue;
            }

            var templatePath = $"theme.yaml.templates.{key}";
            EnsureOnlyKnownFields(templateMap, KnownTemplateDefinitionFields, templatePath);

            result[key] = new ThemeTemplateDefinition
            {
                Template = GetString(templateMap, "template") ?? "",
                Required = GetBool(templateMap, "required"),
                Label = GetString(templateMap, "label"),
                Accepts = ParseTemplateAccept(GetMap(templateMap, "accepts"), $"{templatePath}.accepts"),
                RequiredFields = ParseStringList(ThemeYaml.GetNode(templateMap, "required_fields"))
            };
        }

        return result.Count == 0 ? null : result;
    }

    private static ThemeTemplateAccept? ParseTemplateAccept(YamlMappingNode? map, string path)
    {
        if (map is null)
        {
            return null;
        }

        EnsureOnlyKnownFields(map, KnownTemplateAcceptFields, path);
        return new ThemeTemplateAccept
        {
            Type = GetString(map, "type"),
            Collection = GetString(map, "collection"),
            Kind = GetString(map, "kind")
        };
    }

    private static ThemePageTemplateAccept? ParseAccepts(YamlMappingNode? map, string path)
    {
        if (map is null)
        {
            return null;
        }

        EnsureOnlyKnownFields(map, KnownPageTemplateAcceptFields, path);
        return new ThemePageTemplateAccept
        {
            Type = GetString(map, "type"),
            Collection = GetString(map, "collection")
        };
    }

    private static Dictionary<string, ThemeSectionDefinition>? ParseSections(YamlMappingNode? map)
    {
        if (map is null) return null;

        var result = new Dictionary<string, ThemeSectionDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in ThemeYaml.EnumerateMap(map))
        {
            if (value is not YamlMappingNode sectionMap)
            {
                continue;
            }

            var sectionPath = $"theme.yaml.sections.{key}";
            EnsureOnlyKnownFields(sectionMap, KnownSectionDefinitionFields, sectionPath);

            result[key] = new ThemeSectionDefinition
            {
                Template = GetString(sectionMap, "template") ?? "",
                Schema = GetString(sectionMap, "schema"),
                Preview = GetString(sectionMap, "preview"),
                Description = GetString(sectionMap, "description"),
                Variants = ParseVariants(GetMap(sectionMap, "variants"), sectionPath),
                Data = ParseDataBinding(GetMap(sectionMap, "data"), $"{sectionPath}.data"),
                Plugin = GetString(sectionMap, "plugin")
            };
        }

        return result.Count == 0 ? null : result;
    }

    private static Dictionary<string, ThemeVariantDefinition>? ParseVariants(YamlMappingNode? map, string sectionPath)
    {
        if (map is null)
        {
            return null;
        }

        var result = new Dictionary<string, ThemeVariantDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in ThemeYaml.EnumerateMap(map))
        {
            if (value is not YamlMappingNode variantMap)
            {
                continue;
            }

            var variantPath = $"{sectionPath}.variants.{key}";
            EnsureOnlyKnownFields(variantMap, KnownSectionVariantFields, variantPath);

            result[key] = new ThemeVariantDefinition
            {
                Template = GetString(variantMap, "template") ?? "",
                Label = GetString(variantMap, "label"),
                Description = GetString(variantMap, "description")
            };
        }

        return result.Count == 0 ? null : result;
    }

    private static Dictionary<string, ThemeComponentDefinition>? ParseComponents(YamlMappingNode? map)
    {
        if (map is null) return null;

        var result = new Dictionary<string, ThemeComponentDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in ThemeYaml.EnumerateMap(map))
        {
            if (value is not YamlMappingNode componentMap)
            {
                continue;
            }

            EnsureOnlyKnownFields(componentMap, KnownComponentDefinitionFields, $"theme.yaml.components.{key}");

            result[key] = new ThemeComponentDefinition
            {
                Template = GetString(componentMap, "template") ?? "",
                Props = ParseStringMap(GetMap(componentMap, "props"))
            };
        }

        return result.Count == 0 ? null : result;
    }

    private static ThemeDataBindingDefinition? ParseDataBinding(YamlMappingNode? map, string path)
    {
        if (map is null)
        {
            return null;
        }

        EnsureOnlyKnownFields(map, KnownDataBindingFields, path);
        return new ThemeDataBindingDefinition
        {
            Source = GetString(map, "source"),
            Mode = GetString(map, "mode"),
            Limit = GetInt(map, "limit"),
            Sort = GetString(map, "sort"),
            Filters = ParseObjectMap(GetMap(map, "filters"))
        };
    }

    private static Dictionary<string, string>? ParseStringMap(YamlMappingNode? map)
    {
        if (map is null) return null;

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in ThemeYaml.EnumerateMap(map))
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
        foreach (var (key, value) in ThemeYaml.EnumerateMap(map))
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

    private static YamlMappingNode? GetMap(YamlMappingNode? map, string key)
        => ThemeYaml.GetNode(map, key) as YamlMappingNode;

    private static string? GetString(YamlMappingNode? map, string key)
        => ThemeYaml.GetNode(map, key) is YamlScalarNode scalar ? scalar.Value : null;

    private static bool GetBool(YamlMappingNode? map, string key)
        => bool.TryParse(GetString(map, key), out var value) && value;

    private static int? GetInt(YamlMappingNode? map, string key)
        => int.TryParse(GetString(map, key), out var value) ? value : null;

    private static void EnsureOnlyKnownFields(YamlMappingNode? map, HashSet<string> allowedFields, string path)
    {
        if (map is null) return;

        foreach (var (key, _) in ThemeYaml.EnumerateMap(map))
        {
            if (!allowedFields.Contains(key))
            {
                throw new ThemeManifestException($"theme.yaml: unknown field '{path}.{key}'.");
            }
        }
    }
}

public sealed class ThemeManifestException : Exception
{
    public ThemeManifestException(string message)
        : base(message)
    {
    }

    public ThemeManifestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
