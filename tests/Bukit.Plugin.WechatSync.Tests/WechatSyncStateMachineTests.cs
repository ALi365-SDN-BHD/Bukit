using Bukit.Shared;
using Bukit.WechatSyncing;
using Xunit;

namespace Bukit.Plugin.WechatSync.Tests;

public sealed class WechatSyncStateMachineTests : IDisposable
{
    private const string SyncKey = "notion:page-1";
    private readonly string _rootDir;
    private readonly string _cachePath;
    private readonly ILogger _logger = new ConsoleLogger(LogLevel.Error);

    public WechatSyncStateMachineTests()
    {
        _rootDir = Path.Combine(AppContext.BaseDirectory, "bukit-wechat-state-tests-" + Guid.NewGuid().ToString("N"));
        _cachePath = Path.Combine(_rootDir, ".cache", "wechat-sync", "sync-cache.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_PersistsDraftSubmittingBeforeCallingAddDraft()
    {
        using var credentials = Credentials();
        var observedState = string.Empty;
        var gateway = new StateGateway
        {
            OnAddDraft = () => observedState = LoadCache().Operations[SyncKey].State
        };
        var options = Options(credentials.AppIdName, credentials.SecretName);

        var result = await new WechatSyncWorkflow(gateway).RunAsync(Context(), options);

        Assert.True(result.Success);
        Assert.Equal("DraftSubmitting", observedState);
        Assert.Equal(1, gateway.AddDraftCount);
    }

    [Fact]
    public async Task RunAsync_DraftTargetMatchingDraftCreatedFinalizesWithoutGatewayCalls()
    {
        using var credentials = Credentials();
        var options = Options(credentials.AppIdName, credentials.SecretName);
        SeedOperation("DraftCreated", options, draftId: "draft-existing");
        var gateway = new StateGateway();

        var result = await new WechatSyncWorkflow(gateway).RunAsync(Context(), options);

        Assert.True(result.Success);
        Assert.Equal(1, result.Synced);
        Assert.Equal(0, gateway.TotalCalls);
        var cache = LoadCache();
        Assert.Empty(cache.Operations);
        Assert.Equal("draft-existing", cache.Records[SyncKey].WechatDraftId);
    }

    [Theory]
    [InlineData("DraftSubmitting", "draft", null)]
    [InlineData("PublishSubmitting", "publish", "draft-existing")]
    public async Task RunAsync_UnknownSubmittingStateRequiresRecoveryWithoutGatewayCalls(
        string state,
        string target,
        string? draftId)
    {
        using var credentials = Credentials();
        var options = Options(credentials.AppIdName, credentials.SecretName) with { Target = target };
        SeedOperation(state, options, draftId);
        var gateway = new StateGateway();

        var result = await new WechatSyncWorkflow(gateway).RunAsync(Context(), options);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "plugin.wechat-sync.recoveryRequired");
        Assert.Equal(0, gateway.TotalCalls);
        Assert.Equal(state, LoadCache().Operations[SyncKey].State);
    }

    [Fact]
    public async Task RunAsync_ForceCannotBypassUnknownSubmittingState()
    {
        using var credentials = Credentials();
        var options = Options(credentials.AppIdName, credentials.SecretName) with { Force = true };
        SeedOperation("DraftSubmitting", options);
        var gateway = new StateGateway();

        var result = await new WechatSyncWorkflow(gateway).RunAsync(Context(), options);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "plugin.wechat-sync.recoveryRequired");
        Assert.Equal(0, gateway.TotalCalls);
        Assert.Equal("DraftSubmitting", LoadCache().Operations[SyncKey].State);
    }

    [Fact]
    public async Task RunAsync_ForceEnvironmentCannotBypassUnknownSubmittingState()
    {
        using var credentials = Credentials();
        var forceEnv = "BUKIT_TEST_WECHAT_FORCE_" + Guid.NewGuid().ToString("N");
        var previous = Environment.GetEnvironmentVariable(forceEnv);
        try
        {
            Environment.SetEnvironmentVariable(forceEnv, "true");
            var options = Options(credentials.AppIdName, credentials.SecretName) with
            {
                ForceRetryIgnoreCacheEnv = forceEnv
            };
            SeedOperation("DraftSubmitting", options);
            var gateway = new StateGateway();

            var result = await new WechatSyncWorkflow(gateway).RunAsync(Context(), options);

            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "plugin.wechat-sync.recoveryRequired");
            Assert.Equal(0, gateway.TotalCalls);
            Assert.Equal("DraftSubmitting", LoadCache().Operations[SyncKey].State);
        }
        finally
        {
            Environment.SetEnvironmentVariable(forceEnv, previous);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RunAsync_ChangedHashOrTargetWhilePendingRequiresRecoveryWithoutGatewayCalls(bool changeHash)
    {
        using var credentials = Credentials();
        var options = Options(credentials.AppIdName, credentials.SecretName);
        SeedOperation(
            "DraftCreated",
            options,
            draftId: "draft-existing",
            contentHash: changeHash ? "different-hash" : CurrentHash(options),
            target: changeHash ? options.Target : "publish");
        var gateway = new StateGateway();

        var result = await new WechatSyncWorkflow(gateway).RunAsync(Context(), options);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "plugin.wechat-sync.recoveryRequired");
        Assert.Equal(0, gateway.TotalCalls);
        Assert.Equal("DraftCreated", LoadCache().Operations[SyncKey].State);
    }

