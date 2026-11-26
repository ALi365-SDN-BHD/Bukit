using Bukit.Config;
using Bukit.Content;
using Bukit.Content.CloudBase;
using Bukit.Engine.Abstractions.Content;
using Bukit.Content.Media;
using Bukit.Content.Markdown;
using Bukit.Content.Notion;
using Bukit.Content.Notion.Snapshot;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class ContentProviderFactory
{
    internal static IContentProvider Create(AppConfig config, string rootDir, bool isCi, ILogger logger)
    {
        // DefaultContentProviderFactory is public: a config built in code may not have the site
        // language derived from site.languages yet. A loaded or engine-normalized config is unchanged.
        var site = I18nLanguagePolicy.NormalizeSite(config.Site);
        if (config.Content.Sources is { Count: > 0 } sources)
        {
            var keys = ContentSourceKeys.Compute(sources);
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
                var key = keys[i];

                if (type.Equals("markdown", StringComparison.OrdinalIgnoreCase))
                {
                    var md = s.Markdown ?? new MarkdownConfig();
                    var contentDir = BuildPathUtils.MakeAbsolute(rootDir, md.Dir);
                    var languages = I18nOutputMerger.GetLanguages(site);
                    var defaultLanguage = I18nOutputMerger.GetDefaultLanguage(site, languages);
                    providers.Add((key, mode, s.Collection, s.AddToCollections, new MarkdownFolderProvider(new MarkdownFolderProviderOptions(contentDir, md.DefaultType, md.MaxItems, md.IncludePaths, md.IncludeGlobs, config.Site.AutoSummary, config.Site.AutoSummaryMaxLength) { Languages = languages.Count > 0 ? languages : null, DefaultLanguage = defaultLanguage }, logger)));
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

                if (type.Equals("cloudbase", StringComparison.OrdinalIgnoreCase))
                {
                    var cloudBase = s.CloudBase ??
                        throw new ConfigException(
                            "content.sources[].cloudbase is required when type is cloudbase.",
                            DiagnosticCode.CloudBaseConfigInvalid);

                    providers.Add((key, mode, s.Collection, s.AddToCollections, CreateCloudBaseProvider(config, cloudBase, mode, rootDir, logger)));
                    continue;
                }

                throw new ConfigException($"Unsupported content source type: {type}", DiagnosticCode.ConfigInvalidValue);
            }

            return new CompositeContentProvider(providers, ContentModelSchemaFactory.FromConfig(config)) { Logger = logger };
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

            // Body images are localized lazily later; the localizer reports those on dispose.
            (localizer as ImageAssetLocalizer)?.LogFailureSummary(logger, "documents");

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

    private static CloudBaseContentSource CreateCloudBaseProvider(
        AppConfig config,
        CloudBaseConfig cloudBase,
        string mode,
        string rootDir,
        ILogger logger)
    {
        // Offline, the rows and file identities come from the content snapshot: no key is read.
        var offline = NotionSnapshotSession.Current is { Offline: true };
        var publishableKey = offline ? string.Empty : SecretStore.Get("CLOUDBASE_PUBLISHABLE_KEY");
        if (!offline && string.IsNullOrWhiteSpace(publishableKey))
        {
            throw new ConfigException(
                "CLOUDBASE_PUBLISHABLE_KEY is required for cloudbase provider and must come from environment variables.",
                DiagnosticCode.CloudBaseCredentialMissing);
        }

        if (cloudBase.FileFields.Count > 0 &&
            MediaPlaceholderProbe.FindMissing(config, rootDir, logger) is { } missing)
        {
            throw new ConfigException(
                $"cloudbase.fileFields requires the media placeholder image '{missing}' to exist.",
                DiagnosticCode.CloudBaseConfigInvalid);
        }

        return new CloudBaseContentSource(
            new CloudBaseContentSourceOptions
            {
                EnvId = cloudBase.EnvId,
                Region = cloudBase.Region,
                Table = cloudBase.Table,
                IdField = cloudBase.IdField,
                PageSize = cloudBase.PageSize,
                MaxItems = cloudBase.MaxItems,
                FilterField = cloudBase.Filter?.Field,
                FilterValue = cloudBase.Filter is { } filter ? ConfigEnvValue.Resolve(filter.Value, "cloudbase.filter.value") : null,
                SortField = cloudBase.Sort?.Field,
                SortDirection = cloudBase.Sort?.Direction ?? "asc",
                TitleField = cloudBase.TitleField,
                SlugField = cloudBase.SlugField,
                PublishAtField = cloudBase.PublishAtField,
                UpdatedAtField = cloudBase.UpdatedAtField,
                BodyField = cloudBase.BodyField,
                Mode = mode,
                FieldPolicyMode = cloudBase.FieldPolicy.Mode,
                AllowedFields = cloudBase.FieldPolicy.Allowed,
                PublishableKey = publishableKey ?? string.Empty,
                StorageBucket = cloudBase.StorageBucket,
                FileFields = cloudBase.FileFields,
                DefaultImageUrl = CloudBasePlaceholderUrl(config.Content.Media.DefaultImageUrl),
                SignExpiresSeconds = cloudBase.SignExpiresSeconds,
                MaxRps = cloudBase.MaxRps is > 0 ? cloudBase.MaxRps.Value : 5,
                MaxRetries = cloudBase.MaxRetries ?? 3
            },
            logger: logger);
    }

    private static NotionContentSource CreateNotionProvider(string rootDir, NotionConfig notion, bool isCi, bool renderContent, ILogger logger, bool autoSummary = false, int autoSummaryMaxLength = 200)
    {
        var snapshot = NotionSnapshotSession.Current;
        var token = snapshot is { Offline: true } ? string.Empty : EnvironmentHelper.GetNotionToken();
        if (snapshot is { Offline: true })
        {
            // Offline: never a token, never the network; the snapshot must hold this database.
            snapshot.EnsureDatabase(notion.DatabaseId);
        }
        else if (string.IsNullOrWhiteSpace(token))
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
        if (snapshot is not null)
        {
            // --notion-snapshot overrides cacheMode/cacheDir: bodies live in the snapshot,
            // never in the site directory.
            cacheMode = snapshot.Offline ? "readonly" : "readwrite";
            cacheDir = snapshot.CacheDirectory;
        }

        return new NotionContentSource(new NotionContentSourceOptions
        {
            DatabaseId = notion.DatabaseId,
            Token = token ?? string.Empty,
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
            CacheDir = cacheDir is null ? null : snapshot is not null ? cacheDir : BuildTransaction.Physical(cacheDir),
            PropertyMap = notion.PropertyMap,
            AutoSummary = autoSummary,
            AutoSummaryMaxLength = autoSummaryMaxLength,
            Snapshot = snapshot
        }, logger: logger);
    }

    /// <summary>The site path localized media is published under, such as <c>/assets/uploads</c>.</summary>
    internal static string NormalizeMediaUrlBase(string? urlBase)
    {
        var value = (urlBase ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return "/assets/uploads";
        }

        return value.StartsWith('/') ? value : "/" + value;
    }

    /// <summary>The site path of the media placeholder: trimmed, defaulted and root-relative.</summary>
    private static string NormalizeDefaultImageUrl(string? defaultImageUrl)
    {
        var value = (defaultImageUrl ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return "/assets/images/noneimg-news.jpg";
        }

        return value.StartsWith('/') ? value : "/" + value;
    }

    /// <summary>
    /// CloudBase substitutes the placeholder directly into fields and body images, so an absolute
    /// http(s) placeholder is kept as written; anything else gets the site-path normalization.
    /// </summary>
    internal static string CloudBasePlaceholderUrl(string? defaultImageUrl)
    {
        var value = (defaultImageUrl ?? string.Empty).Trim();
        return Uri.TryCreate(value, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https"
            ? value
            : NormalizeDefaultImageUrl(value);
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

        var urlBase = NormalizeMediaUrlBase(media.UrlBase);

        var defaultImageUrl = NormalizeDefaultImageUrl(media.DefaultImageUrl);

        return media with
        {
            DownloadDir = BuildTransaction.Physical(downloadDir),
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
