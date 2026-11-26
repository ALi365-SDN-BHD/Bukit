using Bukit.Shared;
using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

internal static partial class SiteDefaultsApplier
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
            HomeTitleTemplate = ConfigYamlHelpers.GetOptionalString(seoNode, "homeTitleTemplate") ?? "{siteTitle}",
            PageTitleTemplate = ConfigYamlHelpers.GetOptionalString(seoNode, "pageTitleTemplate") ?? "{pageTitle}",
            TitleSeparator = ConfigYamlHelpers.GetOptionalString(seoNode, "titleSeparator") ?? " | ",
            DefaultImage = ConfigYamlHelpers.GetOptionalString(seoNode, "defaultImage"),
            TwitterSite = ConfigYamlHelpers.GetOptionalString(seoNode, "twitterSite"),
            DefaultAuthor = ConfigYamlHelpers.GetOptionalString(seoNode, "defaultAuthor"),
            DefaultAuthorType = ConfigYamlHelpers.GetOptionalString(seoNode, "defaultAuthorType"),
            Organization = ReadSeoOrganization(orgNode),
            RobotsTxt = ReadSeoRobotsTxt(robotsTxtNode),
            Schema = ReadSeoSchema(schemaNode),
            Geo = ReadSeoGeo(geoNode),
            Authors = SeoArticleConfig.ReadAuthors(seoNode)
        };
    }

    private static SeoOrganizationConfig? ReadSeoOrganization(YamlMappingNode? organizationNode)
    {
        if (organizationNode is null)
        {
            return null;
        }

        return new SeoOrganizationConfig
        {
            Type = organizationNode.Children.ContainsKey(new YamlScalarNode("type"))
                ? ConfigYamlHelpers.GetOptionalString(organizationNode, "type") ?? string.Empty
                : "Organization",
            Name = ConfigYamlHelpers.GetOptionalString(organizationNode, "name"),
            Url = ConfigYamlHelpers.GetOptionalString(organizationNode, "url"),
            Logo = ConfigYamlHelpers.GetOptionalString(organizationNode, "logo"),
            SameAs = ConfigYamlHelpers.ReadStringList(organizationNode, "sameAs") ?? Array.Empty<string>()
        };
    }

    private const string ContentSignalPath = "site.seo.robotsTxt.contentSignal";

    private static SeoRobotsTxtConfig ReadSeoRobotsTxt(YamlMappingNode? robotsTxtNode)
    {
        var signal = robotsTxtNode is null ? null : ConfigYamlHelpers.GetOptionalMapping(robotsTxtNode, "contentSignal");
        return new()
        {
            Enabled = robotsTxtNode is not null && (ConfigYamlHelpers.GetOptionalBool(robotsTxtNode, "enabled") ?? false),
            ContentSignalSearch = signal is null ? null : ConfigYamlHelpers.GetOptionalBool(signal, "search", ContentSignalPath),
            ContentSignalAiInput = signal is null ? null : ConfigYamlHelpers.GetOptionalBool(signal, "aiInput", ContentSignalPath),
            ContentSignalAiTrain = signal is null ? null : ConfigYamlHelpers.GetOptionalBool(signal, "aiTrain", ContentSignalPath)
        };
    }

    private static SeoSchemaConfig ReadSeoSchema(YamlMappingNode? schemaNode)
        => new()
        {
            WebPage = schemaNode is null || (ConfigYamlHelpers.GetOptionalBool(schemaNode, "webPage") ?? true),
            CollectionPage = schemaNode is null || (ConfigYamlHelpers.GetOptionalBool(schemaNode, "collectionPage") ?? true),
            SearchAction = schemaNode is null || (ConfigYamlHelpers.GetOptionalBool(schemaNode, "searchAction") ?? true),
            BreadcrumbHome = schemaNode is not null && (ConfigYamlHelpers.GetOptionalBool(schemaNode, "breadcrumbHome") ?? false),
            BreadcrumbParent = schemaNode is null ? null : ConfigYamlHelpers.GetOptionalString(schemaNode, "breadcrumbParent"),
            ArticleType = schemaNode is null ? null : ConfigYamlHelpers.GetOptionalString(schemaNode, "articleType")
        };

    private static SeoGeoConfig ReadSeoGeo(YamlMappingNode? geoNode)
    {
        if (geoNode is null)
        {
            return new SeoGeoConfig();
        }

        return new SeoGeoConfig
        {
            Enabled = ConfigYamlHelpers.GetOptionalBool(geoNode, "enabled") ?? true,
            LlmsTxt = ConfigYamlHelpers.GetOptionalBool(geoNode, "llmsTxt") ?? true,
            LlmsFullTxt = ConfigYamlHelpers.GetOptionalBool(geoNode, "llmsFullTxt") ?? false,
            LlmsTxtMaxArticles = ConfigYamlHelpers.GetOptionalInt(geoNode, "llmsTxtMaxArticles") ?? 20,
            AiBotMode = ConfigYamlHelpers.GetOptionalString(geoNode, "aiBotMode") ?? "allow",
            AiBotAllowList = ConfigYamlHelpers.ReadStringList(geoNode, "aiBotAllowList"),
            AiBotBlockList = ConfigYamlHelpers.ReadStringList(geoNode, "aiBotBlockList"),
            LlmsTxtOptionalLinks = ReadLlmsTxtOptionalLinks(geoNode)
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
            ProductionOnly = ConfigYamlHelpers.GetOptionalBool(analyticsNode, "productionOnly") ?? true,
            Consent = ReadAnalyticsConsent(analyticsNode),
            Csp = ReadAnalyticsCsp(analyticsNode),
            Providers = ReadAnalyticsProviders(analyticsNode)
        };
    }

    private static AnalyticsCspConfig? ReadAnalyticsCsp(YamlMappingNode analyticsNode)
    {
        var cspNode = ConfigYamlHelpers.GetOptionalMapping(analyticsNode, "csp");
        return cspNode is null
            ? null
            : new AnalyticsCspConfig
            {
                Mode = ConfigYamlHelpers.GetOptionalString(cspNode, "mode")
            };
    }

    private static AnalyticsConsentConfig? ReadAnalyticsConsent(YamlMappingNode analyticsNode)
    {
        var consentNode = ConfigYamlHelpers.GetOptionalMapping(analyticsNode, "consent");
        if (consentNode is null)
        {
            return null;
        }

        var googleNode = ConfigYamlHelpers.GetOptionalMapping(consentNode, "google");
        if (googleNode is null)
        {
            return new AnalyticsConsentConfig();
        }

        var defaultsNode = ConfigYamlHelpers.GetOptionalMapping(googleNode, "defaults");
        return new AnalyticsConsentConfig
        {
            Google = new AnalyticsGoogleConsentConfig
            {
                Mode = ConfigYamlHelpers.GetOptionalString(googleNode, "mode"),
                WaitForUpdateMs = ConfigYamlHelpers.GetOptionalInt(googleNode, "waitForUpdateMs"),
                Defaults = defaultsNode is null
                    ? null
                    : new AnalyticsGoogleConsentDefaultsConfig
                    {
                        AdStorage = ConfigYamlHelpers.GetOptionalString(defaultsNode, "adStorage"),
                        AnalyticsStorage = ConfigYamlHelpers.GetOptionalString(defaultsNode, "analyticsStorage"),
                        AdUserData = ConfigYamlHelpers.GetOptionalString(defaultsNode, "adUserData"),
                        AdPersonalization = ConfigYamlHelpers.GetOptionalString(defaultsNode, "adPersonalization")
                    }
            }
        };
    }

    private static IReadOnlyList<AnalyticsProviderConfig> ReadAnalyticsProviders(YamlMappingNode analyticsNode)
    {
        var providersNode = ConfigYamlHelpers.GetOptionalSequence(analyticsNode, "providers");
        if (providersNode is null)
        {
            return Array.Empty<AnalyticsProviderConfig>();
        }

        var providers = new List<AnalyticsProviderConfig>(providersNode.Children.Count);
        for (var index = 0; index < providersNode.Children.Count; index++)
        {
            if (providersNode.Children[index] is not YamlMappingNode providerNode)
            {
                throw new ConfigException(
                    $"site.analytics.providers[{index}] must be a mapping.",
                    DiagnosticCode.ConfigInvalidValue);
            }

            var type = ConfigYamlHelpers.GetRequiredString(providerNode, "type");
            var hasSnippetMode = providerNode.Children.ContainsKey(new YamlScalarNode("snippetMode"));
            var snippetMode = ConfigYamlHelpers.GetOptionalString(providerNode, "snippetMode");
            var hasScriptUrl = providerNode.Children.ContainsKey(new YamlScalarNode("scriptUrl"));
            var scriptUrl = ConfigYamlHelpers.GetOptionalString(providerNode, "scriptUrl");
            providers.Add(new AnalyticsProviderConfig
            {
                Type = type,
                MeasurementId = ConfigYamlHelpers.GetOptionalString(providerNode, "measurementId"),
                ContainerId = ConfigYamlHelpers.GetOptionalString(providerNode, "containerId"),
                Domain = ConfigYamlHelpers.GetOptionalString(providerNode, "domain"),
                SnippetMode = hasSnippetMode && snippetMode is null ? string.Empty : snippetMode,
                WebsiteId = ConfigYamlHelpers.GetOptionalString(providerNode, "websiteId"),
                ScriptUrl = hasScriptUrl && scriptUrl is null ? string.Empty : scriptUrl
            });
        }

        return providers;
    }

    internal static SharedAssetsConfig ReadSharedAssetsConfig(YamlMappingNode siteNode)
    {
        var sharedNode = ConfigYamlHelpers.GetOptionalMapping(siteNode, "sharedAssets");
        if (sharedNode is null)
        {
            return new SharedAssetsConfig();
        }

        var paths = new List<string>();
        var pathsNode = ConfigYamlHelpers.GetOptionalSequence(sharedNode, "paths", "site.sharedAssets");
        if (pathsNode is not null)
        {
            for (var i = 0; i < pathsNode.Children.Count; i++)
            {
                if (pathsNode.Children[i] is not YamlScalarNode scalar)
                {
                    throw ConfigYamlHelpers.NodeKindMismatch(
                        $"site.sharedAssets.paths[{i}]",
                        "scalar",
                        pathsNode.Children[i]);
                }

                paths.Add((scalar.Value ?? string.Empty).Trim());
            }
        }

        return new SharedAssetsConfig
        {
            Enabled = ConfigYamlHelpers.GetOptionalBool(sharedNode, "enabled") ?? false,
            Paths = pathsNode is null ? null : paths
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
            Route = ConfigYamlHelpers.GetOptionalString(searchNode, "route"),
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

        var threshold = ConfigYamlHelpers.GetOptionalIntStrict(relatedNode, "threshold") ?? 80;
        var limit = ConfigYamlHelpers.GetOptionalIntStrict(relatedNode, "limit") ?? 5;
        return new RelatedConfig
        {
            Enabled = ConfigYamlHelpers.GetOptionalBool(relatedNode, "enabled") ?? false,
            Threshold = threshold,
            Limit = limit,
            Indices = ReadRelatedIndices(relatedNode, "site.related") ?? new RelatedConfig().Indices,
            Collections = ReadRelatedCollections(relatedNode, threshold, limit)
        };
    }

    /// <summary>site.related.collections: threshold and limit default to the site.related values; indices are required.</summary>
    private static List<RelatedCollectionConfig> ReadRelatedCollections(YamlMappingNode relatedNode, int threshold, int limit)
    {
        if (ConfigYamlHelpers.GetOptionalMapping(relatedNode, "collections", "site.related") is not { } node)
        {
            return [];
        }

        var collections = new List<RelatedCollectionConfig>();
        foreach (var (key, value) in node.Children)
        {
            var collection = (key as YamlScalarNode)?.Value?.Trim();
            var path = $"site.related.collections.{collection}";
            if (string.IsNullOrEmpty(collection) || value is not YamlMappingNode rules)
            {
                throw new ConfigException($"{path} must be a mapping.", DiagnosticCode.ConfigInvalidValue);
            }

            var collectionLimit = ConfigYamlHelpers.GetOptionalIntStrict(rules, "limit") ?? limit;
            var collectionThreshold = ConfigYamlHelpers.GetOptionalIntStrict(rules, "threshold") ?? threshold;
            if (collectionLimit < 1 || collectionThreshold < 0)
            {
                throw new ConfigException($"{path} needs limit of at least 1 and threshold of at least 0.", DiagnosticCode.ConfigInvalidValue);
            }

            collections.Add(new RelatedCollectionConfig
            {
                Collection = collection,
                Threshold = collectionThreshold,
                Limit = collectionLimit,
                Indices = ReadRelatedIndices(rules, path)
                    ?? throw new ConfigException($"{path}.indices must list at least one index.", DiagnosticCode.ConfigRequiredFieldMissing)
            });
        }

        return collections;
    }

    private static List<RelatedIndexConfig>? ReadRelatedIndices(YamlMappingNode relatedNode, string path)
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
                throw new ConfigException($"{path}.indices items must be mappings.", DiagnosticCode.ConfigRequiredFieldMissing);
            }

            var name = ConfigYamlHelpers.GetOptionalString(m, "name")?.Trim();
            var field = ConfigYamlHelpers.GetOptionalString(m, "field")?.Trim();
            if (string.IsNullOrEmpty(name) == string.IsNullOrEmpty(field))
            {
                throw new ConfigException($"{path}.indices[{indices.Count}] needs exactly one of name or field.", DiagnosticCode.ConfigRequiredFieldMissing);
            }

            // "field" marks a field index internally; without field: it would silently score 0.
            if (string.Equals(name, "field", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConfigException($"{path}.indices[{indices.Count}] uses name: field; write field: <name> instead.", DiagnosticCode.ConfigInvalidValue);
            }

            if (field is { Length: > 0 } && !ContentLinkPath.IsName(field))
            {
                throw new ConfigException($"{path}.indices[{indices.Count}].field '{field}' is not a valid field name.", DiagnosticCode.ConfigInvalidValue);
            }

            indices.Add(new RelatedIndexConfig
            {
                Name = string.IsNullOrEmpty(field) ? name! : "field",
                Weight = ConfigYamlHelpers.GetOptionalIntStrict(m, "weight") ?? 100,
                Field = string.IsNullOrEmpty(field) ? null : field
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

}
