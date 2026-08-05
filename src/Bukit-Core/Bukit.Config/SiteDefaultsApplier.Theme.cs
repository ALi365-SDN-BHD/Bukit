using Bukit.Shared;
using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

internal static partial class SiteDefaultsApplier
{
    internal static IReadOnlyDictionary<string, ComponentDefinition>? ReadComponents(YamlMappingNode? themeNode)
    {
        if (themeNode is null)
        {
            return null;
        }

        var componentsNode = ConfigYamlHelpers.GetOptionalMapping(themeNode, "components");
        if (componentsNode is null)
        {
            return null;
        }

        var dict = new Dictionary<string, ComponentDefinition>(StringComparer.OrdinalIgnoreCase);
        var componentIndex = 0;
        foreach (var kv in componentsNode.Children)
        {
            var componentName = ConfigYamlHelpers.GetRequiredMapKey(
                kv.Key,
                "theme.components",
                componentIndex);

            if (kv.Value is not YamlMappingNode compNode)
            {
                throw ConfigYamlHelpers.NodeKindMismatch(
                    $"theme.components.{componentName}",
                    "mapping",
                    kv.Value);
            }

            var componentPath = $"theme.components.{componentName}";
            var template = ConfigYamlHelpers.GetOptionalString(compNode, "template", componentPath);
            if (string.IsNullOrWhiteSpace(template))
            {
                componentIndex++;
                continue;
            }

            var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var propsNode = ConfigYamlHelpers.GetOptionalMapping(compNode, "props", componentPath);
            if (propsNode is not null)
            {
                var propertyIndex = 0;
                foreach (var pkv in propsNode.Children)
                {
                    var propertyName = ConfigYamlHelpers.GetRequiredMapKey(
                        pkv.Key,
                        $"{componentPath}.props",
                        propertyIndex);

                    if (pkv.Value is not YamlScalarNode pv)
                    {
                        throw ConfigYamlHelpers.NodeKindMismatch(
                            $"{componentPath}.props.{propertyName}",
                            "scalar",
                            pkv.Value);
                    }

                    props[propertyName] = pv.Value ?? string.Empty;
                    propertyIndex++;
                }
            }

            dict[componentName] = new ComponentDefinition { Template = template, Props = props };
            componentIndex++;
        }

        return dict.Count == 0 ? null : dict;
    }

    internal static ScssConfig? ReadScssConfig(YamlMappingNode? themeNode)
    {
        if (themeNode is null)
        {
            return null;
        }

        var scssNode = ConfigYamlHelpers.GetOptionalMapping(themeNode, "scss");
        if (scssNode is null)
        {
            return null;
        }

        return new ScssConfig
        {
            Enabled = ConfigYamlHelpers.GetOptionalBool(scssNode, "enabled") ?? false,
            EntryPoint = ConfigYamlHelpers.GetOptionalString(scssNode, "entryPoint"),
            OutputDir = ConfigYamlHelpers.GetOptionalString(scssNode, "outputDir") ?? "assets"
        };
    }

    internal static ImageOptimizationConfig? ReadImageOptimizationConfig(YamlMappingNode? themeNode)
    {
        if (themeNode is null)
        {
            return null;
        }

        var imagesNode = ConfigYamlHelpers.GetOptionalMapping(themeNode, "images");
        if (imagesNode is null)
        {
            return null;
        }

        return new ImageOptimizationConfig
        {
            Enabled = ConfigYamlHelpers.GetOptionalBool(imagesNode, "enabled") ?? false,
            Formats = ConfigYamlHelpers.ReadStringList(imagesNode, "formats") ?? new[] { "webp" },
            Sizes = ConfigYamlHelpers.ReadIntList(imagesNode, "sizes", "theme.images")
                ?? new[] { 480, 768, 1200 },
            Quality = ConfigYamlHelpers.GetOptionalInt(imagesNode, "quality") ?? 80
        };
    }

    internal static IReadOnlyDictionary<string, object>? ReadThemeParams(YamlMappingNode? themeNode)
    {
        if (themeNode is null)
        {
            return null;
        }

        var paramsNode = ConfigYamlHelpers.GetOptionalMapping(themeNode, "params");
        if (paramsNode is null)
        {
            return null;
        }

        return ConfigYamlHelpers.ReadObjectMap(paramsNode, "theme.params")
            ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    internal static IReadOnlyDictionary<string, PluginToggleConfig>? ReadPluginToggles(YamlMappingNode siteNode)
    {
        var pluginsNode = ConfigYamlHelpers.GetOptionalMapping(siteNode, "plugins");
        if (pluginsNode is null)
        {
            return null;
        }

        var plugins = new Dictionary<string, PluginToggleConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in pluginsNode.Children)
        {
            if (kv.Key is not YamlScalarNode keyNode)
            {
                continue;
            }

            var name = (keyNode.Value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var enabled = true;
            if (kv.Value is YamlScalarNode scalar)
            {
                var s = (scalar.Value ?? string.Empty).Trim();
                if (bool.TryParse(s, out var b))
                {
                    enabled = b;
                }
            }
            else if (kv.Value is YamlMappingNode m)
            {
                enabled = ConfigYamlHelpers.GetOptionalBool(m, "enabled") ?? true;
                IReadOnlyDictionary<string, object>? options = null;
                if (m.Children.TryGetValue(new YamlScalarNode("options"), out var optionsRaw))
                {
                    if (optionsRaw is not YamlMappingNode optionsNode)
                    {
                        throw new ConfigException($"site.plugins.{name}.options must be a mapping.", DiagnosticCode.ConfigRequiredFieldMissing);
                    }

                    options = ConfigYamlHelpers.ReadObjectMap(
                        optionsNode,
                        $"site.plugins.{name}.options");
                }

                plugins[name] = new PluginToggleConfig
                {
                    Enabled = enabled,
                    Options = options
                };
                continue;
            }
            else
            {
                throw new ConfigException($"site.plugins.{name} must be a mapping or boolean.", DiagnosticCode.ConfigRequiredFieldMissing);
            }

            plugins[name] = new PluginToggleConfig { Enabled = enabled };
        }

        return plugins;
    }

    internal static IReadOnlyList<LlmsTxtOptionalLink>? ReadLlmsTxtOptionalLinks(YamlMappingNode geoNode)
    {
        var seq = ConfigYamlHelpers.GetOptionalSequence(geoNode, "llmsTxtOptionalLinks");
        if (seq is null)
        {
            return null;
        }

        var links = new List<LlmsTxtOptionalLink>();
        foreach (var n in seq.Children)
        {
            if (n is not YamlMappingNode m)
            {
                throw new ConfigException("site.seo.geo.llmsTxtOptionalLinks items must be mappings.", DiagnosticCode.ConfigRequiredFieldMissing);
            }

            var title = ConfigYamlHelpers.GetRequiredString(m, "title");
            var url = ConfigYamlHelpers.GetRequiredString(m, "url");
            var description = ConfigYamlHelpers.GetOptionalString(m, "description");
            links.Add(new LlmsTxtOptionalLink { Title = title, Url = url, Description = description });
        }

        return links.Count == 0 ? null : links;
    }
}
