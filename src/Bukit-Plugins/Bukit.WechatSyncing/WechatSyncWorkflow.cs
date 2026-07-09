using System.Text;

namespace Bukit.WechatSyncing;

using static WechatSyncHelpers;

public sealed class WechatSyncWorkflow
{
    public const string PluginId = "wechat-sync";
    public const string Version = "0.2.0";

    private readonly IWechatDraftGateway? _gateway;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly Func<string, CancellationToken, Task<byte[]>>? _downloadImageAsync;

    public WechatSyncWorkflow()
        : this(null, null, null)
    {
    }

    public WechatSyncWorkflow(
        IWechatDraftGateway? gateway,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        Func<string, CancellationToken, Task<byte[]>>? downloadImageAsync = null)
    {
        _gateway = gateway;
        _delayAsync = delayAsync ?? ((delay, ct) => Task.Delay(delay, ct));
        _downloadImageAsync = downloadImageAsync;
    }

    public async Task<WechatSyncResult> RunAsync(
        WechatSyncContext context,
        WechatSyncOptions options,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<WechatSyncMessage>();
        var diagnostics = new List<WechatSyncDiagnostic>();
        var cachePath = SyncCacheManager.ResolvePath(context.RootDir, options.CacheFile);
        var cache = SyncCacheManager.LoadCache(cachePath, context.Logger);
        var forceRetryIgnoreCache = options.Force || ReadTrueFromEnv(options.ForceRetryIgnoreCacheEnv);
        var filtered = FilterCandidates(context, options);
        if (filtered.Count == 0)
        {
            const string message = "plugin wechat-sync skipped: no candidate content matched filters";
            context.Logger.Info(message);
            messages.Add(new WechatSyncMessage("info", message));
            return new WechatSyncResult(true, 0, 0, 0, messages, diagnostics, cachePath);
        }

        var appId = ReadRequiredEnv(options.AppIdEnv);
        var appSecret = ReadRequiredEnv(options.AppSecretEnv);
        var ownsGateway = _gateway is null;
        var gateway = _gateway ?? new WechatDraftGateway(context.Logger, appId, appSecret);
        var downloadFunc = _downloadImageAsync ?? WechatDraftGateway.DefaultDownloadImageAsync;
        var thumbResolver = new ThumbResolver(gateway, downloadFunc, context.Logger);
        var imageProcessor = options.ProcessImages
            ? new ContentImageProcessor(gateway, downloadFunc, context.Logger)
            : null;

        var updated = false;
        var synced = 0;
        var skipped = 0;

        try
        {
            foreach (var candidate in filtered)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rawHtml = ContentBodyResolver.GetHtml(candidate.Item);
                var contentHash = SyncCacheManager.ComputeContentHash(candidate.Item, candidate.Route, rawHtml, options);
                if (!forceRetryIgnoreCache &&
                    cache.Records.TryGetValue(candidate.SyncKey, out var existing) &&
                    string.Equals(existing.ContentHash, contentHash, StringComparison.Ordinal))
                {
                    skipped++;
                    continue;
                }

                var (draftId, cacheUpdated) = await SyncWithRetryAsync(
                    context, gateway, thumbResolver, imageProcessor, candidate, options, cache, cancellationToken);

                if (options.Target == "publish" && !string.IsNullOrWhiteSpace(draftId))
                {
                    var publishSucceeded = await PublishWithPollingAsync(context, gateway, draftId, options, cancellationToken);
                    if (!publishSucceeded)
                    {
                        diagnostics.Add(new WechatSyncDiagnostic(
                            "plugin.wechat-sync.publishFailed",
                            "error",
                            $"wechat-sync publish failed for '{candidate.SyncKey}'."));
                        continue;
                    }
                }

                cache.Records[candidate.SyncKey] = new SyncRecord(
                    DateTimeOffset.UtcNow,
                    draftId,
                    contentHash,
                    candidate.SourceKey,
                    candidate.SourceId,
                    candidate.Item.Title ?? string.Empty);
                synced++;
                updated = true;
                updated |= cacheUpdated;
            }

            if (updated)
            {
                SyncCacheManager.SaveCache(cachePath, cache);
            }
        }
        catch
        {
            if (updated)
            {
                TrySavePartialCache(cachePath, cache, context.Logger);
            }

            throw;
        }
        finally
        {
            if (ownsGateway && gateway is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        var summary = $"plugin wechat-sync done: candidates={filtered.Count} synced={synced} skipped={skipped} forceIgnoreCache={forceRetryIgnoreCache}";
        context.Logger.Info(summary);
        messages.Add(new WechatSyncMessage("info", summary));

        return new WechatSyncResult(diagnostics.All(x => !x.Severity.Equals("error", StringComparison.OrdinalIgnoreCase)),
            filtered.Count, synced, skipped, messages, diagnostics, cachePath);
    }

    private async Task<(string DraftId, bool CacheUpdated)> SyncWithRetryAsync(
        WechatSyncContext context,
        IWechatDraftGateway gateway,
        ThumbResolver thumbResolver,
        ContentImageProcessor? imageProcessor,
        WechatSyncCandidate candidate,
        WechatSyncOptions options,
        SyncCache cache,
        CancellationToken cancellationToken)
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= options.MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var (thumbMediaId, thumbCacheUpdated) = await thumbResolver.ResolveAndUploadThumbAsync(
                    context, candidate.Item, options, cache, cancellationToken);

                var processedHtml = ContentBodyResolver.GetHtml(candidate.Item);
                if (!options.Passthrough)
                {
                    processedHtml = ContentProcessor.ProcessContent(processedHtml);

                    if (imageProcessor is not null)
                    {
                        processedHtml = await imageProcessor.ProcessImagesAsync(context, processedHtml, options, cancellationToken);
                    }
                }

                var req = BuildDraftRequest(candidate, options, thumbMediaId, processedHtml);

                if (req.ContentHtml is { Length: > 0 })
                {
                    if (req.ContentHtml.Length > WechatContentMaxChars)
                    {
                        context.Logger.Warn(
                            $"plugin wechat-sync content length {req.ContentHtml.Length} exceeds WeChat limit ({WechatContentMaxChars} chars) for '{candidate.SyncKey}'");
                    }

                    var contentBytes = Encoding.UTF8.GetByteCount(req.ContentHtml);
                    if (contentBytes > WechatContentMaxBytes)
                    {
                        context.Logger.Warn(
                            $"plugin wechat-sync content size {contentBytes} bytes exceeds WeChat limit ({WechatContentMaxBytes} bytes) for '{candidate.SyncKey}'");
                    }
                }

                var draftId = await gateway.AddDraftAsync(req, cancellationToken);
                return (draftId, thumbCacheUpdated);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                last = ex;
                if (attempt >= options.MaxAttempts)
                {
                    break;
                }

                var sleep = ComputeDelay(options.BaseDelayMs, options.BackoffFactor, attempt);
                await _delayAsync(sleep, cancellationToken);
            }
        }

        throw new InvalidOperationException($"wechat-sync failed for '{candidate.SyncKey}' after {options.MaxAttempts} attempts: {last?.Message}", last);
    }

    private static void TrySavePartialCache(string cachePath, SyncCache cache, Bukit.Shared.ILogger logger)
    {
        try
        {
            SyncCacheManager.SaveCache(cachePath, cache);
        }
        catch (Exception ex)
        {
            logger.Warn($"plugin wechat-sync partial cache save failed after sync error: {ex.Message}");
        }
    }

    private async Task<bool> PublishWithPollingAsync(
        WechatSyncContext context,
        IWechatDraftGateway gateway,
        string draftMediaId,
        WechatSyncOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var publishId = await gateway.PublishAsync(draftMediaId, cancellationToken);
            context.Logger.Info($"plugin wechat-sync publish submitted: publishId={publishId}");

            var maxPolls = options.PublishPollMaxAttempts;
            for (var poll = 0; poll < maxPolls; poll++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _delayAsync(TimeSpan.FromSeconds(options.PublishPollIntervalSeconds), cancellationToken);

                var status = await gateway.CheckPublishStatusAsync(publishId, cancellationToken);

                if (status.PublishStatus == 0)
                {
                    context.Logger.Info($"plugin wechat-sync publish succeeded: publishId={publishId} articleUrl={status.ArticleUrl}");
                    return true;
                }

                if (status.PublishStatus >= 2)
                {
                    context.Logger.Warn($"plugin wechat-sync publish failed: publishId={publishId} status={status.PublishStatus}");
                    return false;
                }

                context.Logger.Info($"plugin wechat-sync publish in progress: publishId={publishId} poll={poll + 1}/{maxPolls}");
            }

            context.Logger.Warn($"plugin wechat-sync publish status poll timeout: publishId={publishId} after {maxPolls} polls");
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Logger.Warn($"plugin wechat-sync publish failed: {ex.Message}");
            return false;
        }
    }

    private static WechatDraftRequest BuildDraftRequest(
        WechatSyncCandidate candidate,
        WechatSyncOptions options,
        string thumbMediaId,
        string processedHtml)
    {
        var summary = candidate.Item.Metadata.TryGetValue("summary", out var summaryObj) ? summaryObj?.ToString() : null;
        var digest = string.IsNullOrWhiteSpace(summary)
            ? StripHtml(processedHtml, WechatDigestMaxChars)
            : Truncate(summary!.Trim(), WechatDigestMaxChars);

        var title = Truncate(candidate.Item.Title ?? string.Empty, WechatTitleMaxChars);
        var contentSourceUrl = CombineAbsoluteUrl(options.SiteUrl, options.BaseUrl, candidate.Route.Url);
        var author = string.IsNullOrWhiteSpace(options.Author) ? options.SiteName : options.Author;

        return new WechatDraftRequest(
            title,
            author,
            digest,
            processedHtml,
            contentSourceUrl,
            thumbMediaId,
            options.NeedOpenComment,
            options.OnlyFansCanComment);
    }

    private static List<WechatSyncCandidate> FilterCandidates(WechatSyncContext context, WechatSyncOptions options)
    {
        var list = new List<WechatSyncCandidate>();
        foreach (var (item, route) in context.Routed)
        {
            if (ReadMetaString(item.Metadata, "sourceMode").Equals("data", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var sourceKey = ReadMetaString(item.Metadata, "sourceKey");
            if (string.IsNullOrWhiteSpace(sourceKey))
            {
                sourceKey = ReadMetaString(item.Metadata, "source");
            }

            if (options.SourceNames.Count > 0 && !options.SourceNames.Contains(sourceKey))
            {
                continue;
            }

            var fieldType = ReadFieldType(item.Fields);
            if (!MatchesType(fieldType, options.ContentTypes, options.DefaultTypesWhenMissing))
            {
                continue;
            }

            var sourceId = ReadMetaString(item.Metadata, "sourceId");
            var syncKey = !string.IsNullOrWhiteSpace(sourceKey) && !string.IsNullOrWhiteSpace(sourceId)
                ? $"{sourceKey}:{sourceId}"
                : item.Id;

            list.Add(new WechatSyncCandidate(syncKey, sourceKey, sourceId, item, route));
        }

        return list;
    }

    private static bool MatchesType(string? type, HashSet<string> contentTypes, HashSet<string> defaultTypesWhenMissing)
    {
        if (contentTypes.Count == 0)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            return contentTypes.Contains(type);
        }

        foreach (var fallback in defaultTypesWhenMissing)
        {
            if (contentTypes.Contains(fallback))
            {
                return true;
            }
        }

        return false;
    }
}
