using System.Globalization;
using Bukit.Shared;
using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

public static class ConfigLoader
{
    public static AppConfig Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ConfigException("Config path is required.");
        }

        if (!File.Exists(path))
        {
            throw new ConfigException($"Config file not found: {path}");
        }

        using var reader = File.OpenText(path);
        var yaml = new YamlStream();
        yaml.Load(reader);

        if (yaml.Documents.Count == 0)
        {
            throw new ConfigException("Config file is empty.");
        }

        if (yaml.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new ConfigException("Config root must be a mapping.");
        }

        var siteNode = GetMapping(root, "site");
        var contentNode = GetMapping(root, "content");
        var buildNode = GetOptionalMapping(root, "build");
        var themeNode = GetOptionalMapping(root, "theme");
        var taxonomyNode = GetOptionalMapping(root, "taxonomy");
        var loggingNode = GetOptionalMapping(root, "logging");

        var site = new SiteConfig
        {
            Name = GetRequiredString(siteNode, "name"),
            Title = GetRequiredString(siteNode, "title"),
            Url = GetOptionalString(siteNode, "url"),
            Description = GetOptionalString(siteNode, "description"),
            AutoSummary = GetOptionalBool(siteNode, "autoSummary") ?? false,
            AutoSummaryMaxLength = GetOptionalInt(siteNode, "autoSummaryMaxLength") ?? 200,
            BaseUrl = GetOptionalString(siteNode, "baseUrl") ?? "/",
            OutputPathEncoding = GetOptionalString(siteNode, "outputPathEncoding") ?? "none",
            Language = GetOptionalString(siteNode, "language") ?? "zh-CN",
            Languages = ReadStringList(siteNode, "languages"),
            DefaultLanguage = GetOptionalString(siteNode, "defaultLanguage"),
            SitemapMode = GetOptionalString(siteNode, "sitemapMode") ?? "split",
            RssMode = GetOptionalString(siteNode, "rssMode") ?? "split",
            SearchMode = GetOptionalString(siteNode, "searchMode") ?? "split",
            SearchIncludeDerived = GetOptionalBool(siteNode, "searchIncludeDerived") ?? false,
            ExternalProtocolIncludeRoutedPages = GetOptionalBool(siteNode, "externalProtocolIncludeRoutedPages") ?? false,
            PluginFailMode = GetOptionalString(siteNode, "pluginFailMode") ?? "strict",
            DeriveConflictPolicy = GetOptionalString(siteNode, "deriveConflictPolicy") ?? "fail",
            Timezone = GetOptionalString(siteNode, "timezone") ?? "Asia/Shanghai",
            Permalinks = ReadStringMap(siteNode, "permalinks"),
            Collections = ReadCollections(siteNode),
            ExternalPlugins = ReadExternalPlugins(siteNode),
            ExternalAssemblyTrustMode = GetOptionalString(siteNode, "externalAssemblyTrustMode") ?? "warn",
            ExternalAssemblyAllowlist = ReadStringMap(siteNode, "externalAssemblyAllowlist"),
            Plugins = ReadPluginToggles(siteNode)
        };

        var sources = ReadSources(contentNode);
        var provider = GetOptionalString(contentNode, "provider");
        if (sources is null || sources.Count == 0)
        {
            provider = GetRequiredString(contentNode, "provider");
        }

        provider ??= "sources";
        var content = new ContentConfig
        {
            Provider = provider,
            Sources = sources,
            Notion = provider.Equals("notion", StringComparison.OrdinalIgnoreCase) ? ReadNotionConfigFrom(contentNode) : null,
            Markdown = provider.Equals("markdown", StringComparison.OrdinalIgnoreCase) ? ReadMarkdownConfigFrom(contentNode) : null,
            Media = ReadMediaConfigFrom(contentNode)
        };

        var build = new BuildConfig
        {
            Output = buildNode is null ? "dist" : GetOptionalString(buildNode, "output") ?? "dist",
            Clean = buildNode is null ? true : GetOptionalBool(buildNode, "clean") ?? true,
            Draft = buildNode is null ? false : GetOptionalBool(buildNode, "draft") ?? false,
            ListPageContentMode = buildNode is null ? "auto" : GetOptionalString(buildNode, "listPageContentMode") ?? "auto"
        };

        var theme = new ThemeConfig
        {
            Name = themeNode is null ? null : GetOptionalString(themeNode, "name"),
            Layouts = themeNode is null ? "layouts" : GetOptionalString(themeNode, "layouts") ?? "layouts",
            Assets = themeNode is null ? "assets" : GetOptionalString(themeNode, "assets") ?? "assets",
            Static = themeNode is null ? "static" : GetOptionalString(themeNode, "static") ?? "static",
            Params = ReadThemeParams(themeNode)
        };

        var taxonomy = new TaxonomyConfig
        {
            Template = taxonomyNode is null ? "pages/page.html" : GetOptionalString(taxonomyNode, "template") ?? "pages/page.html",
            IndexTemplate = taxonomyNode is null ? null : GetOptionalString(taxonomyNode, "indexTemplate"),
            TermTemplate = taxonomyNode is null ? null : GetOptionalString(taxonomyNode, "termTemplate"),
            Templates = ReadTaxonomyTemplates(taxonomyNode),
            Kinds = ReadTaxonomyKinds(taxonomyNode),
            OutputMode = taxonomyNode is null ? "both" : GetOptionalString(taxonomyNode, "outputMode") ?? "both",
            ItemFields = taxonomyNode is null ? null : ReadStringList(taxonomyNode, "itemFields"),
            PageSize = taxonomyNode is null ? 10 : GetOptionalInt(taxonomyNode, "pageSize") ?? 10,
            IndexEnabled = taxonomyNode is null ? true : GetOptionalBool(taxonomyNode, "indexEnabled") ?? true,
            PinField = taxonomyNode is null ? "pinned" : GetOptionalString(taxonomyNode, "pinField") ?? "pinned",
            PinOrderField = taxonomyNode is null ? null : GetOptionalString(taxonomyNode, "pinOrderField"),
            PinFieldBySource = ReadStringMap(taxonomyNode, "pinFieldBySource"),
            PinOrderFieldBySource = ReadStringMap(taxonomyNode, "pinOrderFieldBySource")
        };

        var logging = new LoggingConfig
        {
            Level = loggingNode is null ? "info" : GetOptionalString(loggingNode, "level") ?? "info"
        };

        return new AppConfig
        {
            Site = site,
            Content = content,
            Build = build,
            Theme = theme,
            Taxonomy = taxonomy,
            Logging = logging
        };
    }

    private static TaxonomyTemplatesConfig ReadTaxonomyTemplates(YamlMappingNode? taxonomyNode)
    {
        if (taxonomyNode is null)
        {
            return new TaxonomyTemplatesConfig();
        }

        var templatesNode = GetOptionalMapping(taxonomyNode, "templates");
        if (templatesNode is null)
        {
            return new TaxonomyTemplatesConfig();
        }

        return new TaxonomyTemplatesConfig
        {
            Tags = ReadTaxonomyKindTemplate(GetOptionalMapping(templatesNode, "tags")),
            Categories = ReadTaxonomyKindTemplate(GetOptionalMapping(templatesNode, "categories"))
        };
    }

    private static IReadOnlyList<TaxonomyKindConfig>? ReadTaxonomyKinds(YamlMappingNode? taxonomyNode)
    {
        if (taxonomyNode is null)
        {
            return null;
        }

        var kindsNode = GetOptionalSequence(taxonomyNode, "kinds");
        if (kindsNode is null)
        {
            return null;
        }

        var kinds = new List<TaxonomyKindConfig>();
        foreach (var n in kindsNode.Children)
        {
            if (n is not YamlMappingNode m)
            {
                throw new ConfigException("taxonomy.kinds items must be mappings.");
            }

            kinds.Add(new TaxonomyKindConfig
            {
                Key = GetRequiredString(m, "key"),
                Kind = GetOptionalString(m, "kind"),
                Title = GetOptionalString(m, "title"),
                SingularTitlePrefix = GetOptionalString(m, "singularTitlePrefix"),
                Template = GetOptionalString(m, "template"),
                IndexTemplate = GetOptionalString(m, "indexTemplate"),
                TermTemplate = GetOptionalString(m, "termTemplate"),
                IndexEnabled = GetOptionalBool(m, "indexEnabled")
            });
        }

        return kinds;
    }

    private static TaxonomyKindTemplateConfig ReadTaxonomyKindTemplate(YamlMappingNode? kindNode)
    {
        if (kindNode is null)
        {
            return new TaxonomyKindTemplateConfig();
        }

        return new TaxonomyKindTemplateConfig
        {
            Template = GetOptionalString(kindNode, "template"),
            IndexTemplate = GetOptionalString(kindNode, "indexTemplate"),
            TermTemplate = GetOptionalString(kindNode, "termTemplate")
        };
    }

    private static IReadOnlyList<ContentSourceConfig>? ReadSources(YamlMappingNode contentNode)
    {
        var sourcesNode = GetOptionalSequence(contentNode, "sources");
        if (sourcesNode is null)
        {
            return null;
        }

        var sources = new List<ContentSourceConfig>();
        foreach (var n in sourcesNode.Children)
        {
            if (n is not YamlMappingNode m)
            {
                throw new ConfigException("content.sources items must be mappings.");
            }

            sources.Add(ReadSource(m));
        }

        return sources;
    }

    private static ContentSourceConfig ReadSource(YamlMappingNode sourceNode)
    {
        var type = GetRequiredString(sourceNode, "type");
        var name = GetOptionalString(sourceNode, "name");
        var mode = GetOptionalString(sourceNode, "mode") ?? "content";
        if (type.Equals("notion", StringComparison.OrdinalIgnoreCase))
        {
            return new ContentSourceConfig
            {
                Type = "notion",
                Name = name,
                Mode = mode,
                Notion = ReadNotionConfigFrom(sourceNode)
            };
        }

        if (type.Equals("markdown", StringComparison.OrdinalIgnoreCase))
        {
            return new ContentSourceConfig
            {
                Type = "markdown",
                Name = name,
                Mode = mode,
                Markdown = ReadMarkdownConfigFrom(sourceNode)
            };
        }

        return new ContentSourceConfig
        {
            Type = type,
            Name = name,
            Mode = mode
        };
    }

    private static NotionConfig ReadNotionConfigFrom(YamlMappingNode contentNode)
    {
        var notionNode = GetOptionalMapping(contentNode, "notion") ?? contentNode;
        var policyNode = GetOptionalMapping(notionNode, "fieldPolicy");
        return new NotionConfig
        {
            DatabaseId = GetRequiredString(notionNode, "databaseId"),
            PageSize = GetOptionalInt(notionNode, "pageSize") ?? 50,
            MaxItems = GetOptionalInt(notionNode, "maxItems"),
            RenderContent = GetOptionalBool(notionNode, "renderContent"),
            RenderConcurrency = GetOptionalInt(notionNode, "renderConcurrency"),
            MaxRps = GetOptionalInt(notionNode, "maxRps"),
            MaxRetries = GetOptionalInt(notionNode, "maxRetries"),
            FieldPolicy = ReadNotionFieldPolicy(policyNode),
            FilterProperty = GetOptionalString(notionNode, "filterProperty") ?? "Published",
            FilterType = GetOptionalString(notionNode, "filterType") ?? "checkbox_true",
            SortProperty = GetOptionalString(notionNode, "sortProperty"),
            SortDirection = GetOptionalString(notionNode, "sortDirection") ?? "ascending",
            IncludeSlugs = ReadStringList(notionNode, "includeSlugs"),
            IncludeSlugProperty = GetOptionalString(notionNode, "includeSlugProperty") ?? "Slug",
            CacheMode = GetOptionalString(notionNode, "cacheMode") ?? "off",
            CacheDir = GetOptionalString(notionNode, "cacheDir")
        };
    }

    private static MediaConfig ReadMediaConfigFrom(YamlMappingNode contentNode)
    {
        var mediaNode = GetOptionalMapping(contentNode, "media");
        if (mediaNode is null)
        {
            return new MediaConfig();
        }

        return new MediaConfig
        {
            DownloadToLocal = GetOptionalBool(mediaNode, "downloadToLocal") ?? true,
            DownloadDir = GetOptionalString(mediaNode, "downloadDir") ?? "assets/uploads",
            UrlBase = GetOptionalString(mediaNode, "urlBase") ?? "/assets/uploads",
            DefaultImageUrl = GetOptionalString(mediaNode, "defaultImageUrl") ?? "/assets/images/noneimg-news.jpg",
            FieldKeys = ReadStringList(mediaNode, "fieldKeys") ?? new[] { "cover", "image", "thumbnail", "og_image", "icon" },
            MaxConcurrency = GetOptionalInt(mediaNode, "maxConcurrency") ?? 4,
            MaxRetries = GetOptionalInt(mediaNode, "maxRetries") ?? 3,
            TimeoutMs = GetOptionalInt(mediaNode, "timeoutMs") ?? 10000,
            MaxFileSizeBytes = GetOptionalLong(mediaNode, "maxFileSizeBytes") ?? 50 * 1024 * 1024,
            BlockPrivateNetworks = GetOptionalBool(mediaNode, "blockPrivateNetworks") ?? true,
            RetryBaseDelayMs = GetOptionalInt(mediaNode, "retryBaseDelayMs") ?? 500
        };
    }

    private static NotionFieldPolicyConfig ReadNotionFieldPolicy(YamlMappingNode? node)
    {
        if (node is null)
        {
            return new NotionFieldPolicyConfig();
        }

        return new NotionFieldPolicyConfig
        {
            Mode = GetOptionalString(node, "mode") ?? "whitelist",
            Allowed = ReadStringList(node, "allowed")
        };
    }

    private static MarkdownConfig ReadMarkdownConfigFrom(YamlMappingNode contentNode)
    {
        var mdNode = GetOptionalMapping(contentNode, "markdown") ?? contentNode;
        return new MarkdownConfig
        {
            Dir = GetOptionalString(mdNode, "dir") ?? "content",
            DefaultType = GetOptionalString(mdNode, "defaultType") ?? "page",
            MaxItems = GetOptionalInt(mdNode, "maxItems"),
            IncludePaths = ReadStringList(mdNode, "includePaths"),
            IncludeGlobs = ReadStringList(mdNode, "includeGlobs")
        };
    }

    private static YamlMappingNode GetMapping(YamlMappingNode node, string key)
    {
        var result = GetOptionalMapping(node, key);
        if (result is null)
        {
            throw new ConfigException($"{key} section is required.");
        }

        return result;
    }

    private static YamlMappingNode? GetOptionalMapping(YamlMappingNode node, string key)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode(key), out var child))
        {
            return null;
        }

        return child as YamlMappingNode;
    }

    private static IReadOnlyDictionary<string, object>? ReadThemeParams(YamlMappingNode? themeNode)
    {
        if (themeNode is null)
        {
            return null;
        }

        var paramsNode = GetOptionalMapping(themeNode, "params");
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

            dict[k.Value] = ToObject(kv.Value);
        }

        return dict;
    }

    private static IReadOnlyDictionary<string, string>? ReadStringMap(YamlMappingNode? parent, string key)
    {
        if (parent is null)
        {
            return null;
        }

        var map = GetOptionalMapping(parent, key);
        if (map is null)
        {
            return null;
        }

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in map.Children)
        {
            if (kv.Key is not YamlScalarNode k || string.IsNullOrWhiteSpace(k.Value))
            {
                continue;
            }

            if (kv.Value is not YamlScalarNode v || string.IsNullOrWhiteSpace(v.Value))
            {
                continue;
            }

            dict[k.Value.Trim()] = v.Value.Trim();
        }

        return dict.Count == 0 ? null : dict;
    }

    private static IReadOnlyDictionary<string, CollectionConfig>? ReadCollections(YamlMappingNode siteNode)
    {
        var collectionsNode = GetOptionalMapping(siteNode, "collections");
        if (collectionsNode is null)
        {
            return null;
        }

        var collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in collectionsNode.Children)
        {
            if (kv.Key is not YamlScalarNode keyNode || string.IsNullOrWhiteSpace(keyNode.Value))
            {
                continue;
            }

            if (kv.Value is not YamlMappingNode collectionNode)
            {
                throw new ConfigException($"site.collections.{keyNode.Value} must be a mapping.");
            }

            var paginationNode = GetOptionalMapping(collectionNode, "pagination");
            var outputNode = GetOptionalMapping(collectionNode, "output");
            collections[keyNode.Value.Trim()] = new CollectionConfig
            {
                Permalink = GetRequiredString(collectionNode, "permalink"),
                Template = GetRequiredString(collectionNode, "template"),
                ListRoute = GetOptionalString(collectionNode, "listRoute"),
                Pagination = new CollectionPaginationConfig
                {
                    Enabled = paginationNode is not null && (GetOptionalBool(paginationNode, "enabled") ?? false),
                    PageSize = paginationNode is null ? 10 : GetOptionalInt(paginationNode, "pageSize") ?? 10
                },
                Output = new CollectionOutputConfig
                {
                    Rss = outputNode is null ? true : GetOptionalBool(outputNode, "rss") ?? true,
                    Sitemap = outputNode is null ? true : GetOptionalBool(outputNode, "sitemap") ?? true,
                    Archive = outputNode is not null && (GetOptionalBool(outputNode, "archive") ?? false)
                }
            };
        }

        return collections.Count == 0 ? null : collections;
    }

    private static IReadOnlyDictionary<string, ExternalPluginConfig>? ReadExternalPlugins(YamlMappingNode siteNode)
    {
        var pluginsNode = GetOptionalMapping(siteNode, "externalPlugins");
        if (pluginsNode is null)
        {
            return null;
        }

        var plugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in pluginsNode.Children)
        {
            if (kv.Key is not YamlScalarNode keyNode || string.IsNullOrWhiteSpace(keyNode.Value))
            {
                continue;
            }

            if (kv.Value is not YamlMappingNode pluginNode)
            {
                throw new ConfigException($"site.externalPlugins.{keyNode.Value} must be a mapping.");
            }

            IReadOnlyDictionary<string, object>? options = null;
            if (pluginNode.Children.TryGetValue(new YamlScalarNode("options"), out var optionsRaw))
            {
                if (optionsRaw is not YamlMappingNode optionsNode)
                {
                    throw new ConfigException($"site.externalPlugins.{keyNode.Value}.options must be a mapping.");
                }

                options = ReadObjectMap(optionsNode);
            }

            plugins[keyNode.Value.Trim()] = new ExternalPluginConfig
            {
                Runtime = GetRequiredString(pluginNode, "runtime"),
                Entry = GetRequiredString(pluginNode, "entry"),
                Hooks = ReadStringList(pluginNode, "hooks") ?? Array.Empty<string>(),
                Enabled = GetOptionalBool(pluginNode, "enabled") ?? true,
                TimeoutMs = GetOptionalInt(pluginNode, "timeoutMs") ?? 5000,
                WasmProfile = GetOptionalString(pluginNode, "wasmProfile") ?? "wasi-preview1",
                MaxMemoryMb = GetOptionalInt(pluginNode, "maxMemoryMb") ?? 64,
                WasmFsMode = GetOptionalString(pluginNode, "wasmFsMode") ?? "output-only",
                WasmAllowNetwork = GetOptionalBool(pluginNode, "wasmAllowNetwork") ?? false,
                Capabilities = ReadStringList(pluginNode, "capabilities"),
                Options = options
            };
        }

        return plugins.Count == 0 ? null : plugins;
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

    private static YamlSequenceNode? GetOptionalSequence(YamlMappingNode node, string key)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode(key), out var child))
        {
            return null;
        }

        return child as YamlSequenceNode;
    }

    private static IReadOnlyList<string>? ReadStringList(YamlMappingNode node, string key)
    {
        var seq = GetOptionalSequence(node, key);
        if (seq is null)
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

    private static string GetRequiredString(YamlMappingNode node, string key)
    {
        var value = GetOptionalString(node, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ConfigException($"{key} is required.");
        }

        return value;
    }

    private static string? GetOptionalString(YamlMappingNode node, string key)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode(key), out var child))
        {
            return null;
        }

        if (child is not YamlScalarNode scalar)
        {
            return null;
        }

        return scalar.Value;
    }

    private static bool? GetOptionalBool(YamlMappingNode node, string key)
    {
        var value = GetOptionalString(node, key);
        if (value is null)
        {
            return null;
        }

        if (bool.TryParse(value, out var b))
        {
            return b;
        }

        if (value.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.Equals("no", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return null;
    }

    private static IReadOnlyDictionary<string, PluginToggleConfig>? ReadPluginToggles(YamlMappingNode siteNode)
    {
        var pluginsNode = GetOptionalMapping(siteNode, "plugins");
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
                enabled = GetOptionalBool(m, "enabled") ?? true;
                IReadOnlyDictionary<string, object>? options = null;
                if (m.Children.TryGetValue(new YamlScalarNode("options"), out var optionsRaw))
                {
                    if (optionsRaw is not YamlMappingNode optionsNode)
                    {
                        throw new ConfigException($"site.plugins.{name}.options must be a mapping.");
                    }

                    options = ReadObjectMap(optionsNode);
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
                throw new ConfigException($"site.plugins.{name} must be a mapping or boolean.");
            }

            plugins[name] = new PluginToggleConfig { Enabled = enabled };
        }

        return plugins;
    }

    private static IReadOnlyDictionary<string, object>? ReadObjectMap(YamlMappingNode mapNode)
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in mapNode.Children)
        {
            if (kv.Key is not YamlScalarNode keyNode || string.IsNullOrWhiteSpace(keyNode.Value))
            {
                continue;
            }

            dict[keyNode.Value.Trim()] = ToObject(kv.Value);
        }

        return dict.Count == 0 ? null : dict;
    }

    private static int? GetOptionalInt(YamlMappingNode node, string key)
    {
        var value = GetOptionalString(node, key);
        if (value is null)
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
        {
            return i;
        }

        return null;
    }

    private static long? GetOptionalLong(YamlMappingNode node, string key)
    {
        var value = GetOptionalString(node, key);
        if (value is null)
        {
            return null;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
        {
            return l;
        }

        return null;
    }
}
