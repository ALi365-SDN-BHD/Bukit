using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
    DateTimeOffset UpdatedAt)
{
    public string? SourceKey { get; init; }
    public string? SourceId { get; init; }
    public string? Title { get; init; }
    public int? LastPublishStatus { get; init; }
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
    internal static readonly TimeSpan DefaultRunLockTimeout = TimeSpan.FromMinutes(2);

    internal static string ResolvePath(string rootDir, string cacheFile)
    {
        var path = Path.IsPathRooted(cacheFile)
            ? Path.GetFullPath(cacheFile)
            : Path.GetFullPath(Path.Combine(rootDir, cacheFile));

        var root = Path.GetFullPath(rootDir);
        var cacheRoot = Path.GetFullPath(Path.Combine(root, ".cache", "wechat-sync"));
        EnsureNoSymbolicLinks(root, path, "cache path");
        if (!PathUtils.IsSameOrSubPathOf(path, root))
        {
            throw new InvalidOperationException("wechat-sync cacheFile must stay under the project root.");
        }

        if (!PathUtils.IsSubPathOf(path, cacheRoot))
        {
            throw new InvalidOperationException("wechat-sync cacheFile must stay under .cache/wechat-sync.");
        }

        return path;
    }

    internal static async Task<RunLockHandle> AcquireRunLockAsync(
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

        var guardPath = Path.Combine(cacheDir, $".{Path.GetFileName(cachePath)}.{Guid.NewGuid():N}.run-guard");
        ValidateManagedPath(rootDir, guardPath, "cache directory identity guard");
        var guardToken = RandomNumberGenerator.GetBytes(32);
        FileStream? guardStream = null;
        RunLockHandle? handle = null;
        try
        {
            guardStream = new FileStream(
                guardPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.Read | FileShare.Delete,
                bufferSize: 1,
                FileOptions.WriteThrough | FileOptions.DeleteOnClose);
            guardStream.Write(guardToken);
            guardStream.Flush(flushToDisk: true);
            handle = new RunLockHandle(rootDir, cacheDir, guardPath, guardToken, guardStream);
            guardStream = null;
            handle.ValidateIdentity();

            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                handle.ValidateIdentity();
                ValidateManagedPath(rootDir, lockPath, "lock file");
                FileStream? lockStream = null;
                try
                {
                    lockStream = new FileStream(
                        lockPath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        bufferSize: 1,
                        FileOptions.Asynchronous);
                    handle.ValidateIdentity();
                    ValidateManagedPath(rootDir, lockPath, "lock file");
                    handle.AttachLock(lockStream);
                    lockStream = null;
                    return handle;
                }
                catch (IOException ex)
                {
                    lockStream?.Dispose();
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
                catch
                {
                    lockStream?.Dispose();
                    throw;
                }
            }
        }
        catch
        {
            if (handle is not null)
            {
                await handle.DisposeAsync();
            }
            else if (guardStream is not null)
            {
                await guardStream.DisposeAsync();
            }

            throw;
        }
    }

    internal sealed class RunLockHandle : IAsyncDisposable
    {
        private readonly string _rootDir;
        private readonly string _cacheDir;
        private readonly string _guardPath;
        private readonly byte[] _guardToken;
        private readonly FileStream _guardStream;
        private FileStream? _lockStream;

        internal RunLockHandle(
            string rootDir,
            string cacheDir,
            string guardPath,
            byte[] guardToken,
            FileStream guardStream)
        {
            _rootDir = rootDir;
            _cacheDir = cacheDir;
            _guardPath = guardPath;
            _guardToken = guardToken;
            _guardStream = guardStream;
        }

        internal void AttachLock(FileStream lockStream)
        {
            if (_lockStream is not null)
            {
                throw new InvalidOperationException("wechat-sync run lock is already attached.");
            }

            _lockStream = lockStream;
        }

        internal void ValidateIdentity()
        {
            try
            {
                ValidateManagedPath(_rootDir, _cacheDir, "cache directory");
                ValidateManagedPath(_rootDir, _guardPath, "cache directory identity guard");
                var currentToken = new byte[_guardToken.Length];
                using var stream = new FileStream(
                    _guardPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length != currentToken.Length)
                {
                    throw new InvalidOperationException("cache directory identity guard length changed");
                }

                stream.ReadExactly(currentToken);
                if (!CryptographicOperations.FixedTimeEquals(currentToken, _guardToken))
                {
                    throw new InvalidOperationException("cache directory identity guard changed");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                throw new InvalidOperationException(
                    "wechat-sync cache directory changed while the run lock was being acquired or held.",
                    ex);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_lockStream is not null)
            {
                await _lockStream.DisposeAsync();
            }

            await _guardStream.DisposeAsync();
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

            CommitAtomicReplacement(tempPath, path);
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

    internal static void CommitAtomicReplacement(string tempPath, string destinationPath)
    {
        if (OperatingSystem.IsWindows())
        {
            const uint moveFileReplaceExisting = 0x1;
            const uint moveFileWriteThrough = 0x8;
            if (!NativeMethods.MoveFileEx(
                    tempPath,
                    destinationPath,
                    moveFileReplaceExisting | moveFileWriteThrough))
            {
                var error = Marshal.GetLastPInvokeError();
                throw new IOException(
                    $"Could not durably replace wechat-sync cache '{destinationPath}'.",
                    new Win32Exception(error));
            }

            return;
        }

        File.Move(tempPath, destinationPath, overwrite: true);
        var directory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException("wechat-sync cache path must have a parent directory.");
        FlushParentDirectoryMetadata(directory);
    }

    internal static void FlushParentDirectoryMetadata(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Windows cache replacement durability is provided by MoveFileEx with MOVEFILE_WRITE_THROUGH.");
        }

        var descriptor = NativeMethods.Open(directory, flags: 0);
        if (descriptor < 0)
        {
            var error = Marshal.GetLastPInvokeError();
            throw new IOException(
                $"Could not open wechat-sync cache directory '{directory}' for metadata flush.",
                new Win32Exception(error));
        }

        try
        {
            if (NativeMethods.Fsync(descriptor) != 0)
            {
                var error = Marshal.GetLastPInvokeError();
                throw new IOException(
                    $"Could not flush wechat-sync cache directory '{directory}' metadata.",
                    new Win32Exception(error));
            }
        }
        finally
        {
            _ = NativeMethods.Close(descriptor);
        }
    }

    private static class NativeMethods
    {
        [DllImport("libc", EntryPoint = "open", SetLastError = true)]
        internal static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

        [DllImport("libc", EntryPoint = "fsync", SetLastError = true)]
        internal static extern int Fsync(int descriptor);

        [DllImport("libc", EntryPoint = "close", SetLastError = true)]
        internal static extern int Close(int descriptor);

        [DllImport("kernel32.dll", EntryPoint = "MoveFileExW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool MoveFileEx(string existingFileName, string newFileName, uint flags);
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
                entry.Value.LastSuccessAt == default ||
                string.IsNullOrWhiteSpace(entry.Value.WechatDraftId) ||
                string.IsNullOrWhiteSpace(entry.Value.ContentHash) ||
                entry.Value.SourceKey is null ||
                entry.Value.SourceId is null ||
                entry.Value.Title is null))
        {
            throw new InvalidDataException("Cache contains an invalid sync record.");
        }

        if (cache.ThumbMediaIds.Any(entry =>
                string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Value)))
        {
            throw new InvalidDataException("Cache contains an invalid thumb media record.");
        }

        if (cache.Version != 3)
        {
            return;
        }

        foreach (var entry in cache.Operations)
        {
            var operation = entry.Value;
            if (string.IsNullOrWhiteSpace(entry.Key) ||
                operation is null ||
                string.IsNullOrWhiteSpace(operation.State) ||
                string.IsNullOrWhiteSpace(operation.ContentHash) ||
                operation.Target is not ("draft" or "publish") ||
                operation.UpdatedAt == default ||
                operation.SourceKey is null ||
                operation.SourceId is null ||
                operation.Title is null ||
                operation.DraftId is not null && string.IsNullOrWhiteSpace(operation.DraftId) ||
                operation.PublishId is not null && string.IsNullOrWhiteSpace(operation.PublishId) ||
                !HasValidOperationState(operation))
            {
                throw new InvalidDataException("Cache contains an invalid operation record.");
            }
        }
    }

    private static bool HasValidOperationState(SyncOperation operation)
        => operation.State switch
        {
            "DraftSubmitting" =>
                operation.DraftId is null &&
                operation.PublishId is null &&
                operation.LastPublishStatus is null,
            "DraftCreated" =>
                !string.IsNullOrWhiteSpace(operation.DraftId) &&
                operation.PublishId is null &&
                operation.LastPublishStatus is null,
            "PublishSubmitting" =>
                operation.Target == "publish" &&
                !string.IsNullOrWhiteSpace(operation.DraftId) &&
                operation.PublishId is null &&
                operation.LastPublishStatus is null,
            "PublishSubmitted" =>
                operation.Target == "publish" &&
                !string.IsNullOrWhiteSpace(operation.DraftId) &&
                !string.IsNullOrWhiteSpace(operation.PublishId) &&
                operation.LastPublishStatus is null or 1,
            "PublishFailed" =>
                operation.Target == "publish" &&
                !string.IsNullOrWhiteSpace(operation.DraftId) &&
                !string.IsNullOrWhiteSpace(operation.PublishId) &&
                operation.LastPublishStatus is >= 2 and <= 6,
            _ => false
        };

    private static void ValidateManagedPath(string rootDir, string path, string kind)
    {
        var root = Path.GetFullPath(rootDir);
        var cacheRoot = Path.GetFullPath(Path.Combine(root, ".cache", "wechat-sync"));
        EnsureNoSymbolicLinks(root, path, kind);
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

    private static void EnsureNoSymbolicLinks(string rootDir, string path, string kind)
    {
        var root = Path.GetFullPath(rootDir);
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(root, fullPath);
        if (Path.IsPathRooted(relative) ||
            relative.Equals("..", PlatformPathHelper.PathComparison) ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, PlatformPathHelper.PathComparison) ||
            relative.StartsWith(".." + Path.AltDirectorySeparatorChar, PlatformPathHelper.PathComparison))
        {
            throw new InvalidOperationException($"wechat-sync {kind} must stay under the project root.");
        }

        var current = root;
        ThrowIfSymbolicLinkOrReparsePoint(current, kind);
        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            ThrowIfSymbolicLinkOrReparsePoint(current, kind);

            if (!File.Exists(current) && !Directory.Exists(current))
            {
                break;
            }
        }
    }

    private static void ThrowIfSymbolicLinkOrReparsePoint(string path, string kind)
    {
        var file = new FileInfo(path);
        var directory = new DirectoryInfo(path);
        if (file.LinkTarget is not null ||
            directory.LinkTarget is not null ||
            (file.Exists || directory.Exists) &&
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                $"wechat-sync {kind} must not contain a symbolic link or reparse point: '{path}'.");
        }
    }
}