    [Fact]
    public async Task RunAsync_CancellationDuringAddDraftPersistsIntentAndPropagates()
    {
        using var credentials = Credentials();
        var options = Options(credentials.AppIdName, credentials.SecretName);
        var gateway = new StateGateway { CancelAddDraft = true };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new WechatSyncWorkflow(gateway).RunAsync(Context(), options));

        Assert.Equal(1, gateway.AddDraftCount);
        Assert.Equal("DraftSubmitting", LoadCache().Operations[SyncKey].State);
    }

    [Fact]
    public async Task RunAsync_EmptyDraftIdRetainsSubmittingIntent()
    {
        using var credentials = Credentials();
        var options = Options(credentials.AppIdName, credentials.SecretName) with { MaxAttempts = 2 };
        var gateway = new StateGateway { DraftId = string.Empty };

        var result = await new WechatSyncWorkflow(gateway).RunAsync(Context(), options);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "plugin.wechat-sync.recoveryRequired");
        Assert.Equal(1, gateway.AddDraftCount);
        Assert.Equal("DraftSubmitting", LoadCache().Operations[SyncKey].State);
    }

    [Fact]
    public async Task RunAsync_AddDraftRetriesExhaustedReturnsRecoveryRequiredWithIntent()
    {
        using var credentials = Credentials();
        var options = Options(credentials.AppIdName, credentials.SecretName) with { MaxAttempts = 2 };
        var gateway = new StateGateway
        {
            AddDraftException = new InvalidOperationException("ambiguous draft response")
        };

        var result = await Workflow(gateway).RunAsync(Context(), options);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "plugin.wechat-sync.recoveryRequired");
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Message.Contains("ambiguous", StringComparison.Ordinal));
        Assert.Equal(2, gateway.AddDraftCount);
        Assert.Equal("DraftSubmitting", LoadCache().Operations[SyncKey].State);
    }

    [Fact]
    public async Task RunAsync_EarlierAmbiguousAddThenPreAddFailureStillRequiresRecovery()
    {
        using var credentials = Credentials();
        var options = Options(credentials.AppIdName, credentials.SecretName) with { MaxAttempts = 2 };
        var gateway = new StateGateway
        {
            AddDraftException = new InvalidOperationException("ambiguous draft response")
        };
        var saveCount = 0;
        void FailSecondIntentSave(string path, SyncCache cache)
        {
            saveCount++;
            if (saveCount == 2)
            {
                throw new IOException("pre-add persistence failed on second attempt");
            }

            SyncCacheManager.SaveCache(path, cache);
        }

        var workflow = new WechatSyncWorkflow(
            gateway,
            delayAsync: (_, _) => Task.CompletedTask,
            downloadImageAsync: null,
            SyncCacheManager.DefaultRunLockTimeout,
            FailSecondIntentSave);

        var result = await workflow.RunAsync(Context(), options);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "plugin.wechat-sync.recoveryRequired");
        Assert.Equal(1, gateway.AddDraftCount);
        Assert.Equal("DraftSubmitting", LoadCache().Operations[SyncKey].State);
    }

    [Fact]
    public async Task RunAsync_DraftCreatedPersistenceFailureDoesNotRepeatAddDraft()
    {
        using var credentials = Credentials();
        var options = Options(credentials.AppIdName, credentials.SecretName) with { MaxAttempts = 2 };
        var gateway = new StateGateway();
        var saveCount = 0;
        void SaveWithSecondCallFailure(string path, SyncCache cache)
        {
            saveCount++;
            if (saveCount == 2)
            {
                throw new IOException("injected post-draft persistence failure");
            }

            SyncCacheManager.SaveCache(path, cache);
        }

        var workflow = new WechatSyncWorkflow(
            gateway,
            delayAsync: (_, _) => Task.CompletedTask,
            downloadImageAsync: null,
            SyncCacheManager.DefaultRunLockTimeout,
            SaveWithSecondCallFailure);

        await Assert.ThrowsAsync<IOException>(() => workflow.RunAsync(Context(), options));

        Assert.Equal(1, gateway.AddDraftCount);
        Assert.Equal(0, gateway.PublishCount);
        Assert.Equal("DraftSubmitting", LoadCache().Operations[SyncKey].State);
    }

    [Fact]
    public async Task RunAsync_FinalRecordPersistenceFailureAndPartialSaveRemainDraftCreated()
    {
        using var credentials = Credentials();
        var options = Options(credentials.AppIdName, credentials.SecretName);
        var coverPath = Path.Combine(_rootDir, "dist", "assets", "cover.png");
        Directory.CreateDirectory(Path.GetDirectoryName(coverPath)!);
        File.WriteAllBytes(coverPath, TinyPng);
        var gateway = new StateGateway();
        var saveCount = 0;
        void SaveWithFinalCallFailure(string path, SyncCache cache)
        {
            saveCount++;
            if (saveCount == 3)
            {
                throw new IOException("injected final-record persistence failure");
            }

            SyncCacheManager.SaveCache(path, cache);
        }

        var workflow = new WechatSyncWorkflow(
            gateway,
            delayAsync: (_, _) => Task.CompletedTask,
            downloadImageAsync: null,
            SyncCacheManager.DefaultRunLockTimeout,
            SaveWithFinalCallFailure);

        await Assert.ThrowsAsync<IOException>(() =>
            workflow.RunAsync(Context("/assets/cover.png"), options));

        Assert.Equal(1, gateway.AddDraftCount);
        var cache = LoadCache();
        Assert.Empty(cache.Records);
        Assert.Equal("DraftCreated", cache.Operations[SyncKey].State);
    }

    [Fact]
    public async Task RunAsync_DraftCreatedCommitThenThrowPartialSaveKeepsCommittedIdentifier()
    {
        using var credentials = Credentials();
        var options = Options(credentials.AppIdName, credentials.SecretName);
        var context = ContextWithUploadedCover();
        var gateway = new StateGateway();
        var workflow = WorkflowWithCommittedSaveFailure(gateway, failOnSave: 2);

        await Assert.ThrowsAsync<IOException>(() => workflow.RunAsync(context, options));

        Assert.Equal(1, gateway.AddDraftCount);
        Assert.Equal(0, gateway.PublishCount);
        var cache = LoadCache();
        Assert.Empty(cache.Records);
        Assert.Equal("DraftCreated", cache.Operations[SyncKey].State);
        Assert.Equal("draft-new", cache.Operations[SyncKey].DraftId);
    }

    [Fact]
    public async Task RunAsync_PostCommitReloadFailureDoesNotOverwriteUnknownDiskState()
    {
        using var credentials = Credentials();
        var options = Options(credentials.AppIdName, credentials.SecretName);
        var context = ContextWithUploadedCover();
        var gateway = new StateGateway();
        var saveCount = 0;
        void CommitCorruptAndThrow(string path, SyncCache cache)
        {
            saveCount++;
            SyncCacheManager.SaveCache(path, cache);
            if (saveCount == 2)
            {
                File.WriteAllText(path, "commit-state-unreadable");
                throw new IOException("injected post-commit durability failure with unreadable reload");
            }
        }

        var workflow = new WechatSyncWorkflow(
            gateway,
            delayAsync: (_, _) => Task.CompletedTask,
            downloadImageAsync: null,
            SyncCacheManager.DefaultRunLockTimeout,
            CommitCorruptAndThrow);

        var error = await Assert.ThrowsAnyAsync<IOException>(() => workflow.RunAsync(context, options));

        Assert.Contains("durable state could not be reloaded", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, gateway.AddDraftCount);
        Assert.Equal("commit-state-unreadable", File.ReadAllText(_cachePath));
    }

    [Fact]
    public async Task RunAsync_PersistsPublishSubmittingBeforePublishAndSubmittedBeforePolling()
    {
        using var credentials = Credentials();
        var stateAtPublish = string.Empty;
        var stateAtPoll = string.Empty;
        var gateway = new StateGateway
        {
            OnPublish = () => stateAtPublish = LoadCache().Operations[SyncKey].State,
            OnCheckStatus = () => stateAtPoll = LoadCache().Operations[SyncKey].State
        };
        gateway.StatusOutcomes.Enqueue(0);
        var options = PublishOptions(credentials);

        var result = await Workflow(gateway).RunAsync(Context(), options);

        Assert.True(result.Success);
        Assert.Equal("PublishSubmitting", stateAtPublish);
        Assert.Equal("PublishSubmitted", stateAtPoll);
        Assert.Equal(1, gateway.AddDraftCount);
        Assert.Equal(1, gateway.PublishCount);
        Assert.Equal(1, gateway.CheckStatusCount);
    }

    [Fact]
    public async Task RunAsync_MatchingDraftCreatedResumesAtPublishWithoutAddingDraft()
    {
        using var credentials = Credentials();
        var options = PublishOptions(credentials);
        SeedOperation("DraftCreated", options, draftId: "draft-existing");
        var gateway = new StateGateway();
        gateway.StatusOutcomes.Enqueue(0);

        var result = await Workflow(gateway).RunAsync(Context(), options);

        Assert.True(result.Success);
        Assert.Equal(0, gateway.AddDraftCount);
        Assert.Equal(["draft-existing"], gateway.PublishedDraftIds);
        Assert.Empty(LoadCache().Operations);
    }

    [Fact]
    public async Task RunAsync_MatchingPublishSubmittedOnlyPollsStoredPublishId()
    {
        using var credentials = Credentials();
        var options = PublishOptions(credentials);
        SeedOperation(
            "PublishSubmitted",
            options,
            draftId: "draft-existing",
            publishId: "publish-existing");
        var gateway = new StateGateway();
        gateway.StatusOutcomes.Enqueue(0);

        var result = await Workflow(gateway).RunAsync(Context(), options);

        Assert.True(result.Success);
        Assert.Equal(0, gateway.AddDraftCount);
        Assert.Equal(0, gateway.PublishCount);
        Assert.Equal(["publish-existing"], gateway.CheckedPublishIds);
        Assert.Empty(LoadCache().Operations);
    }

    [Fact]
    public async Task RunAsync_PendingOperationTakesPrecedenceOverSuccessfulRecordSkip()
    {
        using var credentials = Credentials();
        var options = PublishOptions(credentials);
        SeedOperation(
            "PublishSubmitted",
            options,
            draftId: "draft-existing",
            publishId: "publish-existing");
        var cache = LoadCache();
        cache.Records[SyncKey] = new SyncRecord(
            DateTimeOffset.Parse("2026-07-20T00:00:00Z"),
            "draft-old",
            CurrentHash(options),
            "notion",
            "page-1",
            "Hello");
        SyncCacheManager.SaveCache(_cachePath, cache);
        var gateway = new StateGateway();
        gateway.StatusOutcomes.Enqueue(0);

        var result = await Workflow(gateway).RunAsync(Context(), options);

        Assert.True(result.Success);
        Assert.Equal(0, gateway.AddDraftCount);
        Assert.Equal(0, gateway.PublishCount);
        Assert.Equal(["publish-existing"], gateway.CheckedPublishIds);
        Assert.Equal("draft-existing", LoadCache().Records[SyncKey].WechatDraftId);
    }

    [Fact]
    public async Task RunAsync_PollTimeoutThenRerunPollsSamePublishIdWithoutResubmitting()
    {
        using var credentials = Credentials();
        var options = PublishOptions(credentials);
        var gateway = new StateGateway();
        gateway.StatusOutcomes.Enqueue(1);
        gateway.StatusOutcomes.Enqueue(1);
        var workflow = Workflow(gateway);

        var first = await workflow.RunAsync(Context(), options);
        var second = await workflow.RunAsync(Context(), options);

        Assert.False(first.Success);
        Assert.False(second.Success);
        Assert.All([first, second], result =>
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "plugin.wechat-sync.publishPending"));
        Assert.Equal(1, gateway.AddDraftCount);
        Assert.Equal(1, gateway.PublishCount);
        Assert.Equal(["publish-1", "publish-1"], gateway.CheckedPublishIds);
        var operation = LoadCache().Operations[SyncKey];
        Assert.Equal("PublishSubmitted", operation.State);
        Assert.Equal(1, operation.LastPublishStatus);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task RunAsync_TerminalPublishStatusPersistsFailedAndRerunDoesNotResubmit(int status)
    {
        using var credentials = Credentials();
        var options = PublishOptions(credentials);
        var gateway = new StateGateway();
        gateway.StatusOutcomes.Enqueue(status);
        var workflow = Workflow(gateway);

        var first = await workflow.RunAsync(Context(), options);
        var second = await workflow.RunAsync(Context(), options);

        Assert.False(first.Success);
        Assert.False(second.Success);
        Assert.All([first, second], result =>
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "plugin.wechat-sync.publishFailed"));
        Assert.Equal(1, gateway.AddDraftCount);
        Assert.Equal(1, gateway.PublishCount);
        Assert.Equal(1, gateway.CheckStatusCount);
        var cache = LoadCache();
        Assert.Empty(cache.Records);
        Assert.Equal("PublishFailed", cache.Operations[SyncKey].State);
        Assert.Equal(status, cache.Operations[SyncKey].LastPublishStatus);
    }

    [Fact]
    public async Task RunAsync_PublishStatusZeroFinalizesAndNextRunSkips()
    {
        using var credentials = Credentials();
        var options = PublishOptions(credentials);
        var gateway = new StateGateway();
        gateway.StatusOutcomes.Enqueue(0);
        var workflow = Workflow(gateway);

        var first = await workflow.RunAsync(Context(), options);
        var second = await workflow.RunAsync(Context(), options);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(1, first.Synced);
        Assert.Equal(1, second.Skipped);
        Assert.Equal(1, gateway.AddDraftCount);
        Assert.Equal(1, gateway.PublishCount);
        Assert.Equal(1, gateway.CheckStatusCount);
        var cache = LoadCache();
        Assert.Empty(cache.Operations);
        Assert.Equal("draft-new", cache.Records[SyncKey].WechatDraftId);
    }

    [Fact]
    public async Task RunAsync_StatusQueryFailureThenRerunPollsSameIdWithoutResubmitting()
    {
        using var credentials = Credentials();
        var options = PublishOptions(credentials);
        var gateway = new StateGateway();
        gateway.StatusOutcomes.Enqueue(new InvalidOperationException("status unavailable"));
        gateway.StatusOutcomes.Enqueue(0);
        var workflow = Workflow(gateway);

        var first = await workflow.RunAsync(Context(), options);
        var second = await workflow.RunAsync(Context(), options);

        Assert.False(first.Success);
        Assert.Contains(first.Diagnostics, diagnostic => diagnostic.Code == "plugin.wechat-sync.publishPending");
        Assert.True(second.Success);
        Assert.Equal(1, gateway.AddDraftCount);
        Assert.Equal(1, gateway.PublishCount);
        Assert.Equal(["publish-1", "publish-1"], gateway.CheckedPublishIds);
        Assert.Empty(LoadCache().Operations);
    }

    [Fact]
    public async Task RunAsync_CancellationDuringPublishPersistsSubmittingIntentAndPropagates()
    {
        using var credentials = Credentials();
        var options = PublishOptions(credentials);
        var gateway = new StateGateway { CancelPublish = true };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Workflow(gateway).RunAsync(Context(), options));

        Assert.Equal(1, gateway.AddDraftCount);
        Assert.Equal(1, gateway.PublishCount);
        Assert.Equal("PublishSubmitting", LoadCache().Operations[SyncKey].State);
    }

    [Fact]
    public async Task RunAsync_CancellationDuringStatusCheckPersistsSubmittedStateAndPropagates()
    {
        using var credentials = Credentials();
        var options = PublishOptions(credentials);
        var gateway = new StateGateway { CancelCheckStatus = true };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Workflow(gateway).RunAsync(Context(), options));

        Assert.Equal(1, gateway.AddDraftCount);
        Assert.Equal(1, gateway.PublishCount);
        Assert.Equal(1, gateway.CheckStatusCount);
        Assert.Equal("PublishSubmitted", LoadCache().Operations[SyncKey].State);
    }

    [Fact]
    public async Task RunAsync_EmptyPublishIdRetainsSubmittingIntentAndRequiresRecovery()
    {
        using var credentials = Credentials();
        var options = PublishOptions(credentials);
        var gateway = new StateGateway { PublishId = string.Empty };

        var result = await Workflow(gateway).RunAsync(Context(), options);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "plugin.wechat-sync.recoveryRequired");
        Assert.Equal(1, gateway.AddDraftCount);
        Assert.Equal(1, gateway.PublishCount);
        Assert.Equal(0, gateway.CheckStatusCount);
        Assert.Equal("PublishSubmitting", LoadCache().Operations[SyncKey].State);
    }

    [Fact]
    public async Task RunAsync_PublishSubmittedPersistenceFailureDoesNotRepeatPublish()
    {
        using var credentials = Credentials();
        var options = PublishOptions(credentials);
        SeedOperation("DraftCreated", options, draftId: "draft-existing");
        var gateway = new StateGateway();
        var saveCount = 0;
        void SaveWithSecondCallFailure(string path, SyncCache cache)
        {
            saveCount++;
            if (saveCount == 2)
            {
                throw new IOException("injected post-publish persistence failure");
            }

            SyncCacheManager.SaveCache(path, cache);
        }

        var workflow = new WechatSyncWorkflow(
            gateway,
            delayAsync: (_, _) => Task.CompletedTask,
            downloadImageAsync: null,
            SyncCacheManager.DefaultRunLockTimeout,
            SaveWithSecondCallFailure);

        await Assert.ThrowsAsync<IOException>(() => workflow.RunAsync(Context(), options));

        Assert.Equal(0, gateway.AddDraftCount);
        Assert.Equal(1, gateway.PublishCount);
        Assert.Equal(0, gateway.CheckStatusCount);
        Assert.Equal("PublishSubmitting", LoadCache().Operations[SyncKey].State);
    }

    [Fact]
    public async Task RunAsync_PublishSubmittingPersistenceFailureDoesNotCallPublish()
    {
        using var credentials = Credentials();
        var options = PublishOptions(credentials);
        SeedOperation("DraftCreated", options, draftId: "draft-existing");
        var gateway = new StateGateway();
        var workflow = WorkflowWithSaveFailure(gateway, failOnSave: 1);

        await Assert.ThrowsAsync<IOException>(() => workflow.RunAsync(Context(), options));

        Assert.Equal(0, gateway.PublishCount);
        Assert.Equal("DraftCreated", LoadCache().Operations[SyncKey].State);
    }

    [Fact]
    public async Task RunAsync_PublishFailedPersistenceFailureRemainsSubmitted()
    {
        using var credentials = Credentials();
        var options = PublishOptions(credentials);
        SeedOperation(
            "PublishSubmitted",
            options,
            draftId: "draft-existing",
            publishId: "publish-existing");
        var gateway = new StateGateway();
        gateway.StatusOutcomes.Enqueue(2);
        var workflow = WorkflowWithSaveFailure(gateway, failOnSave: 1);

        await Assert.ThrowsAsync<IOException>(() => workflow.RunAsync(Context(), options));

        Assert.Equal(1, gateway.CheckStatusCount);
        Assert.Empty(LoadCache().Records);
        Assert.Equal("PublishSubmitted", LoadCache().Operations[SyncKey].State);
    }

    [Fact]
    public async Task RunAsync_PublishedFinalRecordPersistenceFailureRemainsSubmitted()
    {
        using var credentials = Credentials();
        var options = PublishOptions(credentials);
        SeedOperation(
            "PublishSubmitted",
            options,
            draftId: "draft-existing",
            publishId: "publish-existing");
        var gateway = new StateGateway();
        gateway.StatusOutcomes.Enqueue(0);
        var workflow = WorkflowWithSaveFailure(gateway, failOnSave: 1);

        await Assert.ThrowsAsync<IOException>(() => workflow.RunAsync(Context(), options));

        Assert.Equal(1, gateway.CheckStatusCount);
        var cache = LoadCache();
        Assert.Empty(cache.Records);
        Assert.Equal("PublishSubmitted", cache.Operations[SyncKey].State);
    }

    [Fact]
    public async Task RunAsync_PublishSubmittedCommitThenThrowPartialSaveKeepsPublishId()
    {
        using var credentials = Credentials();
        var options = PublishOptions(credentials);
        var context = ContextWithUploadedCover();
        var gateway = new StateGateway();
        var workflow = WorkflowWithCommittedSaveFailure(gateway, failOnSave: 4);

        await Assert.ThrowsAsync<IOException>(() => workflow.RunAsync(context, options));

        Assert.Equal(1, gateway.AddDraftCount);
        Assert.Equal(1, gateway.PublishCount);
        Assert.Equal(0, gateway.CheckStatusCount);
        var operation = LoadCache().Operations[SyncKey];
        Assert.Equal("PublishSubmitted", operation.State);
        Assert.Equal("publish-1", operation.PublishId);
    }

    [Fact]
    public async Task RunAsync_FinalRecordCommitThenThrowPartialSaveKeepsCommittedSuccess()
    {
        using var credentials = Credentials();
        var options = Options(credentials.AppIdName, credentials.SecretName);
        var context = ContextWithUploadedCover();
        var gateway = new StateGateway();
        var workflow = WorkflowWithCommittedSaveFailure(gateway, failOnSave: 3);

        await Assert.ThrowsAsync<IOException>(() => workflow.RunAsync(context, options));

        Assert.Equal(1, gateway.AddDraftCount);
        var cache = LoadCache();
        Assert.Empty(cache.Operations);
        Assert.Equal("draft-new", cache.Records[SyncKey].WechatDraftId);
    }

    [Fact]
    public async Task RunAsync_PreloadedPublishFailedReturnsErrorWithoutGatewayCalls()
    {
        using var credentials = Credentials();
        var options = PublishOptions(credentials);
        SeedOperation(
            "PublishFailed",
            options,
            draftId: "draft-existing",
            publishId: "publish-existing",
            lastPublishStatus: 4);
        var gateway = new StateGateway();

        var result = await Workflow(gateway).RunAsync(Context(), options);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "plugin.wechat-sync.publishFailed");
        Assert.Equal(0, gateway.TotalCalls);
        Assert.Equal("PublishFailed", LoadCache().Operations[SyncKey].State);
    }

    private SyncCache LoadCache()
        => SyncCacheManager.LoadCache(_cachePath, _logger);

    private void SeedOperation(
        string state,
        WechatSyncOptions options,
        string? draftId = null,
        string? publishId = null,
        int? lastPublishStatus = null,
        string? contentHash = null,
        string? target = null)
    {
        var operation = new SyncOperation(
            state,
            contentHash ?? CurrentHash(options),
            target ?? options.Target,
            draftId,
            publishId,
            DateTimeOffset.Parse("2026-07-21T00:00:00Z"))
        {
            SourceKey = "notion",
            SourceId = "page-1",
            Title = "Hello",
            LastPublishStatus = lastPublishStatus
        };
        var cache = new SyncCache(3, new Dictionary<string, SyncRecord>(StringComparer.Ordinal))
        {
            Operations = new Dictionary<string, SyncOperation>(StringComparer.Ordinal)
            {
                [SyncKey] = operation
            }
        };
        SyncCacheManager.SaveCache(_cachePath, cache);
    }

    private string CurrentHash(WechatSyncOptions options)
    {
        var context = Context();
        var (item, route) = context.Routed[0];
        return SyncCacheManager.ComputeContentHash(item, route, item.ContentHtml ?? string.Empty, options, context);
    }

    private WechatSyncContext Context(string? cover = null)
    {
        var fields = new Dictionary<string, WechatSyncField>
        {
            ["type"] = new("string", "post")
        };
        if (cover is not null)
        {
            fields["cover"] = new("string", cover);
        }

        var item = new WechatSyncItem(
            Id: "post-1",
            Title: "Hello",
            Slug: "post-1",
            PublishAt: DateTimeOffset.Parse("2026-07-21T00:00:00Z"),
            ContentHtml: "<p>Hello</p>",
            Metadata: new Dictionary<string, object>
            {
                ["sourceKey"] = "notion",
                ["sourceId"] = "page-1",
                ["summary"] = "Summary"
            },
            Fields: fields);
        var route = new WechatSyncRoute(
            "/posts/post-1/",
            Path.Combine(_rootDir, "dist", "posts", "post-1", "index.html"),
            "post");

        return new WechatSyncContext
        {
            RootDir = _rootDir,
            OutputDir = Path.Combine(_rootDir, "dist"),
            BaseUrl = "/",
            SiteName = "Bukit",
            SiteUrl = "https://example.com",
            Logger = _logger,
            Routed = [(item, route)]
        };
    }

    private WechatSyncContext ContextWithUploadedCover()
    {
        var coverPath = Path.Combine(_rootDir, "dist", "assets", "cover.png");
        Directory.CreateDirectory(Path.GetDirectoryName(coverPath)!);
        File.WriteAllBytes(coverPath, TinyPng);
        return Context("/assets/cover.png");
    }

    private static WechatSyncOptions Options(string appIdEnv, string appSecretEnv)
        => new(
            SourceNames: [],
            ContentTypes: new HashSet<string>(["post"], StringComparer.OrdinalIgnoreCase),
            DefaultTypesWhenMissing: new HashSet<string>(["post"], StringComparer.OrdinalIgnoreCase),
            CacheFile: ".cache/wechat-sync/sync-cache.json",
            MaxAttempts: 1,
            BaseDelayMs: 1,
            BackoffFactor: 1,
            AppIdEnv: appIdEnv,
            AppSecretEnv: appSecretEnv,
            ForceRetryIgnoreCacheEnv: string.Empty,
            Author: null,
            DefaultThumbMediaId: "thumb-media-id",
            NeedOpenComment: false,
            OnlyFansCanComment: false,
            SiteName: "Bukit",
            SiteUrl: "https://example.com",
            BaseUrl: "/");

    private static WechatSyncOptions PublishOptions(CredentialScope credentials)
        => Options(credentials.AppIdName, credentials.SecretName) with
        {
            Target = "publish",
            PublishPollMaxAttempts = 1,
            PublishPollIntervalSeconds = 1
        };

    private static WechatSyncWorkflow Workflow(StateGateway gateway)
        => new(gateway, delayAsync: (_, _) => Task.CompletedTask);

    private static WechatSyncWorkflow WorkflowWithSaveFailure(StateGateway gateway, int failOnSave)
    {
        var saveCount = 0;
        void Save(string path, SyncCache cache)
        {
            saveCount++;
            if (saveCount == failOnSave)
            {
                throw new IOException($"injected persistence failure {failOnSave}");
            }

            SyncCacheManager.SaveCache(path, cache);
        }

        return new WechatSyncWorkflow(
            gateway,
            delayAsync: (_, _) => Task.CompletedTask,
            downloadImageAsync: null,
            SyncCacheManager.DefaultRunLockTimeout,
            Save);
    }

    private static WechatSyncWorkflow WorkflowWithCommittedSaveFailure(StateGateway gateway, int failOnSave)
    {
        var saveCount = 0;
        void SaveThenMaybeThrow(string path, SyncCache cache)
        {
            saveCount++;
            SyncCacheManager.SaveCache(path, cache);
            if (saveCount == failOnSave)
            {
                throw new IOException($"injected post-commit durability failure {failOnSave}");
            }
        }

        return new WechatSyncWorkflow(
            gateway,
            delayAsync: (_, _) => Task.CompletedTask,
            downloadImageAsync: null,
            SyncCacheManager.DefaultRunLockTimeout,
            SaveThenMaybeThrow);
    }

    private static CredentialScope Credentials()
        => new(
            "BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"),
            "BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"));

    private sealed class StateGateway : IWechatDraftGateway
    {
        public string DraftId { get; init; } = "draft-new";
        public string PublishId { get; init; } = "publish-1";
        public bool CancelAddDraft { get; init; }
        public bool CancelPublish { get; init; }
        public bool CancelCheckStatus { get; init; }
        public Action? OnAddDraft { get; init; }
        public Exception? AddDraftException { get; init; }
        public Action? OnPublish { get; init; }
        public Action? OnCheckStatus { get; init; }
        public Queue<object> StatusOutcomes { get; } = new();
        public List<string> PublishedDraftIds { get; } = new();
        public List<string> CheckedPublishIds { get; } = new();
        public int AddDraftCount { get; private set; }
        public int PublishCount { get; private set; }
        public int CheckStatusCount { get; private set; }
        public int UploadThumbCount { get; private set; }
        public int UploadContentImageCount { get; private set; }
        public int TotalCalls =>
            AddDraftCount + PublishCount + CheckStatusCount + UploadThumbCount + UploadContentImageCount;

        public Task<string> AddDraftAsync(WechatDraftRequest request, CancellationToken cancellationToken)
        {
            _ = request;
            _ = cancellationToken;
            AddDraftCount++;
            OnAddDraft?.Invoke();
            if (CancelAddDraft)
            {
                throw new OperationCanceledException("draft canceled");
            }

            if (AddDraftException is not null)
            {
                throw AddDraftException;
            }

            return Task.FromResult(DraftId);
        }

        public Task<string> UploadThumbAsync(byte[] bytes, string fileName, string contentType, CancellationToken cancellationToken)
        {
            _ = bytes;
            _ = fileName;
            _ = contentType;
            _ = cancellationToken;
            UploadThumbCount++;
            return Task.FromResult("thumb-1");
        }

        public Task<string> UploadContentImageAsync(byte[] bytes, string fileName, string contentType, CancellationToken cancellationToken)
        {
            _ = bytes;
            _ = fileName;
            _ = contentType;
            _ = cancellationToken;
            UploadContentImageCount++;
            return Task.FromResult("https://mmbiz.qpic.cn/image.jpg");
        }

        public Task<string> PublishAsync(string mediaId, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            PublishCount++;
            PublishedDraftIds.Add(mediaId);
            OnPublish?.Invoke();
            if (CancelPublish)
            {
                throw new OperationCanceledException("publish canceled");
            }

            return Task.FromResult(PublishId);
        }

        public Task<WechatPublishStatusResult> CheckPublishStatusAsync(string publishId, CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            CheckStatusCount++;
            CheckedPublishIds.Add(publishId);
            OnCheckStatus?.Invoke();
            if (CancelCheckStatus)
            {
                throw new OperationCanceledException("status check canceled");
            }

            var outcome = StatusOutcomes.Count > 0 ? StatusOutcomes.Dequeue() : 0;
            if (outcome is Exception exception)
            {
                throw exception;
            }

            return Task.FromResult(new WechatPublishStatusResult(publishId, (int)outcome, null));
        }
    }

    private sealed class CredentialScope : IDisposable
    {
        private readonly string? _previousAppId;
        private readonly string? _previousSecret;

        internal CredentialScope(string appIdName, string secretName)
        {
            AppIdName = appIdName;
            SecretName = secretName;
            _previousAppId = Environment.GetEnvironmentVariable(appIdName);
            _previousSecret = Environment.GetEnvironmentVariable(secretName);
            Environment.SetEnvironmentVariable(appIdName, "app");
            Environment.SetEnvironmentVariable(secretName, "secret");
        }

        internal string AppIdName { get; }
        internal string SecretName { get; }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(AppIdName, _previousAppId);
            Environment.SetEnvironmentVariable(SecretName, _previousSecret);
        }
    }

    private static readonly byte[] TinyPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41,
        0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0xF0,
        0x1F, 0x00, 0x05, 0x00, 0x01, 0xFF, 0x89, 0x99,
        0x3D, 0x1D, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45,
        0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    ];
}
