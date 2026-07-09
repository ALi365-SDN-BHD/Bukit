using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        if (Path.IsPathFullyQualified(relative) ||
            relative.Equals("..", StringComparison.Ordinal) ||
            relative.StartsWith("../", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("wechat-sync cacheFile must stay under the project root.");
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
        WechatSyncOptions options)
    {
        using var sha = SHA256.Create();
        var author = string.IsNullOrWhiteSpace(options.Author) ? options.SiteName : options.Author;
        var contentSourceUrl = WechatSyncHelpers.CombineAbsoluteUrl(options.SiteUrl, options.BaseUrl, route.Url);
        var summary = WechatSyncHelpers.ReadMetaString(item.Metadata, "summary");
        var thumbSource = ThumbResolver.ResolveThumbSource(item, options) ?? string.Empty;
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
            options.Target);
        var bytes = Encoding.UTF8.GetBytes(payload);
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }

    private static SyncCache CreateEmpty()
    {
        return new SyncCache(2, new Dictionary<string, SyncRecord>(StringComparer.Ordinal));
    }
}
