using System.Text.Json;
using Bukit.Shared;

namespace Bukit.WechatSyncing;

public static class WechatSyncInputLoader
{
    public static async Task<WechatSyncContext> LoadAsync(
        string rootDir,
        string outputDir,
        string? manifestPath,
        string siteName,
        string? siteUrl,
        string baseUrl,
        string? mediaDownloadDir,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        rootDir = Path.GetFullPath(rootDir);
        outputDir = ResolveUnderRoot(rootDir, rootDir, outputDir, "--output");
        manifestPath = string.IsNullOrWhiteSpace(manifestPath)
            ? ResolveOutputPath(outputDir, "agent-manifest.json", "agent manifest")
            : ResolveUnderRoot(rootDir, rootDir, manifestPath, "--manifest");

        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("wechat-sync requires agent-manifest.json. Run bukit build before syncing.", manifestPath);
        }

        await using var stream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync(
            stream,
            WechatSyncInputJsonContext.Default.ContentProjectionAgentManifest,
            cancellationToken);
        if (manifest is null)
        {
            throw new InvalidOperationException($"wechat-sync manifest is empty or invalid: {manifestPath}");
        }

        var routed = new List<(WechatSyncItem Item, WechatSyncRoute Route)>();
        var documents = manifest.Documents ?? [];
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var jsonUrl = document.Representations
                .FirstOrDefault(x => x.Kind.Equals("json", StringComparison.OrdinalIgnoreCase))
                ?.Url;
            if (string.IsNullOrWhiteSpace(jsonUrl))
            {
                logger.Warn($"plugin wechat-sync manifest document has no json representation: {document.Id}");
                continue;
            }

            var jsonPath = ResolveOutputPath(outputDir, jsonUrl, "content json");
            if (!File.Exists(jsonPath))
            {
                logger.Warn($"plugin wechat-sync content json missing: {jsonUrl}");
                continue;
            }

            await using var jsonStream = File.OpenRead(jsonPath);
            var content = await JsonSerializer.DeserializeAsync(
                jsonStream,
                WechatSyncInputJsonContext.Default.ContentProjectionDocument,
                cancellationToken);
            if (content is null)
            {
                logger.Warn($"plugin wechat-sync content json invalid: {jsonUrl}");
                continue;
            }

            var routeUrl = string.IsNullOrWhiteSpace(content.Route) ? document.Route : content.Route;
            var htmlUrl = document.Representations
                .FirstOrDefault(x => x.Kind.Equals("html", StringComparison.OrdinalIgnoreCase))
                ?.Url;
            var htmlLocatorUrl = ResolveHtmlLocatorUrl(htmlUrl, routeUrl, siteUrl);
            var outputPath = InferHtmlOutputPath(htmlLocatorUrl, baseUrl);
            var renderedHtml = TryReadRenderedHtml(outputDir, outputPath);
            var bodyHtml = !string.IsNullOrWhiteSpace(content.Body) ? content.Body : renderedHtml;

