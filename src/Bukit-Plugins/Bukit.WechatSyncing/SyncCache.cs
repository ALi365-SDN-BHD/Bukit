using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bukit.Shared;

namespace Bukit.WechatSyncing;

public sealed record SyncCache(int Version, Dictionary<string, SyncRecord> Records)
{
    public Dictionary<string, string> ThumbMediaIds { get; init; } = new(StringComparer.Ordinal);
}

public sealed record SyncRecord(
    DateTimeOffset LastSuccessAt,
    string WechatDraftId,
    string ContentHash,
    string SourceKey,
    string SourceId,
    string Title);

internal static class SyncCacheManager
{
    internal static string ResolvePath(string rootDir, string cacheFile)
    {
        var path = Path.IsPathRooted(cacheFile)
            ? Path.GetFullPath(cacheFile)
            : Path.GetFullPath(Path.Combine(rootDir, cacheFile));

        var root = Path.GetFullPath(rootDir);
        if (!PathUtils.IsSameOrSubPathOf(path, root))
        {
            throw new InvalidOperationException("wechat-sync cacheFile must stay under the project root.");
        }

        var cacheRoot = Path.GetFullPath(Path.Combine(root, ".cache", "wechat-sync"));
        if (!PathUtils.IsSubPathOf(path, cacheRoot))
        {
            throw new InvalidOperationException("wechat-sync cacheFile must stay under .cache/wechat-sync.");
        }

        return path;
    }

    internal static SyncCache LoadCache(string path, Bukit.Shared.ILogger logger)
    {
        if (!File.Exists(path))
        {
            return CreateEmpty();
        }

        try
        {
            var text = File.ReadAllText(path);
            var cache = JsonSerializer.Deserialize(text, WechatSyncJsonContext.Default.SyncCache);
            if (cache is null || cache.Records is null)
            {
                return CreateEmpty();
            }

            var thumbs = new Dictionary<string, string>(cache.ThumbMediaIds, StringComparer.Ordinal);

            return cache with
            {
                Version = 2,
                Records = new Dictionary<string, SyncRecord>(cache.Records, StringComparer.Ordinal),
                ThumbMediaIds = thumbs
            };
        }
        catch (Exception ex)
        {
            logger.Warn($"plugin wechat-sync cache parse failed, reset cache: {ex.Message}");
            return CreateEmpty();
        }
    }

    internal static void SaveCache(string path, SyncCache cache)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(cache, WechatSyncJsonContext.Default.SyncCache));
    }

    internal static string ComputeContentHash(
        WechatSyncItem item,
        WechatSyncRoute route,
        string html,
        WechatSyncOptions options,
        WechatSyncContext? context = null)
    {
        using var sha = SHA256.Create();
        var author = string.IsNullOrWhiteSpace(options.Author) ? options.SiteName : options.Author;
        var contentSourceUrl = WechatSyncHelpers.CombineAbsoluteUrl(options.SiteUrl, options.BaseUrl, route.Url);
        var summary = WechatSyncHelpers.ReadMetaString(item.Metadata, "summary");
        var thumbSource = ThumbResolver.ResolveThumbSource(item, options) ?? string.Empty;
        var mediaFingerprint = ComputeMediaFingerprint(context, item, html, options);
        var payload = string.Join('\n',
            "wechat-sync-cache-v3",
            item.Id,
            item.Title ?? string.Empty,
            html,
            route.Url,
            summary,
            author,
            contentSourceUrl,
            thumbSource,
            options.DefaultThumbMediaId ?? string.Empty,
            options.DefaultImageUrl ?? string.Empty,
            options.NeedOpenComment.ToString(),
            options.OnlyFansCanComment.ToString(),
            options.SiteUrl ?? string.Empty,
            options.BaseUrl,
            options.ProcessImages.ToString(),
            options.Passthrough.ToString(),
            options.Target,
            mediaFingerprint);
        var bytes = Encoding.UTF8.GetBytes(payload);
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }

    internal static string ComputeFileSignature(string path)
    {
        var fullPath = Path.GetFullPath(path).Replace('\\', '/');
        try
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
            return $"{fullPath}|sha256:{hash}";
        }
        catch (FileNotFoundException)
        {
            return $"{fullPath}|missing";
        }
        catch (DirectoryNotFoundException)
        {
            return $"{fullPath}|missing";
        }
        catch (IOException ex)
        {
            return $"{fullPath}|unreadable:{ex.GetType().Name}";
        }
        catch (UnauthorizedAccessException ex)
        {
            return $"{fullPath}|unreadable:{ex.GetType().Name}";
        }
    }

    private static string ComputeMediaFingerprint(
        WechatSyncContext? context,
        WechatSyncItem item,
        string html,
        WechatSyncOptions options)
    {
        if (context is null)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        AddMediaFingerprint(parts, "thumb", context, ThumbResolver.ResolveThumbSource(item, options), options);

        if (options.ProcessImages && !string.IsNullOrWhiteSpace(html))
        {
            foreach (Match match in Regex.Matches(html, @"<img\b[^>]*/?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                AddMediaFingerprint(parts, "inline", context, ContentImageProcessor.ResolveBestImageUrl(match.Value), options);
            }
        }

        return string.Join('\n', parts.OrderBy(x => x, StringComparer.Ordinal));
    }

    private static void AddMediaFingerprint(
        List<string> parts,
        string kind,
        WechatSyncContext context,
        string? source,
        WechatSyncOptions options)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        source = source.Trim();
        if (source.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(source, @"\.svg(\?|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return;
        }

        if (ThumbResolver.TryResolveLocalAssetPath(context, source, out var localPath))
        {
            parts.Add($"{kind}:local:{ComputeFileSignature(localPath)}");
            return;
        }

        if (!WechatSyncHelpers.TryNormalizeToAbsoluteUrl(source, options.SiteUrl, options.BaseUrl, out var absoluteUrl) ||
            !WechatSyncHelpers.IsHttpUrl(absoluteUrl))
        {
            return;
        }

        var normalizedKey = WechatSyncHelpers.NormalizeMediaSourceUrlKey(absoluteUrl);
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return;
        }

        var downloadDir = ThumbResolver.ResolveEffectiveMediaDownloadDir(context);
        var mediaPath = ThumbResolver.TryResolveFromMediaIndex(downloadDir, normalizedKey)
            ?? ThumbResolver.TryResolveFromMediaHashName(downloadDir, normalizedKey, absoluteUrl);
        if (!string.IsNullOrWhiteSpace(mediaPath))
        {
            parts.Add($"{kind}:media-cache:{normalizedKey}:{ComputeFileSignature(mediaPath)}");
        }
    }

    private static SyncCache CreateEmpty()
    {
        return new SyncCache(2, new Dictionary<string, SyncRecord>(StringComparer.Ordinal));
    }
}
