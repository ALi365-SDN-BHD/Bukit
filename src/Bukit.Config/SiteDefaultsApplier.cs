using Bukit.Shared;
using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

internal static class SiteDefaultsApplier
{
    internal static SeoConfig ReadSeoConfig(YamlMappingNode siteNode)
    {
        var seoNode = ConfigYamlHelpers.GetOptionalMapping(siteNode, "seo");
        if (seoNode is null)
        {
            return new SeoConfig();
        }

        var orgNode = ConfigYamlHelpers.GetOptionalMapping(seoNode, "organization");
        var robotsTxtNode = ConfigYamlHelpers.GetOptionalMapping(seoNode, "robotsTxt");
        var schemaNode = ConfigYamlHelpers.GetOptionalMapping(seoNode, "schema");
        var geoNode = ConfigYamlHelpers.GetOptionalMapping(seoNode, "geo");
        return new SeoConfig
        {
            Enabled = ConfigYamlHelpers.GetOptionalBool(seoNode, "enabled") ?? true,
            RenderMode = ConfigYamlHelpers.GetOptionalString(seoNode, "renderMode") ?? "inject",
            Diagnostics = ConfigYamlHelpers.GetOptionalString(seoNode, "diagnostics") ?? "warn",
            DefaultImage = ConfigYamlHelpers.GetOptionalString(seoNode, "defaultImage"),
            TwitterSite = ConfigYamlHelpers.GetOptionalString(seoNode, "twitterSite"),
            Organization = orgNode is null
                ? null
                : new SeoOrganizationConfig
                {
                    Name = ConfigYamlHelpers.GetOptionalString(orgNode, "name"),
                    Url = ConfigYamlHelpers.GetOptionalString(orgNode, "url"),
                    Logo = ConfigYamlHelpers.GetOptionalString(orgNode, "logo")
                },
            RobotsTxt = new SeoRobotsTxtConfig
            {
                Enabled = robotsTxtNode is not null && (ConfigYamlHelpers.GetOptionalBool(robotsTxtNode, "enabled") ?? false)
            },
            Schema = new SeoSchemaConfig
            {
                WebPage = schemaNode is null || (ConfigYamlHelpers.GetOptionalBool(schemaNode, "webPage") ?? true),
                CollectionPage = schemaNode is null || (ConfigYamlHelpers.GetOptionalBool(schemaNode, "collectionPage") ?? true),
                SearchAction = schemaNode is null || (ConfigYamlHelpers.GetOptionalBool(schemaNode, "searchAction") ?? true)
            },
            Geo = geoNode is null
                ? new SeoGeoConfig()
                : new SeoGeoConfig
                {
                    Enabled = ConfigYamlHelpers.GetOptionalBool(geoNode, "enabled") ?? true,
                    LlmsTxt = ConfigYamlHelpers.GetOptionalBool(geoNode, "llmsTxt") ?? true,
                    LlmsFullTxt = ConfigYamlHelpers.GetOptionalBool(geoNode, "llmsFullTxt") ?? false,
                    LlmsTxtMaxArticles = ConfigYamlHelpers.GetOptionalInt(geoNode, "llmsTxtMaxArticles") ?? 20,
                    AiBotMode = ConfigYamlHelpers.GetOptionalString(geoNode, "aiBotMode") ?? "allow",
                    AiBotAllowList = ConfigYamlHelpers.ReadStringList(geoNode, "aiBotAllowList"),
                    AiBotBlockList = ConfigYamlHelpers.ReadStringList(geoNode, "aiBotBlockList"),
                    LlmsTxtOptionalLinks = ReadLlmsTxtOptionalLinks(geoNode)
                }
        };
    }

    internal static AnalyticsConfig ReadAnalyticsConfig(YamlMappingNode siteNode)
    {
        var analyticsNode = ConfigYamlHelpers.GetOptionalMapping(siteNode, "analytics");
        if (analyticsNode is null)
        {
            return new AnalyticsConfig();
        }

        return new AnalyticsConfig
        {
            Enabled = ConfigYamlHelpers.GetOptionalBool(analyticsNode, "enabled") ?? true,
            GoogleAnalyticsId = ConfigYamlHelpers.GetOptionalString(analyticsNode, "googleAnalyticsId"),
            DisableInPreview = ConfigYamlHelpers.GetOptionalBool(analyticsNode, "disableInPreview") ?? true
        };
    }

