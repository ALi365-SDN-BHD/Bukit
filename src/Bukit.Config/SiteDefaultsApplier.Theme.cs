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
        foreach (var kv in componentsNode.Children)
        {
            if (kv.Key is not YamlScalarNode k || string.IsNullOrWhiteSpace(k.Value))
            {
                continue;
            }

            if (kv.Value is not YamlMappingNode compNode)
            {
                continue;
            }

            var template = ConfigYamlHelpers.GetOptionalString(compNode, "template");
            if (string.IsNullOrWhiteSpace(template))
            {
                continue;
            }

            var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var propsNode = ConfigYamlHelpers.GetOptionalMapping(compNode, "props");
            if (propsNode is not null)
            {
                foreach (var pkv in propsNode.Children)
                {
                    if (pkv.Key is not YamlScalarNode pk || string.IsNullOrWhiteSpace(pk.Value))
                    {
                        continue;
                    }

                    if (pkv.Value is not YamlScalarNode pv)
                    {
                        continue;
                    }

                    props[pk.Value] = pv.Value ?? string.Empty;
                }
            }

            dict[k.Value] = new ComponentDefinition { Template = template, Props = props };
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
            Sizes = (ConfigYamlHelpers.GetOptionalSequence(imagesNode, "sizes")?.Children
                .OfType<YamlScalarNode>()
                .Select(x => int.TryParse(x.Value, out var v) ? v : (int?)null)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToList() as IReadOnlyList<int>) ?? new[] { 480, 768, 1200 },
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

        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in paramsNode.Children)
        {
            if (kv.Key is not YamlScalarNode k || string.IsNullOrWhiteSpace(k.Value))
            {
                continue;
            }

            dict[k.Value] = ConfigYamlHelpers.ToObject(kv.Value);
        }

        return dict;
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

                    options = ConfigYamlHelpers.ReadObjectMap(optionsNode);
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
