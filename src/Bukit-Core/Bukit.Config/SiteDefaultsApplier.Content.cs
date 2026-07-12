using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

internal static partial class SiteDefaultsApplier
{
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
            Canonical = ConfigYamlHelpers.GetOptionalString(node, "Canonical"),
            OriginalUrl = ConfigYamlHelpers.GetOptionalString(node, "OriginalUrl"),
            References = ConfigYamlHelpers.GetOptionalString(node, "References"),
            EntitiesJson = ConfigYamlHelpers.GetOptionalString(node, "EntitiesJson"),
            Cover = ConfigYamlHelpers.GetOptionalString(node, "Cover"),
            CoverAlt = ConfigYamlHelpers.GetOptionalString(node, "CoverAlt"),
            CoverCaption = ConfigYamlHelpers.GetOptionalString(node, "CoverCaption")
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
}