            var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["sourceKey"] = content.Source ?? document.Source ?? content.Collection,
                ["source"] = content.Source ?? document.Source ?? content.Collection,
                ["sourceId"] = content.Id,
                ["summary"] = content.Summary ?? string.Empty,
                ["originalSource"] = content.OriginalSource ?? string.Empty,
                ["syncStatus"] = content.SyncStatus ?? string.Empty,
                ["manifestReviewStatus"] = document.ReviewStatus ?? string.Empty,
                ["reviewStatus"] = content.ReviewStatus ?? string.Empty
            };
            if (content.ExpiresAt is { } expiresAt)
            {
                meta["expiresAt"] = expiresAt;
            }

            var fields = new Dictionary<string, WechatSyncField>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", content.Type),
                ["collection"] = new("text", content.Collection),
                ["language"] = new("text", content.Language)
            };

            var media = content.Media ?? [];
            var cover = media.FirstOrDefault(x =>
                x.Kind.Equals("image", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(x.Url));
            if (cover is not null)
            {
                fields["cover"] = new WechatSyncField("text", cover.Url);
            }

            var item = new WechatSyncItem(
                content.Id,
                content.Title,
                content.Slug,
                content.PublishedAt,
                bodyHtml,
                meta,
                fields);
            routed.Add((item, new WechatSyncRoute(routeUrl, outputPath, string.Empty)));
        }

        return new WechatSyncContext
        {
            RootDir = rootDir,
            OutputDir = outputDir,
            BaseUrl = baseUrl,
            SiteName = siteName,
            SiteUrl = siteUrl,
            MediaDownloadDir = mediaDownloadDir,
            Routed = routed,
            Logger = logger
        };
    }

    internal static string ResolveUnderRoot(string rootDir, string workingDir, string value, string name)
    {
        var combined = Path.IsPathRooted(value)
            ? value
            : Path.Combine(workingDir, value);
        var full = Path.GetFullPath(combined);
        var root = Path.GetFullPath(rootDir);
        if (!PathUtils.IsSameOrSubPathOf(full, root))
        {
            throw new InvalidOperationException($"{name} must stay under the project root.");
        }

        return full;
    }

    private static string ResolveHtmlLocatorUrl(string? htmlUrl, string routeUrl, string? siteUrl)
    {
        if (string.IsNullOrWhiteSpace(htmlUrl))
        {
            return routeUrl;
        }

        var candidate = htmlUrl.Trim();
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return candidate;
        }

        if (!Uri.TryCreate(siteUrl, UriKind.Absolute, out var siteUri) ||
            !string.Equals(uri.Authority, siteUri.Authority, StringComparison.OrdinalIgnoreCase))
        {
            return routeUrl;
        }

        return candidate;
    }

    internal static string InferHtmlOutputPath(string routeUrl, string? baseUrl = null)
    {
        var route = NormalizeHtmlLocatorUrl(routeUrl, baseUrl);
        if (route.Length == 0 || route == "/")
        {
            return "index.html";
        }

        route = route.TrimStart('/');
        if (route.EndsWith('/'))
        {
            return route + "index.html";
        }

        return Path.HasExtension(route)
            ? route
            : route + "/index.html";
    }

    private static string NormalizeHtmlLocatorUrl(string? url, string? baseUrl)
    {
        var route = (url ?? string.Empty).Trim();
        if (route.Length == 0)
        {
            return string.Empty;
        }

        if (Uri.TryCreate(route, UriKind.Absolute, out var uri) &&
            (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            route = uri.AbsolutePath;
        }

        var normalizedBase = string.IsNullOrWhiteSpace(baseUrl) ? "/" : baseUrl.Trim();
        if (!normalizedBase.StartsWith('/'))
        {
            normalizedBase = "/" + normalizedBase;
        }

        normalizedBase = normalizedBase.Length > 1 ? normalizedBase.TrimEnd('/') : "/";
        if (normalizedBase == "/")
        {
            return route;
        }

        if (route.Equals(normalizedBase, StringComparison.OrdinalIgnoreCase))
        {
            return "/";
        }

        return route.StartsWith(normalizedBase + "/", StringComparison.OrdinalIgnoreCase)
            ? route[normalizedBase.Length..]
            : route;
    }

    private static string ResolveOutputPath(string outputDir, string relativePath, string name)
    {
        var rel = (relativePath ?? string.Empty).Trim().TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(outputDir, rel));
        var root = Path.GetFullPath(outputDir);
        if (!PathUtils.IsSameOrSubPathOf(full, root))
        {
            throw new InvalidOperationException($"{name} must stay under the build output directory.");
        }

        return full;
    }

    private static string? TryReadRenderedHtml(string outputDir, string outputPath)
    {
        var path = ResolveOutputPath(outputDir, outputPath, "rendered html");
        if (!File.Exists(path))
        {
            return null;
        }

        return File.ReadAllText(path);
    }
}
