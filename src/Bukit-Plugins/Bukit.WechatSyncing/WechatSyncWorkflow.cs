using System.Text;

namespace Bukit.WechatSyncing;

using static WechatSyncHelpers;

public sealed class WechatSyncWorkflow
{
    public const string PluginId = "wechat-sync";
    public const string Version = "0.4.0";

    private readonly IWechatDraftGateway? _gateway;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly Func<string, CancellationToken, Task<byte[]>>? _downloadImageAsync;
    private readonly TimeSpan _runLockTimeout;
    private readonly Action<string, SyncCache> _saveCache;
    private readonly Func<DateTimeOffset> _utcNow;

    public WechatSyncWorkflow()
        : this(null, null, null)
    {
    }

    public WechatSyncWorkflow(
        IWechatDraftGateway? gateway,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        Func<string, CancellationToken, Task<byte[]>>? downloadImageAsync = null)
        : this(gateway, delayAsync, downloadImageAsync, SyncCacheManager.DefaultRunLockTimeout)
    {
    }

    internal WechatSyncWorkflow(
        IWechatDraftGateway? gateway,
        Func<TimeSpan, CancellationToken, Task>? delayAsync,
        Func<string, CancellationToken, Task<byte[]>>? downloadImageAsync,
        TimeSpan runLockTimeout,
        Action<string, SyncCache>? saveCache = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        _gateway = gateway;
        _delayAsync = delayAsync ?? ((delay, ct) => Task.Delay(delay, ct));
        _downloadImageAsync = downloadImageAsync;
        _runLockTimeout = runLockTimeout;
        _saveCache = saveCache ?? SyncCacheManager.SaveCache;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<WechatSyncResult> RunAsync(
        WechatSyncContext context,
        WechatSyncOptions options,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<WechatSyncMessage>();
        var diagnostics = new List<WechatSyncDiagnostic>();
        var cachePath = SyncCacheManager.ResolvePath(context.RootDir, options.CacheFile);
        var plan = WechatSyncPlanner.Create(context, options, _utcNow());
        diagnostics.AddRange(plan.Exclusions.Select(exclusion => new WechatSyncDiagnostic(
            exclusion.Code,
            exclusion.Severity,
            exclusion.Message,
            exclusion.Path)));
        if (plan.HasErrors || plan.Candidates.Count == 0)
        {
            const string message = "plugin wechat-sync skipped: no candidate content matched eligibility and filters";
            context.Logger.Info(message);
            messages.Add(new WechatSyncMessage("info", message));
            return new WechatSyncResult(!plan.HasErrors, 0, 0, 0, messages, diagnostics, cachePath);
        }

        await using var runLock = await SyncCacheManager.AcquireRunLockAsync(
            context.RootDir,
            cachePath,
            _runLockTimeout,
            cancellationToken);
        runLock.ValidateIdentity();
        var cache = SyncCacheManager.LoadCache(cachePath, context.Logger);
        runLock.ValidateIdentity();
        var forceRetryIgnoreCache = options.Force || ReadTrueFromEnv(options.ForceRetryIgnoreCacheEnv);
        var filtered = plan.Candidates;

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
                var contentHash = SyncCacheManager.ComputeContentHash(candidate.Item, candidate.Route, rawHtml, options, context);
                SyncOperation? currentOperation = null;
                if (cache.Operations.TryGetValue(candidate.SyncKey, out var operation))
                {
                    if (!string.Equals(operation.ContentHash, contentHash, StringComparison.Ordinal) ||
                        !string.Equals(operation.Target, options.Target, StringComparison.Ordinal))
                    {
                        AddRecoveryRequiredDiagnostic(
                            diagnostics,
                            candidate.SyncKey,
                            "stored operation content hash or target does not match the current candidate");
                        continue;
                    }

                    if (operation.State is "DraftSubmitting" or "PublishSubmitting")
                    {
                        AddRecoveryRequiredDiagnostic(
                            diagnostics,
                            candidate.SyncKey,
                            $"stored operation is in outcome-unknown state '{operation.State}'");
                        continue;
                    }

                    if (operation.State == "DraftCreated" && operation.Target == "draft")
                    {
                        PersistSuccessfulRecord(context.Logger, cachePath, cache, runLock, candidate.SyncKey, operation);
                        synced++;
                        continue;
                    }

                    if (operation.State == "PublishFailed")
                    {
                        AddPublishFailedDiagnostic(diagnostics, candidate.SyncKey);
                        continue;
                    }

                    currentOperation = operation;
                }

                if (currentOperation is null &&
                    !forceRetryIgnoreCache &&
                    cache.Records.TryGetValue(candidate.SyncKey, out var existing) &&
                    string.Equals(existing.ContentHash, contentHash, StringComparison.Ordinal))
                {
                    skipped++;
                    continue;
                }

                if (currentOperation is null)
                {
                    SyncOperation createdOperation;
                    bool cacheUpdated;
                    try
                    {
                        (createdOperation, cacheUpdated) = await SyncWithRetryAsync(
                            context,
                            gateway,
                            thumbResolver,
                            imageProcessor,
                            candidate,
                            options,
                            cache,
                            cachePath,
                            runLock,
                            contentHash,
                            () => updated = true,
                            cancellationToken);
                    }
                    catch (DraftSubmissionOutcomeUnknownException)
                    {
                        AddRecoveryRequiredDiagnostic(
                            diagnostics,
                            candidate.SyncKey,
                            "draft submission outcome is unknown");
                        continue;
                    }

                    updated |= cacheUpdated;
                    currentOperation = createdOperation;
                }

                if (options.Target == "publish")
                {
                    if (candidate.ExpiresAt is { } expiresAt && expiresAt <= _utcNow())
                    {
                        AddContentExpiredDiagnostic(diagnostics, candidate, expiresAt);
                        continue;
                    }

                    var publishProgress = await ResumePublishAsync(
                        context,
                        gateway,
                        candidate,
                        options,
                        cache,
                        cachePath,
                        runLock,
                        currentOperation,
                        diagnostics,
                        cancellationToken);
                    if (publishProgress != PublishProgress.Succeeded)
                    {
                        continue;
                    }

                    currentOperation = cache.Operations[candidate.SyncKey];
                }

                PersistSuccessfulRecord(context.Logger, cachePath, cache, runLock, candidate.SyncKey, currentOperation);
                synced++;
            }

            if (updated)
            {
                runLock.ValidateIdentity();
                SyncCacheManager.SaveCache(cachePath, cache);
                runLock.ValidateIdentity();
            }
        }
        catch (Exception ex)
        {
            if (updated && ex is not CacheCommitUnknownException)
            {
                TrySavePartialCache(cachePath, cache, runLock, context.Logger);
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

    private async Task<(SyncOperation Operation, bool CacheUpdated)> SyncWithRetryAsync(
        WechatSyncContext context,
        IWechatDraftGateway gateway,
        ThumbResolver thumbResolver,
        ContentImageProcessor? imageProcessor,
        WechatSyncCandidate candidate,
        WechatSyncOptions options,
        SyncCache cache,
        string cachePath,
        SyncCacheManager.RunLockHandle runLock,
        string contentHash,
        Action markCacheUpdated,
        CancellationToken cancellationToken)
    {
        Exception? last = null;
        var anyAddDraftInvoked = false;
        for (var attempt = 1; attempt <= options.MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var thumbCacheUpdated = false;
            string? draftId;
            try
            {
                var thumbResult = await thumbResolver.ResolveAndUploadThumbAsync(
                    context, candidate.Item, options, cache, cancellationToken);
                var thumbMediaId = thumbResult.ThumbMediaId;
                thumbCacheUpdated = thumbResult.CacheUpdated;
                if (thumbCacheUpdated)
                {
                    markCacheUpdated();
                }

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

                var submittingOperation = CreateOperation(
                    "DraftSubmitting",
                    contentHash,
                    options.Target,
                    candidate);
                PersistTransition(
                    context.Logger,
                    cachePath,
                    cache,
                    runLock,
                    next => next.Operations[candidate.SyncKey] = submittingOperation);
                anyAddDraftInvoked = true;
                draftId = await gateway.AddDraftAsync(req, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not CacheCommitUnknownException)
            {
                last = ex;
                if (attempt >= options.MaxAttempts)
                {
                    if (anyAddDraftInvoked)
                    {
                        throw new DraftSubmissionOutcomeUnknownException(ex);
                    }

                    break;
                }

                var sleep = ComputeDelay(options.BaseDelayMs, options.BackoffFactor, attempt);
                await _delayAsync(sleep, cancellationToken);
                continue;
            }

            if (string.IsNullOrWhiteSpace(draftId))
            {
                throw new DraftSubmissionOutcomeUnknownException();
            }

            var createdOperation = cache.Operations[candidate.SyncKey] with
            {
                State = "DraftCreated",
                DraftId = draftId,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            PersistTransition(
                context.Logger,
                cachePath,
                cache,
                runLock,
                next => next.Operations[candidate.SyncKey] = createdOperation);
            return (createdOperation, thumbCacheUpdated);
        }

        if (anyAddDraftInvoked)
        {
            throw new DraftSubmissionOutcomeUnknownException(last);
        }

        throw new InvalidOperationException($"wechat-sync failed for '{candidate.SyncKey}' after {options.MaxAttempts} attempts: {last?.Message}", last);
    }

    private static SyncOperation CreateOperation(
        string state,
        string contentHash,
        string target,
        WechatSyncCandidate candidate,
        string? draftId = null,
        string? publishId = null,
        int? lastPublishStatus = null)
        => new(
            state,
            contentHash,
            target,
            draftId,
            publishId,
            DateTimeOffset.UtcNow)
        {
            SourceKey = candidate.SourceKey,
            SourceId = candidate.SourceId,
            Title = candidate.Item.Title ?? string.Empty,
            LastPublishStatus = lastPublishStatus
        };

    private void PersistSuccessfulRecord(
        Bukit.Shared.ILogger logger,
        string cachePath,
        SyncCache cache,
        SyncCacheManager.RunLockHandle runLock,
        string syncKey,
        SyncOperation operation)
    {
        var record = new SyncRecord(
                DateTimeOffset.UtcNow,
                operation.DraftId!,
                operation.ContentHash,
                operation.SourceKey!,
                operation.SourceId!,
                operation.Title!);
        PersistTransition(
            logger,
            cachePath,
            cache,
            runLock,
            next =>
            {
                next.Records[syncKey] = record;
                next.Operations.Remove(syncKey);
            });
    }

    private static void AddRecoveryRequiredDiagnostic(
        List<WechatSyncDiagnostic> diagnostics,
        string syncKey,
        string reason)
        => diagnostics.Add(new WechatSyncDiagnostic(
            "plugin.wechat-sync.recoveryRequired",
            "error",
            $"wechat-sync recovery required for '{syncKey}': {reason}."));

    private static void AddPublishFailedDiagnostic(
        List<WechatSyncDiagnostic> diagnostics,
        string syncKey)
        => diagnostics.Add(new WechatSyncDiagnostic(
            "plugin.wechat-sync.publishFailed",
            "error",
            $"wechat-sync publish failed for '{syncKey}'."));

    private static void AddPublishPendingDiagnostic(
        List<WechatSyncDiagnostic> diagnostics,
        string syncKey)
        => diagnostics.Add(new WechatSyncDiagnostic(
            "plugin.wechat-sync.publishPending",
            "error",
            $"wechat-sync publish remains pending for '{syncKey}'."));

    private static void AddContentExpiredDiagnostic(
        List<WechatSyncDiagnostic> diagnostics,
        WechatSyncCandidate candidate,
        DateTimeOffset expiresAt)
        => diagnostics.Add(new WechatSyncDiagnostic(
            "plugin.wechat-sync.contentExpired",
            "warning",
            $"wechat-sync item '{candidate.Item.Id}' excluded: content expired at {expiresAt:O}.",
            candidate.Route.OutputPath));

    private void PersistCache(
        string cachePath,
        SyncCache cache,
        SyncCacheManager.RunLockHandle runLock)
    {
        runLock.ValidateIdentity();
        _saveCache(cachePath, cache);
        runLock.ValidateIdentity();
    }

    private void PersistTransition(
        Bukit.Shared.ILogger logger,
        string cachePath,
        SyncCache cache,
        SyncCacheManager.RunLockHandle runLock,
        Action<SyncCache> transition)
    {
        var next = CloneCache(cache);
        transition(next);
        try
        {
            PersistCache(cachePath, next, runLock);
        }
        catch (Exception saveException)
        {
            try
            {
                runLock.ValidateIdentity();
                var durable = SyncCacheManager.LoadCache(cachePath, logger);
                runLock.ValidateIdentity();
                ReplaceCacheContents(cache, durable);
            }
            catch (Exception reloadException)
            {
                throw new CacheCommitUnknownException(saveException, reloadException);
            }

            throw;
        }

        ReplaceCacheContents(cache, next);
    }

    private static SyncCache CloneCache(SyncCache cache)
        => new(cache.Version, new Dictionary<string, SyncRecord>(cache.Records, StringComparer.Ordinal))
        {
            ThumbMediaIds = new Dictionary<string, string>(cache.ThumbMediaIds, StringComparer.Ordinal),
            Operations = new Dictionary<string, SyncOperation>(cache.Operations, StringComparer.Ordinal)
        };

    private static void ReplaceCacheContents(SyncCache cache, SyncCache next)
    {
        ReplaceDictionary(cache.Records, next.Records);
        ReplaceDictionary(cache.ThumbMediaIds, next.ThumbMediaIds);
        ReplaceDictionary(cache.Operations, next.Operations);
    }

    private static void ReplaceDictionary<TKey, TValue>(
        Dictionary<TKey, TValue> destination,
        Dictionary<TKey, TValue> source)
        where TKey : notnull
    {
        destination.Clear();
        foreach (var pair in source)
        {
            destination.Add(pair.Key, pair.Value);
        }
    }

    private static void TrySavePartialCache(
        string cachePath,
        SyncCache cache,
        SyncCacheManager.RunLockHandle runLock,
        Bukit.Shared.ILogger logger)
    {
        try
        {
            runLock.ValidateIdentity();
            SyncCacheManager.SaveCache(cachePath, cache);
            runLock.ValidateIdentity();
        }
        catch (Exception ex)
        {
            logger.Warn($"plugin wechat-sync partial cache save failed after sync error: {ex.Message}");
        }
    }

    private async Task<PublishProgress> ResumePublishAsync(
        WechatSyncContext context,
        IWechatDraftGateway gateway,
        WechatSyncCandidate candidate,
        WechatSyncOptions options,
        SyncCache cache,
        string cachePath,
        SyncCacheManager.RunLockHandle runLock,
        SyncOperation operation,
        List<WechatSyncDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var syncKey = candidate.SyncKey;
        if (operation.State == "DraftCreated")
        {
            var draftCreatedOperation = operation;
            operation = operation with
            {
                State = "PublishSubmitting",
                PublishId = null,
                LastPublishStatus = null,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            PersistTransition(
                context.Logger,
                cachePath,
                cache,
                runLock,
                next => next.Operations[syncKey] = operation);

            if (candidate.ExpiresAt is { } expiresAt && expiresAt <= _utcNow())
            {
                operation = draftCreatedOperation with { UpdatedAt = _utcNow() };
                PersistTransition(
                    context.Logger,
                    cachePath,
                    cache,
                    runLock,
                    next => next.Operations[syncKey] = operation);
                AddContentExpiredDiagnostic(diagnostics, candidate, expiresAt);
                return PublishProgress.Expired;
            }

            string? publishId;
            try
            {
                publishId = await gateway.PublishAsync(operation.DraftId!, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                context.Logger.Warn($"plugin wechat-sync publish submission failed: {ex.Message}");
                AddRecoveryRequiredDiagnostic(
                    diagnostics,
                    syncKey,
                    "publish submission outcome is unknown");
                return PublishProgress.RecoveryRequired;
            }

            if (string.IsNullOrWhiteSpace(publishId))
            {
                AddRecoveryRequiredDiagnostic(
                    diagnostics,
                    syncKey,
                    "publish submission returned an empty publish id");
                return PublishProgress.RecoveryRequired;
            }

            operation = operation with
            {
                State = "PublishSubmitted",
                PublishId = publishId,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            PersistTransition(
                context.Logger,
                cachePath,
                cache,
                runLock,
                next => next.Operations[syncKey] = operation);
            context.Logger.Info($"plugin wechat-sync publish submitted: publishId={publishId}");
        }

        var maxPolls = options.PublishPollMaxAttempts;
        for (var poll = 0; poll < maxPolls; poll++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _delayAsync(TimeSpan.FromSeconds(options.PublishPollIntervalSeconds), cancellationToken);

            WechatPublishStatusResult status;
            try
            {
                status = await gateway.CheckPublishStatusAsync(operation.PublishId!, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                context.Logger.Warn($"plugin wechat-sync publish status query failed: {ex.Message}");
                AddPublishPendingDiagnostic(diagnostics, syncKey);
                return PublishProgress.Pending;
            }

            if (status.PublishStatus == 0)
            {
                context.Logger.Info($"plugin wechat-sync publish succeeded: publishId={operation.PublishId} articleUrl={status.ArticleUrl}");
                return PublishProgress.Succeeded;
            }

            if (status.PublishStatus is >= 2 and <= 6)
            {
                operation = operation with
                {
                    State = "PublishFailed",
                    LastPublishStatus = status.PublishStatus,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                PersistTransition(
                    context.Logger,
                    cachePath,
                    cache,
                    runLock,
                    next => next.Operations[syncKey] = operation);
                context.Logger.Warn($"plugin wechat-sync publish failed: publishId={operation.PublishId} status={status.PublishStatus}");
                AddPublishFailedDiagnostic(diagnostics, syncKey);
                return PublishProgress.Failed;
            }

            if (status.PublishStatus == 1)
            {
                operation = operation with
                {
                    LastPublishStatus = 1,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                PersistTransition(
                    context.Logger,
                    cachePath,
                    cache,
                    runLock,
                    next => next.Operations[syncKey] = operation);
            }

            context.Logger.Info($"plugin wechat-sync publish in progress: publishId={operation.PublishId} poll={poll + 1}/{maxPolls}");
        }

        context.Logger.Warn($"plugin wechat-sync publish status poll timeout: publishId={operation.PublishId} after {maxPolls} polls");
        AddPublishPendingDiagnostic(diagnostics, syncKey);
        return PublishProgress.Pending;
    }

    private enum PublishProgress
    {
        Succeeded,
        Pending,
        Failed,
        Expired,
        RecoveryRequired
    }

    private sealed class CacheCommitUnknownException : IOException
    {
        internal CacheCommitUnknownException(Exception saveException, Exception reloadException)
            : base(
                "wechat-sync cache commit failed and the durable state could not be reloaded; automatic partial save is disabled.",
                new AggregateException(saveException, reloadException))
        {
        }
    }

    private sealed class DraftSubmissionOutcomeUnknownException : Exception
    {
        internal DraftSubmissionOutcomeUnknownException(Exception? innerException = null)
            : base("wechat-sync draft submission outcome is unknown.", innerException)
        {
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

}
