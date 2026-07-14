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

}
