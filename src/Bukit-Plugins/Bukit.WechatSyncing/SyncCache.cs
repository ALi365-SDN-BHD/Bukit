using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Bukit.Shared;

namespace Bukit.WechatSyncing;

public sealed record SyncCache(int Version, Dictionary<string, SyncRecord> Records)
{
    public Dictionary<string, string> ThumbMediaIds { get; init; } = new(StringComparer.Ordinal);

    [JsonInclude]
    internal Dictionary<string, SyncOperation> Operations { get; init; } = new(StringComparer.Ordinal);
}

internal sealed record SyncOperation(
    string State,
    string ContentHash,
    string Target,
    string? DraftId,
    string? PublishId,
    DateTimeOffset UpdatedAt);

public sealed record SyncRecord(
    DateTimeOffset LastSuccessAt,
    string WechatDraftId,
    string ContentHash,
    string SourceKey,
    string SourceId,
    string Title);

internal static class SyncCacheManager
{
    internal static readonly TimeSpan DefaultRunLockTimeout = TimeSpan.FromMinutes(2);

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

    internal static async Task<FileStream> AcquireRunLockAsync(
        string rootDir,
        string cachePath,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "wechat-sync lock timeout must not be negative.");
        }

        var cacheDir = Path.GetDirectoryName(cachePath);
        if (string.IsNullOrWhiteSpace(cacheDir))
        {
            throw new InvalidOperationException("wechat-sync cache path must have a parent directory.");
        }

        ValidateManagedPath(rootDir, cacheDir, "cache directory");
        Directory.CreateDirectory(cacheDir);
        ValidateManagedPath(rootDir, cacheDir, "cache directory");

        var lockPath = cachePath + ".lock";
        ValidateManagedPath(rootDir, cachePath, "cache file");
        ValidateManagedPath(rootDir, lockPath, "lock file");

        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateManagedPath(rootDir, lockPath, "lock file");
            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.Asynchronous);
            }
            catch (IOException ex)
            {
                var remaining = timeout - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    throw new TimeoutException(
                        $"wechat-sync timed out after {timeout} waiting for cache lock '{lockPath}'.",
                        ex);
                }

                var delay = remaining < TimeSpan.FromMilliseconds(50)
                    ? remaining
                    : TimeSpan.FromMilliseconds(50);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    internal static SyncCache LoadCache(string path, Bukit.Shared.ILogger logger)
    {
        ValidateCacheLocation(path, "cache file");
        if (!File.Exists(path))
        {
            return CreateEmpty();
        }

        _ = logger;
        try
        {
            var text = File.ReadAllText(path);
            var cache = JsonSerializer.Deserialize(text, WechatSyncJsonContext.Default.SyncCache);
            if (cache is null)
            {
                throw new InvalidDataException("Cache document is empty.");
            }

            ValidateCache(cache);

            var thumbs = new Dictionary<string, string>(cache.ThumbMediaIds, StringComparer.Ordinal);
            var operations = cache.Version == 2
                ? new Dictionary<string, SyncOperation>(StringComparer.Ordinal)
                : new Dictionary<string, SyncOperation>(cache.Operations, StringComparer.Ordinal);

            return cache with
            {
                Version = 3,
                Records = new Dictionary<string, SyncRecord>(cache.Records, StringComparer.Ordinal),
                ThumbMediaIds = thumbs,
                Operations = operations
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"wechat-sync cache '{path}' is invalid or uses an unsupported version; repair or remove the cache file before retrying.",
                ex);
        }
    }

    internal static void SaveCache(string path, SyncCache cache)
    {
        ValidateCacheLocation(path, "cache file");
        var dir = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(dir))
        {
            throw new InvalidOperationException("wechat-sync cache path must have a parent directory.");
        }

        Directory.CreateDirectory(dir);
        ValidateCacheLocation(path, "cache file");
        var tempPath = Path.Combine(dir, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        ValidateCacheLocation(tempPath, "temporary cache file");
        var ownsTemp = false;
        try
        {
            using (var stream = new FileStream(
                       tempPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            {
                ownsTemp = true;
                using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true))
                {
                    writer.Write(JsonSerializer.Serialize(cache, WechatSyncJsonContext.Default.SyncCache));
                    writer.Flush();
                }

                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, path, overwrite: true);
            ownsTemp = false;
        }
        finally
        {
            if (ownsTemp)
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Preserve the primary save failure; this call owns no other temp path.
                }
            }
        }
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
        AddMediaFingerprint(parts, "default", context, options.DefaultImageUrl, options);

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

        if (ThumbResolver.TryResolveLocalAssetPath(context, absoluteUrl, out localPath))
        {
            parts.Add($"{kind}:local:{ComputeFileSignature(localPath)}");
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
        return new SyncCache(3, new Dictionary<string, SyncRecord>(StringComparer.Ordinal))
        {
            ThumbMediaIds = new Dictionary<string, string>(StringComparer.Ordinal),
            Operations = new Dictionary<string, SyncOperation>(StringComparer.Ordinal)
        };
    }

    private static void ValidateCache(SyncCache cache)
    {
        if (cache.Version is not (2 or 3))
        {
            throw new InvalidDataException($"Unsupported cache version '{cache.Version}'.");
        }

        if (cache.Records is null || cache.ThumbMediaIds is null || (cache.Version == 3 && cache.Operations is null))
        {
            throw new InvalidDataException("Cache collections must not be null.");
        }

        if (cache.Records.Any(entry =>
                string.IsNullOrWhiteSpace(entry.Key) ||
                entry.Value is null ||
                entry.Value.WechatDraftId is null ||
                entry.Value.ContentHash is null ||
                entry.Value.SourceKey is null ||
                entry.Value.SourceId is null ||
                entry.Value.Title is null))
        {
            throw new InvalidDataException("Cache contains an invalid sync record.");
        }

        if (cache.ThumbMediaIds.Any(entry =>
                string.IsNullOrWhiteSpace(entry.Key) || entry.Value is null))
        {
            throw new InvalidDataException("Cache contains an invalid thumb media record.");
        }

        if (cache.Version == 3 && cache.Operations.Any(entry =>
                string.IsNullOrWhiteSpace(entry.Key) ||
                entry.Value is null ||
                string.IsNullOrWhiteSpace(entry.Value.State) ||
                entry.Value.ContentHash is null ||
                entry.Value.Target is null))
        {
            throw new InvalidDataException("Cache contains an invalid operation record.");
        }
    }

    private static void ValidateManagedPath(string rootDir, string path, string kind)
    {
        var root = Path.GetFullPath(rootDir);
        var cacheRoot = Path.GetFullPath(Path.Combine(root, ".cache", "wechat-sync"));
        if (!PathUtils.IsSameOrSubPathOf(path, root) || !PathUtils.IsSameOrSubPathOf(path, cacheRoot))
        {
            throw new InvalidOperationException($"wechat-sync {kind} must stay under the project .cache/wechat-sync directory.");
        }
    }

    private static void ValidateCacheLocation(string path, string kind)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = new DirectoryInfo(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("wechat-sync cache path must have a parent directory."));

        for (var current = directory; current is not null; current = current.Parent)
        {
            if (!current.Name.Equals("wechat-sync", PlatformPathHelper.PathComparison) ||
                current.Parent is null ||
                !current.Parent.Name.Equals(".cache", PlatformPathHelper.PathComparison) ||
                current.Parent.Parent is null)
            {
                continue;
            }

            ValidateManagedPath(current.Parent.Parent.FullName, fullPath, kind);
            return;
        }

        throw new InvalidOperationException(
            $"wechat-sync {kind} must stay under the project .cache/wechat-sync directory.");
    }
}
