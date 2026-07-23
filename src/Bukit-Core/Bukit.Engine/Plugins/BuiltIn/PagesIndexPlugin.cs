using Bukit.Config;
using Bukit.Content;
using Bukit.Content.Media;
using Bukit.Content.Notion;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;

namespace Bukit.Engine.Plugins.BuiltIn;

internal sealed record NotionFetchedPage(
    string PageId,
    string Title,
    string Slug,
    string NotionUrl,
    IReadOnlyDictionary<string, ContentField> Fields);

internal interface INotionPageFetcher
{
    Task<NotionFetchedPage?> FetchAsync(NotionApiClient client, string pageId, CancellationToken cancellationToken);
}

internal sealed class PagesIndexPlugin : IBukitPlugin, IDerivePagesPlugin
{
    private readonly AppConfig _config;
    private readonly INotionPageFetcher _notionFetcher;

    public string Name => "pages-index";
    public string Version => "1.1.0";

    internal PagesIndexPlugin(AppConfig config)
        : this(config, new DefaultNotionPageFetcher())
    {
    }

    internal PagesIndexPlugin(AppConfig config, INotionPageFetcher notionFetcher)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(notionFetcher);
        _config = config;
        _notionFetcher = notionFetcher;
    }

    public IReadOnlyList<RoutedContentDocument> DerivePages(BuildContext context)
    {
        var index = GetOrCreateIndex(context);
        AddRoutedToIndex(context, index);
        ResolveNotionRelationsIfConfiguredAsync(context, index).GetAwaiter().GetResult();
        if (index.Count > 0)
        {
            context.Data["pages_by_id"] = index;
        }
        return Array.Empty<RoutedContentDocument>();
    }

    private static Dictionary<string, object> GetOrCreateIndex(BuildContext context)
    {
        if (context.Data.TryGetValue("pages_by_id", out var existing) &&
            existing is Dictionary<string, object> dict)
        {
            return dict;
        }

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    private static void AddRoutedToIndex(BuildContext context, Dictionary<string, object> index)
    {
        var recordsById = context.ContentGraph.Records
            .GroupBy(x => x.Identity.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var routedDocument in context.RoutedDocuments)
        {
            var document = routedDocument.Document;
            if (string.IsNullOrWhiteSpace(document.Id))
            {
                continue;
            }

            recordsById.TryGetValue(document.Id, out var record);
            index[document.Id] = BuildPageObject(document, routedDocument.Route, record);
        }
    }

    private static Dictionary<string, object> BuildPageObject(ContentDocument document, RouteInfo route, ContentRecord? record)
    {
        var type = record?.Identity.ContentType ?? ContentFieldReader.GetContentType(document);
        type = string.IsNullOrWhiteSpace(type) ? null : type.Trim();

        var summary = record?.Presentation.Summary ?? ContentFieldReader.GetSummary(document);
        summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = document.Id,
            ["title"] = record?.Presentation.Title ?? document.Title,
            ["url"] = route.Url,
            ["slug"] = document.Slug,
            ["type"] = type ?? string.Empty,
            ["publish_date"] = (record?.Lifecycle.PublishedAt ?? document.PublishAt).DateTime,
            ["summary"] = summary ?? string.Empty,
            ["fields"] = BuildFieldsObject(document.CustomFields)
        };
    }

    private static Dictionary<string, object> BuildFieldsObject(IReadOnlyDictionary<string, ContentField>? fields)
    {
        var obj = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (fields is null || fields.Count == 0)
        {
            return obj;
        }

        foreach (var kv in fields)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
            {
                continue;
            }

            var f = kv.Value;
            obj[kv.Key] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = f.Type,
                ["value"] = f.Value ?? string.Empty
            };
        }

        return obj;
    }

    private async Task ResolveNotionRelationsIfConfiguredAsync(BuildContext context, Dictionary<string, object> index)
    {
        if (!PagesIndexConfigHelper.HasNotionContent(_config))
        {
            return;
        }

        if (_config.Theme.Params is null || _config.Theme.Params.Count == 0)
        {
            return;
        }

        if (!PagesIndexConfigHelper.TryGetMap(_config.Theme.Params, "pages_index", out var pagesIndexCfg))
        {
            return;
        }

        if (!PagesIndexConfigHelper.TryGetMap(pagesIndexCfg, "resolve_notion", out var resolveCfg))
        {
            return;
        }

        if (!PagesIndexConfigHelper.TryGetBool(resolveCfg, "enabled", false))
        {
            return;
        }

        var fieldKeys = PagesIndexConfigHelper.TryGetStringList(resolveCfg, "field_keys");
        if (fieldKeys.Count == 0)
        {
            return;
        }

        var maxItems = PagesIndexConfigHelper.TryGetInt(resolveCfg, "max_items", 200);
        if (maxItems <= 0)
        {
            return;
        }

        var ids = PagesIndexConfigHelper.CollectRelationIds(context.RoutedDocuments, fieldKeys, index, maxItems);
        if (ids.Count == 0)
        {
            return;
        }

        var cacheMode = PagesIndexCacheHelper.NormalizeCacheMode(PagesIndexConfigHelper.TryGetString(resolveCfg, "cache_mode") ?? "readwrite");
        var cachePath = PagesIndexCacheHelper.ResolveCachePath(context.RootDir, PagesIndexConfigHelper.TryGetString(resolveCfg, "cache_path"));
        if (cacheMode != "off")
        {
            var cached = PagesIndexCacheHelper.TryLoadCache(cachePath);
            if (cached is not null && cached.Count > 0)
            {
                var toFetch = new List<string>();
                for (var i = 0; i < ids.Count; i++)
                {
                    var id = ids[i];
                    if (index.ContainsKey(id))
                    {
                        continue;
                    }

                    if (cached.TryGetValue(id, out var cachedPage))
                    {
                        index[id] = cachedPage;
                    }
                    else
                    {
                        toFetch.Add(id);
                    }
                }

                ids = toFetch;
                if (ids.Count == 0)
                {
                    if (cacheMode == "readwrite")
                    {
                        PagesIndexCacheHelper.TrySaveCache(cachePath, index);
                    }
                    return;
                }
            }
            else if (cacheMode == "readonly")
            {
                return;
            }
        }

        if (cacheMode == "readonly")
        {
            return;
        }

        var token = Bukit.Shared.EnvironmentHelper.GetNotionToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var concurrency = PagesIndexConfigHelper.TryGetInt(resolveCfg, "concurrency", 4);
        if (concurrency <= 0)
        {
            concurrency = 4;
        }

        var maxRps = PagesIndexConfigHelper.TryGetNullableInt(resolveCfg, "max_rps");
        var maxRetries = PagesIndexConfigHelper.TryGetInt(resolveCfg, "max_retries", 5);
        var requestDelayMs = PagesIndexConfigHelper.TryGetInt(resolveCfg, "request_delay_ms", 0);

        var opts = new NotionProviderOptions
        {
            DatabaseId = "dummy",
            Token = token.Trim(),
            MaxRetries = maxRetries,
            MaxRps = maxRps,
            RequestDelayMs = requestDelayMs
        };

        using var client = new NotionApiClient(opts);
        using var sem = new SemaphoreSlim(concurrency, concurrency);
        var tasks = new Task<NotionFetchedPage?>[ids.Count];
        for (var i = 0; i < ids.Count; i++)
        {
            var id = ids[i];
            tasks[i] = ResolveOneAsync(id);
        }

        await Task.WhenAll(tasks);

        var resolvedPages = new NotionFetchedPage?[tasks.Length];
        for (var i = 0; i < tasks.Length; i++)
        {
            resolvedPages[i] = await tasks[i];
        }

        await LocalizeResolvedPageFieldsAsync(resolvedPages, context, _config.Content.Media);

        for (var i = 0; i < resolvedPages.Length; i++)
        {
            var p = resolvedPages[i];
            if (p is null)
            {
                continue;
            }

            if (index.ContainsKey(p.PageId))
            {
                continue;
            }

            var pageType = GetTypeFromFields(p.Fields) ?? "notion";
            index[p.PageId] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = p.PageId,
                ["title"] = p.Title,
                ["url"] = string.Empty,
                ["external_url"] = p.NotionUrl,
                ["slug"] = p.Slug,
                ["type"] = pageType,
                ["publish_date"] = null!,
                ["summary"] = string.Empty,
                ["fields"] = BuildFieldsObject(p.Fields)
            };
        }

        if (cacheMode == "readwrite")
        {
            PagesIndexCacheHelper.TrySaveCache(cachePath, index);
        }

        async Task<NotionFetchedPage?> ResolveOneAsync(string pageId)
        {
            await sem.WaitAsync(CancellationToken.None);
            try
            {
                return await _notionFetcher.FetchAsync(client, pageId, CancellationToken.None);
            }
            finally
            {
                sem.Release();
            }
        }
    }

    private static string? GetTypeFromFields(IReadOnlyDictionary<string, ContentField> fields)
    {
        if (fields.TryGetValue("type", out var f) && f.Value is not null)
        {
            var s = f.Value.ToString();
            s = string.IsNullOrWhiteSpace(s) ? null : s.Trim();
            return s;
        }

        return null;
    }

    private static async Task LocalizeResolvedPageFieldsAsync(
        NotionFetchedPage?[] pages,
        BuildContext context,
        MediaConfig media)
    {
        if (!media.DownloadToLocal)
        {
            return;
        }

        var hasWork = false;
        for (var i = 0; i < pages.Length; i++)
        {
            if (pages[i] is not null)
            {
                hasWork = true;
                break;
            }
        }

        if (!hasWork)
        {
            return;
        }

        var mediaCacheDir = Path.Combine(context.RootDir, ".cache", "media");
        var effective = BuildResolveMediaConfig(media, context.RootDir, mediaCacheDir);
        var fieldKeys = new HashSet<string>(media.FieldKeys ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        using var localizer = new ImageAssetLocalizer(effective, context.Logger);

        for (var i = 0; i < pages.Length; i++)
        {
            var p = pages[i];
            if (p is null)
            {
                continue;
            }

            var fields = p.Fields;
            if (fields.Count == 0)
            {
                continue;
            }

            var changed = false;
            Dictionary<string, ContentField>? mutable = null;

            foreach (var kv in fields)
            {
                var isFileType = string.Equals(kv.Value.Type, "file", StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(kv.Value.Type, "files", StringComparison.OrdinalIgnoreCase);

                if (!isFileType && !fieldKeys.Contains(kv.Key))
                {
                    continue;
                }

                if (kv.Value.Value is string url && !string.IsNullOrWhiteSpace(url))
                {
                    var localized = await localizer.LocalizeAsync(url, CancellationToken.None);
                    if (!string.Equals(localized, url, StringComparison.Ordinal))
                    {
                        mutable ??= new Dictionary<string, ContentField>(fields, StringComparer.OrdinalIgnoreCase);
                        mutable[kv.Key] = kv.Value with { Value = localized };
                        changed = true;
                    }
                }
            }

            if (changed && mutable is not null)
            {
                pages[i] = p with { Fields = mutable };
            }
        }
    }

    private static MediaConfig BuildResolveMediaConfig(MediaConfig media, string rootDir, string cacheDir)
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

        return media with
        {
            DownloadDir = downloadDir,
            UrlBase = urlBase
        };
    }
}
