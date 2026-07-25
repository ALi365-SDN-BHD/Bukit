using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Content.Media;
using Bukit.Content.Markdown;
using Bukit.Content.Notion;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class ContentProviderFactory
{
    internal static IContentProvider Create(AppConfig config, string rootDir, bool isCi, ILogger logger)
    {
        if (config.Content.Sources is { Count: > 0 } sources)
        {
            var typeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in sources)
            {
                var t = (s.Type ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(t))
                {
                    continue;
                }

                typeCounts[t] = typeCounts.TryGetValue(t, out var c) ? c + 1 : 1;
            }

            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var providers = new List<(string SourceKey, string SourceMode, string? Collection, IReadOnlyList<string>? AddToCollections, IContentProvider Provider)>();

            for (var i = 0; i < sources.Count; i++)
            {
                var s = sources[i];
                var type = (s.Type ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(type))
                {
                    throw new ConfigException("content.sources[].type is required.", DiagnosticCode.ConfigRequiredFieldMissing);
                }

                var mode = (s.Mode ?? "content").Trim().ToLowerInvariant();
                var key = string.IsNullOrWhiteSpace(s.Name) ? string.Empty : s.Name.Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    key = type.ToLowerInvariant();
                    if (typeCounts.TryGetValue(type, out var count) && count > 1)
                    {
                        seen[type] = seen.TryGetValue(type, out var n) ? n + 1 : 1;
                        key = $"{key}{seen[type]}";
                    }
                }

                if (type.Equals("markdown", StringComparison.OrdinalIgnoreCase))
                {
                    var md = s.Markdown ?? new MarkdownConfig();
                    var contentDir = BuildPathUtils.MakeAbsolute(rootDir, md.Dir);
                    providers.Add((key, mode, s.Collection, s.AddToCollections, new MarkdownFolderProvider(new MarkdownFolderProviderOptions(contentDir, md.DefaultType, md.MaxItems, md.IncludePaths, md.IncludeGlobs))));
                    continue;
                }

                if (type.Equals("notion", StringComparison.OrdinalIgnoreCase))
                {
                    var notion = s.Notion;
                    if (notion is null)
                    {
                        throw new ConfigException("content.sources[].notion is required when type is notion.", DiagnosticCode.ConfigRequiredFieldMissing);
                    }

                    var renderContent = notion.RenderContent ?? mode != "data";
                    providers.Add((key, mode, s.Collection, s.AddToCollections, CreateNotionProvider(rootDir, notion, isCi, renderContent: renderContent, logger: logger, autoSummary: config.Site.AutoSummary, autoSummaryMaxLength: config.Site.AutoSummaryMaxLength)));
                    continue;
                }

                throw new ConfigException($"Unsupported content source type: {type}", DiagnosticCode.ConfigInvalidValue);
            }

            return new CompositeContentProvider(providers, ContentModelSchemaFactory.FromConfig(config));
        }

        throw new ConfigException("content.sources is required in Bukit 1.0.", DiagnosticCode.ConfigRequiredFieldMissing);
    }

    internal static async Task<RawContentLoadResult> LocalizeContentImagesAsync(
        RawContentLoadResult result,
        MediaConfig media,
        string rootDir,
        string cacheDir,
        ILogger logger,
        CancellationToken cancellationToken,
        Func<MediaConfig, ILogger, IImageAssetLocalizer>? localizerFactory = null)
    {
        var effective = BuildEffectiveMediaConfig(media, rootDir, cacheDir);
        var localizer = localizerFactory?.Invoke(effective, logger) ?? new ImageAssetLocalizer(effective, logger);
        var ownedLocalizer = localizer as IDisposable;
        var ownershipTransferred = false;
        var pipeline = new ContentImageRewritePipeline(effective, localizer);
        try
        {
            var documents = ContentDocumentNormalizer.ToDocuments(result.Documents);
            var localizedDocuments = await pipeline.RewriteAsync(documents, cancellationToken);

            var failures = localizer is ImageAssetLocalizer imageAssetLocalizer
                ? imageAssetLocalizer.Failures
                : Array.Empty<MediaFailure>();
            if (failures.Count > 0)
            {
                logger.Warn($"event=media.localize_summary failed={failures.Count}");
                foreach (var f in failures)
                {
                    logger.Warn($"  - {f.SourceUrl} => {f.Reason}");
                }
            }

            var localizedRaw = localizedDocuments
                .Select(document => new RawContentDocument(
                    Id: document.Id,
                    Title: document.Title,
                    Slug: document.Slug,
                    PublishAt: document.PublishAt,
                    Body: new RawBody(document.Body.Html, document.Body.BodyKey, document.Body.Markdown, document.Body.PlainText),
                    Properties: RawContentValue.FromFields(document.CustomFields),
                    Source: document.Source,
                    CustomFields: document.CustomFields))
                .ToArray();

            var bodyStore = new LocalizedContentBodyStore(result.BodyStore, pipeline, ownedLocalizer);
            ownershipTransferred = true;
            return new RawContentLoadResult(localizedRaw, bodyStore);
        }
        finally
        {
            if (!ownershipTransferred)
            {
                ownedLocalizer?.Dispose();
            }
        }
    }

    private static NotionContentProvider CreateNotionProvider(string rootDir, NotionConfig notion, bool isCi, bool renderContent, ILogger logger, bool autoSummary = false, int autoSummaryMaxLength = 200)
    {
        var token = EnvironmentHelper.GetNotionToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ConfigException("NOTION_TOKEN is required for notion provider and must come from environment variables.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        var renderConcurrency = notion.RenderConcurrency is > 0 ? notion.RenderConcurrency.Value : isCi ? 2 : 4;
        var maxRps = notion.MaxRps is > 0 ? notion.MaxRps.Value : 3;
        var maxRetries = notion.MaxRetries is >= 0 ? notion.MaxRetries.Value : 5;

        var cacheMode = (notion.CacheMode ?? "off").Trim().ToLowerInvariant();
        cacheMode = cacheMode is "readwrite" or "readonly" ? cacheMode : "off";
        var cacheDir = cacheMode == "off"
            ? null
            : string.IsNullOrWhiteSpace(notion.CacheDir)
                ? Path.Combine(rootDir, ".cache", "notion")
                : BuildPathUtils.MakeAbsolute(rootDir, notion.CacheDir);

        return new NotionContentProvider(new NotionProviderOptions
        {
            DatabaseId = notion.DatabaseId,
            Token = token,
            PageSize = notion.PageSize,
            MaxItems = notion.MaxItems,
            RequestDelayMs = 0,
            MaxRetries = maxRetries,
            RenderConcurrency = renderConcurrency,
            MaxRps = maxRps,
            FieldPolicyMode = notion.FieldPolicy.Mode,
            AllowedFields = notion.FieldPolicy.Allowed,
            FilterProperty = notion.FilterProperty,
            FilterType = notion.FilterType,
            FilterValue = notion.FilterValue,
            SortProperty = notion.SortProperty,
            SortDirection = notion.SortDirection,
            RenderContent = renderContent,
            IncludeSlugs = notion.IncludeSlugs,
            IncludeSlugProperty = notion.IncludeSlugProperty,
            CacheMode = cacheMode,
            CacheDir = cacheDir,
            PropertyMap = notion.PropertyMap,
            AutoSummary = autoSummary,
            AutoSummaryMaxLength = autoSummaryMaxLength
        }, logger: logger);
    }

    internal static MediaConfig BuildEffectiveMediaConfig(MediaConfig media, string rootDir, string cacheDir)
    {
        var downloadDir = (media.DownloadDir ?? string.Empty).Trim();
        if (downloadDir.Length == 0 || string.Equals(downloadDir, "assets/uploads", StringComparison.OrdinalIgnoreCase))
        {
            downloadDir = cacheDir;
        }
        else
        {
            downloadDir = Path.IsPathRooted(downloadDir)
                ? downloadDir
                : Path.GetFullPath(Path.Combine(rootDir, downloadDir));
        }

        var urlBase = (media.UrlBase ?? string.Empty).Trim();
        if (urlBase.Length == 0)
        {
            urlBase = "/assets/uploads";
        }
        if (!urlBase.StartsWith('/'))
        {
            urlBase = "/" + urlBase;
        }

        var defaultImageUrl = (media.DefaultImageUrl ?? string.Empty).Trim();
        if (defaultImageUrl.Length == 0)
        {
            defaultImageUrl = "/assets/images/noneimg-news.jpg";
        }
        if (!defaultImageUrl.StartsWith('/'))
        {
            defaultImageUrl = "/" + defaultImageUrl;
        }

        return media with
        {
            DownloadDir = downloadDir,
            UrlBase = urlBase,
            DefaultImageUrl = defaultImageUrl,
            FieldKeys = media.FieldKeys ?? Array.Empty<string>(),
            MaxConcurrency = media.MaxConcurrency is > 0 ? media.MaxConcurrency : 4,
            MaxRetries = media.MaxRetries is >= 0 ? media.MaxRetries : 3,
            TimeoutMs = media.TimeoutMs is > 0 ? media.TimeoutMs : 10000,
            MaxFileSizeBytes = media.MaxFileSizeBytes is > 0 ? media.MaxFileSizeBytes : 50 * 1024 * 1024,
            RetryBaseDelayMs = media.RetryBaseDelayMs is >= 0 ? media.RetryBaseDelayMs : 500
        };
    }
}
