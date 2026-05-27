using Bukit.Config;
using Bukit.Content;
using Bukit.Content.Media;
using Bukit.Content.Notion;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;

namespace Bukit.Engine.Plugins.BuiltIn;

public sealed record NotionFetchedPage(
    string PageId,
    string Title,
    string Slug,
    string NotionUrl,
    IReadOnlyDictionary<string, ContentField> Fields);

public interface INotionPageFetcher
{
    Task<NotionFetchedPage?> FetchAsync(NotionApiClient client, string pageId, CancellationToken cancellationToken);
}

public sealed class PagesIndexPlugin : IBukitPlugin, IDerivePagesPlugin
{
    private readonly INotionPageFetcher _notionFetcher;

    public string Name => "pages-index";
    public string Version => "1.1.0";

    public PagesIndexPlugin()
        : this(new DefaultNotionPageFetcher())
    {
    }

    public PagesIndexPlugin(INotionPageFetcher notionFetcher)
    {
        _notionFetcher = notionFetcher;
    }

    public IReadOnlyList<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)> DerivePages(BuildContext context)
    {
        var index = GetOrCreateIndex(context);
        AddRoutedToIndex(context, index);
        ResolveNotionRelationsIfConfiguredAsync(context, index).GetAwaiter().GetResult();
        if (index.Count > 0)
        {
            context.Data["pages_by_id"] = index;
        }
        return Array.Empty<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)>();
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
        foreach (var (item, route) in context.Routed)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                continue;
            }

            index[item.Id] = BuildPageObject(item, route);
        }
    }

    private static Dictionary<string, object> BuildPageObject(ContentItem item, RouteInfo route)
    {
        var type = item.Meta.TryGetValue("type", out var t) ? t?.ToString() : null;
        type = string.IsNullOrWhiteSpace(type) ? null : type.Trim();

        var summary = item.Meta.TryGetValue("summary", out var s) ? s?.ToString() : null;
        summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = item.Id,
            ["title"] = item.Title,
            ["url"] = route.Url,
            ["slug"] = item.Slug,
            ["type"] = type ?? string.Empty,
            ["publish_date"] = item.PublishAt.DateTime,
            ["summary"] = summary ?? string.Empty,
            ["fields"] = BuildFieldsObject(item.Fields)
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
        if (!PagesIndexConfigHelper.HasNotionContent(context.Config))
        {
            return;
        }

        if (context.Config.Theme.Params is null || context.Config.Theme.Params.Count == 0)
        {
            return;
        }

        if (!PagesIndexConfigHelper.TryGetMap(context.Config.Theme.Params, "pages_index", out var pagesIndexCfg))
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

        var ids = PagesIndexConfigHelper.CollectRelationIds(context.Routed, fieldKeys, index, maxItems);
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

        await LocalizeResolvedPageFieldsAsync(resolvedPages, context);

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

    private static async Task LocalizeResolvedPageFieldsAsync(NotionFetchedPage?[] pages, BuildContext context)
    {
        var media = context.Config.Content.Media;
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