    internal static FeedConfig ReadFeedConfig(YamlMappingNode siteNode)
    {
        var feedNode = ConfigYamlHelpers.GetOptionalMapping(siteNode, "feed");
        if (feedNode is null)
        {
            return new FeedConfig();
        }

        return new FeedConfig
        {
            Mode = ConfigYamlHelpers.GetOptionalString(feedNode, "mode") ?? "split",
            Formats = ConfigYamlHelpers.ReadStringList(feedNode, "formats") ?? new[] { "rss" },
            Limit = ConfigYamlHelpers.GetOptionalInt(feedNode, "limit") ?? 20,
            Path = ConfigYamlHelpers.GetOptionalString(feedNode, "path") ?? "feed"
        };
    }

    internal static SitemapDetailConfig ReadSitemapDetailConfig(YamlMappingNode siteNode)
    {
        var sitemapNode = ConfigYamlHelpers.GetOptionalMapping(siteNode, "sitemapDetail");
        if (sitemapNode is null)
        {
            return new SitemapDetailConfig();
        }

        return new SitemapDetailConfig
        {
            DefaultPriority = ConfigYamlHelpers.GetOptionalDouble(sitemapNode, "defaultPriority") ?? 0.5,
            DefaultChangefreq = ConfigYamlHelpers.GetOptionalString(sitemapNode, "defaultChangefreq") ?? "weekly",
            ImageEnabled = ConfigYamlHelpers.GetOptionalBool(sitemapNode, "imageEnabled") ?? false,
            VideoEnabled = ConfigYamlHelpers.GetOptionalBool(sitemapNode, "videoEnabled") ?? false
        };
    }

    internal static PaginationGlobalConfig ReadPaginationConfig(YamlMappingNode siteNode)
    {
        var paginationNode = ConfigYamlHelpers.GetOptionalMapping(siteNode, "pagination");
        if (paginationNode is null)
        {
            return new PaginationGlobalConfig();
        }

        return new PaginationGlobalConfig
        {
            Enabled = ConfigYamlHelpers.GetOptionalBool(paginationNode, "enabled") ?? false,
            PageSize = ConfigYamlHelpers.GetOptionalIntStrict(paginationNode, "pageSize") ?? 10
        };
    }

    internal static SearchDetailConfig ReadSearchConfig(YamlMappingNode siteNode)
    {
        var searchNode = ConfigYamlHelpers.GetOptionalMapping(siteNode, "search");
        if (searchNode is null)
        {
            return new SearchDetailConfig();
        }

        return new SearchDetailConfig
        {
            Mode = ConfigYamlHelpers.GetOptionalString(searchNode, "mode") ?? "split",
            Ui = ConfigYamlHelpers.GetOptionalString(searchNode, "ui") ?? "default",
            UiTheme = ConfigYamlHelpers.GetOptionalString(searchNode, "uiTheme") ?? "light",
            PlaceholderText = ConfigYamlHelpers.GetOptionalString(searchNode, "placeholderText"),
            MaxContentLength = ConfigYamlHelpers.GetOptionalInt(searchNode, "maxContentLength") ?? 8000
        };
    }

    internal static RelatedConfig ReadRelatedConfig(YamlMappingNode siteNode)
    {
        var relatedNode = ConfigYamlHelpers.GetOptionalMapping(siteNode, "related");
        if (relatedNode is null)
        {
            return new RelatedConfig();
        }

        return new RelatedConfig
        {
            Enabled = ConfigYamlHelpers.GetOptionalBool(relatedNode, "enabled") ?? false,
            Threshold = ConfigYamlHelpers.GetOptionalIntStrict(relatedNode, "threshold") ?? 80,
            Limit = ConfigYamlHelpers.GetOptionalIntStrict(relatedNode, "limit") ?? 5,
            Indices = ReadRelatedIndices(relatedNode) ?? new RelatedConfig().Indices
        };
    }

    private static IReadOnlyList<RelatedIndexConfig>? ReadRelatedIndices(YamlMappingNode relatedNode)
    {
        var indicesNode = ConfigYamlHelpers.GetOptionalSequence(relatedNode, "indices");
        if (indicesNode is null)
        {
            return null;
        }

        var indices = new List<RelatedIndexConfig>();
        foreach (var n in indicesNode.Children)
        {
            if (n is not YamlMappingNode m)
            {
                throw new ConfigException("site.related.indices items must be mappings.", DiagnosticCode.ConfigRequiredFieldMissing);
            }

            indices.Add(new RelatedIndexConfig
            {
                Name = ConfigYamlHelpers.GetRequiredString(m, "name"),
                Weight = ConfigYamlHelpers.GetOptionalIntStrict(m, "weight") ?? 100
            });
        }

        return indices.Count == 0 ? null : indices;
    }

