using YamlDotNet.RepresentationModel;

namespace Bukit.Cli.Intent;

public static class IntentLoader
{
    public static SiteIntent Load(string intentPath)
    {
        if (!File.Exists(intentPath))
        {
            throw new InvalidOperationException($"Intent not found: {intentPath}");
        }

        var yaml = File.ReadAllText(intentPath);
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new InvalidOperationException("Invalid intent YAML: root must be a mapping.");
        }

        var siteNode = GetMapping(root, "site");
        var contentNode = GetMapping(root, "content");
        var themeNode = GetMapping(root, "theme");

        var languagesNode = GetOptionalMapping(root, "languages");
        var featuresNode = GetOptionalMapping(root, "features");
        var deploymentNode = GetOptionalMapping(root, "deployment");

        return new SiteIntent
        {
            Site = new SiteIntentSite
            {
                Name = GetRequiredString(siteNode, "name"),
                Title = GetRequiredString(siteNode, "title"),
                BaseUrl = GetOptionalString(siteNode, "base_url") ?? "/",
                Url = GetOptionalString(siteNode, "url"),
                Type = GetOptionalString(siteNode, "type"),
                Language = GetOptionalString(siteNode, "language")
            },
            Languages = languagesNode is null
                ? null
                : new SiteIntentLanguages
                {
                    Default = GetRequiredString(languagesNode, "default"),
                    Supported = ReadStringList(languagesNode, "supported")
                },
            Content = ReadContent(contentNode),
            Theme = new SiteIntentTheme
            {
                Name = GetRequiredString(themeNode, "name"),
                Params = ReadObjectMap(GetOptionalMapping(themeNode, "params"))
            },
            Features = featuresNode is null
                ? null
                : new SiteIntentFeatures
                {
                    Sitemap = GetOptionalBool(featuresNode, "sitemap"),
                    Rss = GetOptionalBool(featuresNode, "rss"),
                    Search = GetOptionalBool(featuresNode, "search")
                },
            Deployment = deploymentNode is null
                ? null
                : new SiteIntentDeployment
                {
                    Target = GetOptionalString(deploymentNode, "target")
                }
        };
    }

    private static SiteIntentContent ReadContent(YamlMappingNode node)
    {
        var provider = GetRequiredString(node, "provider");
        var normalized = provider.Trim().ToLowerInvariant();

        if (normalized == "markdown")
        {
            var md = GetOptionalMapping(node, "markdown");
            return new SiteIntentContent
            {
                Provider = "markdown",
                Markdown = new SiteIntentMarkdownContent
                {
                    Dir = GetOptionalString(md, "dir") ?? "content"
                }
            };
        }

        if (normalized == "notion")
        {
            var notion = GetMapping(node, "notion");
            var fpNode = GetOptionalMapping(notion, "field_policy");
            return new SiteIntentContent
            {
                Provider = "notion",
                Notion = new SiteIntentNotionContent
                {
                    DatabaseId = GetRequiredString(notion, "database_id"),
                    FieldPolicy = fpNode is null
                        ? new SiteIntentNotionFieldPolicy()
                        : new SiteIntentNotionFieldPolicy
                        {
                            Mode = GetOptionalString(fpNode, "mode") ?? "whitelist",
                            Allowed = ReadOptionalStringList(fpNode, "allowed")
                        }
                }
            };
        }

        return new SiteIntentContent
        {
            Provider = provider
        };
    }

    private static YamlMappingNode GetMapping(YamlMappingNode node, string key)
    {
        var result = GetOptionalMapping(node, key);
        if (result is null)
        {
            throw new InvalidOperationException($"{key} section is required.");
        }

        return result;
    }

    private static YamlMappingNode? GetOptionalMapping(YamlMappingNode? node, string key)
    {
        if (node is null)
        {
            return null;
        }

        if (!node.Children.TryGetValue(new YamlScalarNode(key), out var child))
        {
            return null;
        }

        return child as YamlMappingNode;
    }

    private static string GetRequiredString(YamlMappingNode node, string key)
    {
        var value = GetOptionalString(node, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} is required.");
        }

        return value.Trim();
    }

    private static string? GetOptionalString(YamlMappingNode? node, string key)
    {
        if (node is null)
        {
            return null;
        }

        if (!node.Children.TryGetValue(new YamlScalarNode(key), out var child))
        {
            return null;
        }

        return child is YamlScalarNode s ? s.Value : child?.ToString();
    }

    private static bool? GetOptionalBool(YamlMappingNode node, string key)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode(key), out var child))
        {
            return null;
        }

        if (child is YamlScalarNode s && bool.TryParse(s.Value, out var b))
        {
            return b;
        }

        return null;
    }

    private static IReadOnlyList<string> ReadStringList(YamlMappingNode node, string key)
    {
        var list = ReadOptionalStringList(node, key);
        if (list is null || list.Count == 0)
        {
            throw new InvalidOperationException($"{key} must be a non-empty list.");
        }

        return list;
    }

    private static IReadOnlyList<string>? ReadOptionalStringList(YamlMappingNode node, string key)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode(key), out var child) || child is not YamlSequenceNode seq)
        {
            return null;
        }

        var list = new List<string>();
        foreach (var n in seq.Children)
        {
            if (n is YamlScalarNode s && !string.IsNullOrWhiteSpace(s.Value))
            {
                list.Add(s.Value.Trim());
            }
        }

        return list.Count == 0 ? null : list;
    }

    private static IReadOnlyDictionary<string, object>? ReadObjectMap(YamlMappingNode? node)
    {
        if (node is null)
        {
            return null;
        }

        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in node.Children)
        {
            if (kv.Key is not YamlScalarNode k || string.IsNullOrWhiteSpace(k.Value))
            {
                continue;
            }

            dict[k.Value] = ToObject(kv.Value);
        }

        return dict.Count == 0 ? null : dict;
    }

    private static object ToObject(YamlNode node)
    {
        return node switch
        {
            YamlScalarNode s => s.Value ?? string.Empty,
            YamlSequenceNode seq => seq.Children.Select(ToObject).ToList(),
            YamlMappingNode map => map.Children
                .Where(p => p.Key is YamlScalarNode ks && !string.IsNullOrWhiteSpace(ks.Value))
                .ToDictionary(
                    p => ((YamlScalarNode)p.Key).Value!,
                    p => ToObject(p.Value),
                    StringComparer.OrdinalIgnoreCase),
            _ => node.ToString()
        };
    }
}