    internal static IReadOnlyDictionary<string, IReadOnlyList<MenuConfig>>? ReadMenus(YamlMappingNode siteNode)
    {
        var menusNode = ConfigYamlHelpers.GetOptionalMapping(siteNode, "menus");
        if (menusNode is null)
        {
            return null;
        }

        var menus = new Dictionary<string, IReadOnlyList<MenuConfig>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in menusNode.Children)
        {
            if (kv.Key is not YamlScalarNode keyNode || string.IsNullOrWhiteSpace(keyNode.Value))
            {
                continue;
            }

            if (kv.Value is not YamlSequenceNode itemsNode)
            {
                throw new ConfigException($"site.menus.{keyNode.Value} must be a sequence.", DiagnosticCode.ConfigRequiredFieldMissing);
            }

            var items = ReadMenuItems(itemsNode, $"site.menus.{keyNode.Value}");
            if (items.Count > 0)
            {
                menus[keyNode.Value.Trim()] = items;
            }
        }

        return menus.Count == 0 ? null : menus;
    }

    private static IReadOnlyList<MenuConfig> ReadMenuItems(YamlSequenceNode itemsNode, string path)
    {
        var items = new List<MenuConfig>();
        var index = 0;
        foreach (var n in itemsNode.Children)
        {
            if (n is not YamlMappingNode m)
            {
                throw new ConfigException($"{path}[{index}] must be a mapping.", DiagnosticCode.ConfigRequiredFieldMissing);
            }

            var childrenNode = ConfigYamlHelpers.GetOptionalSequence(m, "children");
            items.Add(new MenuConfig
            {
                Identifier = ConfigYamlHelpers.GetRequiredString(m, "identifier"),
                Name = ConfigYamlHelpers.GetRequiredString(m, "name"),
                Url = ConfigYamlHelpers.GetRequiredString(m, "url"),
                Weight = ConfigYamlHelpers.GetOptionalIntStrict(m, "weight") ?? 1,
                Children = childrenNode is null ? null : ReadMenuItems(childrenNode, $"{path}[{index}].children")
            });
            index++;
        }

        return items;
    }

    internal static NotionConfig ReadNotionConfigFrom(YamlMappingNode contentNode)
    {
        var notionNode = ConfigYamlHelpers.GetMapping(contentNode, "notion");
        var policyNode = ConfigYamlHelpers.GetOptionalMapping(notionNode, "fieldPolicy");
        var propertyMapNode = ConfigYamlHelpers.GetOptionalMapping(notionNode, "propertyMap");
        return new NotionConfig
        {
            DatabaseId = ConfigYamlHelpers.GetRequiredString(notionNode, "databaseId"),
            PageSize = ConfigYamlHelpers.GetOptionalIntStrict(notionNode, "pageSize") ?? 50,
            MaxItems = ConfigYamlHelpers.GetOptionalInt(notionNode, "maxItems"),
            RenderContent = ConfigYamlHelpers.GetOptionalBool(notionNode, "renderContent"),
            RenderConcurrency = ConfigYamlHelpers.GetOptionalInt(notionNode, "renderConcurrency"),
            MaxRps = ConfigYamlHelpers.GetOptionalInt(notionNode, "maxRps"),
            MaxRetries = ConfigYamlHelpers.GetOptionalInt(notionNode, "maxRetries"),
            FieldPolicy = ReadNotionFieldPolicy(policyNode),
            FilterProperty = ConfigYamlHelpers.GetOptionalString(notionNode, "filterProperty") ?? "Published",
            FilterType = ConfigYamlHelpers.GetOptionalString(notionNode, "filterType") ?? "checkbox_true",
            FilterValue = ConfigYamlHelpers.GetOptionalString(notionNode, "filterValue"),
            SortProperty = ConfigYamlHelpers.GetOptionalString(notionNode, "sortProperty"),
            SortDirection = ConfigYamlHelpers.GetOptionalString(notionNode, "sortDirection") ?? "ascending",
            IncludeSlugs = ConfigYamlHelpers.ReadStringList(notionNode, "includeSlugs"),
            IncludeSlugProperty = ConfigYamlHelpers.GetOptionalString(notionNode, "includeSlugProperty") ?? "Slug",
            CacheMode = ConfigYamlHelpers.GetOptionalString(notionNode, "cacheMode") ?? "off",
            CacheDir = ConfigYamlHelpers.GetOptionalString(notionNode, "cacheDir"),
            PropertyMap = ReadNotionPropertyMap(propertyMapNode)
        };
    }

    internal static MediaConfig ReadMediaConfigFrom(YamlMappingNode contentNode)
    {
        var mediaNode = ConfigYamlHelpers.GetOptionalMapping(contentNode, "media");
        if (mediaNode is null)
        {
            return new MediaConfig();
        }

        return new MediaConfig
        {
            DownloadToLocal = ConfigYamlHelpers.GetOptionalBool(mediaNode, "downloadToLocal") ?? true,
            DownloadDir = ConfigYamlHelpers.GetOptionalString(mediaNode, "downloadDir") ?? "assets/uploads",
            UrlBase = ConfigYamlHelpers.GetOptionalString(mediaNode, "urlBase") ?? "/assets/uploads",
            DefaultImageUrl = ConfigYamlHelpers.GetOptionalString(mediaNode, "defaultImageUrl") ?? "/assets/images/noneimg-news.jpg",
            FieldKeys = ConfigYamlHelpers.ReadStringList(mediaNode, "fieldKeys") ?? new[] { "cover", "image", "thumbnail", "og_image", "icon" },
            MaxConcurrency = ConfigYamlHelpers.GetOptionalInt(mediaNode, "maxConcurrency") ?? 4,
            MaxRetries = ConfigYamlHelpers.GetOptionalInt(mediaNode, "maxRetries") ?? 3,
            TimeoutMs = ConfigYamlHelpers.GetOptionalIntStrict(mediaNode, "timeoutMs") ?? 10000,
            MaxFileSizeBytes = ConfigYamlHelpers.GetOptionalLong(mediaNode, "maxFileSizeBytes") ?? 50 * 1024 * 1024,
            BlockPrivateNetworks = ConfigYamlHelpers.GetOptionalBool(mediaNode, "blockPrivateNetworks") ?? true,
            RetryBaseDelayMs = ConfigYamlHelpers.GetOptionalInt(mediaNode, "retryBaseDelayMs") ?? 500
        };
    }

    internal static NotionFieldPolicyConfig ReadNotionFieldPolicy(YamlMappingNode? node)
    {
        if (node is null)
        {
            return new NotionFieldPolicyConfig();
        }

        return new NotionFieldPolicyConfig
        {
            Mode = ConfigYamlHelpers.GetOptionalString(node, "mode") ?? "whitelist",
            Allowed = ConfigYamlHelpers.ReadStringList(node, "allowed")
        };
    }

    internal static NotionPropertyMapConfig? ReadNotionPropertyMap(YamlMappingNode? node)
    {
        if (node is null)
        {
            return null;
        }

        return new NotionPropertyMapConfig
        {
            Title = ConfigYamlHelpers.GetOptionalString(node, "Title"),
            Slug = ConfigYamlHelpers.GetOptionalString(node, "Slug"),
            Type = ConfigYamlHelpers.GetOptionalString(node, "Type"),
            PublishAt = ConfigYamlHelpers.GetOptionalString(node, "PublishAt"),
            Language = ConfigYamlHelpers.GetOptionalString(node, "Language"),
            I18nKey = ConfigYamlHelpers.GetOptionalString(node, "I18nKey"),
            Summary = ConfigYamlHelpers.GetOptionalString(node, "Summary"),
            Collection = ConfigYamlHelpers.GetOptionalString(node, "Collection"),
            SeoTitle = ConfigYamlHelpers.GetOptionalString(node, "SeoTitle"),
            SeoDescription = ConfigYamlHelpers.GetOptionalString(node, "SeoDescription"),
            SeoImage = ConfigYamlHelpers.GetOptionalString(node, "SeoImage"),
            Canonical = ConfigYamlHelpers.GetOptionalString(node, "Canonical")
        };
    }

    internal static MarkdownConfig ReadMarkdownConfigFrom(YamlMappingNode contentNode)
    {
        var mdNode = ConfigYamlHelpers.GetMapping(contentNode, "markdown");
        return new MarkdownConfig
        {
            Dir = ConfigYamlHelpers.GetOptionalString(mdNode, "dir") ?? "content",
            DefaultType = ConfigYamlHelpers.GetOptionalString(mdNode, "defaultType") ?? string.Empty,
            MaxItems = ConfigYamlHelpers.GetOptionalInt(mdNode, "maxItems"),
            IncludePaths = ConfigYamlHelpers.ReadStringList(mdNode, "includePaths"),
            IncludeGlobs = ConfigYamlHelpers.ReadStringList(mdNode, "includeGlobs")
        };
    }

    internal static DeployConfig? ReadDeployConfig(YamlMappingNode? deployNode)
    {
        if (deployNode is null)
        {
            return null;
        }

        return new DeployConfig
        {
            Provider = ConfigYamlHelpers.GetOptionalString(deployNode, "provider"),
            Branch = ConfigYamlHelpers.GetOptionalString(deployNode, "branch") ?? "gh-pages",
            Message = ConfigYamlHelpers.GetOptionalString(deployNode, "message") ?? "bukit deploy",
            Cname = ConfigYamlHelpers.GetOptionalString(deployNode, "cname"),
            KeepHistory = ConfigYamlHelpers.GetOptionalBool(deployNode, "keepHistory") ?? false
        };
    }

    internal static TaxonomyConfig ReadTaxonomyConfig(YamlMappingNode? taxonomyNode)
    {
        if (taxonomyNode is null)
        {
            return new TaxonomyConfig();
        }

        return new TaxonomyConfig
        {
            Kinds = ReadTaxonomyKinds(taxonomyNode),
            OutputMode = ConfigYamlHelpers.GetOptionalString(taxonomyNode, "outputMode") ?? "both",
            ItemFields = ConfigYamlHelpers.ReadStringList(taxonomyNode, "itemFields"),
            PageSize = ConfigYamlHelpers.GetOptionalIntStrict(taxonomyNode, "pageSize") ?? 10,
            IndexEnabled = ConfigYamlHelpers.GetOptionalBool(taxonomyNode, "indexEnabled") ?? true,
            PinField = ConfigYamlHelpers.GetOptionalString(taxonomyNode, "pinField") ?? "pinned",
            PinOrderField = ConfigYamlHelpers.GetOptionalString(taxonomyNode, "pinOrderField"),
            PinFieldBySource = ConfigYamlHelpers.ReadStringMap(taxonomyNode, "pinFieldBySource"),
            PinOrderFieldBySource = ConfigYamlHelpers.ReadStringMap(taxonomyNode, "pinOrderFieldBySource")
        };
    }

    internal static IReadOnlyList<TaxonomyKindConfig>? ReadTaxonomyKinds(YamlMappingNode? taxonomyNode)
    {
        if (taxonomyNode is null)
        {
            return null;
        }

        var kindsNode = ConfigYamlHelpers.GetOptionalSequence(taxonomyNode, "kinds");
        if (kindsNode is null)
        {
            return null;
        }

        var kinds = new List<TaxonomyKindConfig>();
        foreach (var n in kindsNode.Children)
        {
            if (n is not YamlMappingNode m)
            {
                throw new ConfigException("taxonomy.kinds items must be mappings.", DiagnosticCode.ConfigRequiredFieldMissing);
            }

            kinds.Add(new TaxonomyKindConfig
            {
                Key = ConfigYamlHelpers.GetRequiredString(m, "key"),
                Kind = ConfigYamlHelpers.GetOptionalString(m, "kind"),
                Title = ConfigYamlHelpers.GetOptionalString(m, "title"),
                SingularTitlePrefix = ConfigYamlHelpers.GetOptionalString(m, "singularTitlePrefix"),
                Template = ConfigYamlHelpers.GetOptionalString(m, "template"),
                IndexTemplate = ConfigYamlHelpers.GetOptionalString(m, "indexTemplate"),
                TermTemplate = ConfigYamlHelpers.GetOptionalString(m, "termTemplate"),
                IndexEnabled = ConfigYamlHelpers.GetOptionalBool(m, "indexEnabled"),
                Hierarchical = ConfigYamlHelpers.GetOptionalBool(m, "hierarchical") ?? false
            });
        }

        return kinds;
    }

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
