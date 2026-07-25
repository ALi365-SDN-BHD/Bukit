using System.Text.Json;
using Bukit.IndexNow;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Runtime;
using Bukit.Plugin.Abstractions.Security;
using Bukit.Plugin.IndexNow;
using Xunit;

namespace Bukit.Plugin.IndexNow.Tests;

public sealed class IndexNowPluginContractTests : IDisposable
{
    private readonly string _rootDir;
    private readonly string _outputDir;
    private readonly string _snapshotPath;
    private readonly string _changeSetPath;

    public IndexNowPluginContractTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-indexnow-tests-" + Guid.NewGuid().ToString("N"));
        _outputDir = Path.Combine(_rootDir, "dist");
        _snapshotPath = Path.Combine(_outputDir, ".bukit", "publish-url-snapshot.json");
        _changeSetPath = Path.Combine(_rootDir, "changes.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_snapshotPath)!);
        WriteSnapshot("https://silushangxun.com/one/", "https://silushangxun.com/two/");
        WriteChangeSet(
            ("added", "https://silushangxun.com/one/", Hash('a')),
            ("deleted", "https://silushangxun.com/two/", Hash('b')));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public void Manifest_ExposesOnlyTheFixedIndexNowCommandAndPermissions()
    {
        var handshake = IndexNowPluginManifestProvider.CreateHandshakeResponse("req", "osx-arm64");
        var manifest = IndexNowPluginManifestProvider.CreateManifestResponse("req");

        Assert.Equal("indexnow", handshake.Plugin?.Id);
        Assert.Equal(IndexNowSubmissionWorkflow.Version, handshake.Plugin?.Version);
        Assert.Equal("osx-arm64", handshake.Plugin?.Platform);
        Assert.Equal(["cli-command"], handshake.Plugin?.Capabilities);

        var root = Assert.Single(manifest.Commands);
        Assert.Equal("indexnow", root.Name);
        var submit = Assert.Single(root.Subcommands);
        Assert.Equal("submit", submit.Name);
        Assert.Equal(
            ["--change-set", "--dry-run", "--site-url", "--snapshot", "--state-dir"],
            submit.Options.Select(option => option.Name).OrderBy(value => value, StringComparer.Ordinal));
        Assert.All(submit.Options.Where(option => option.Name != "--dry-run"), option => Assert.True(option.Required));
        Assert.False(submit.Options.Single(option => option.Name == "--dry-run").Required);
        Assert.True(manifest.RequiredPermissions.Network);
        Assert.Equal(["INDEXNOW_KEY"], manifest.RequiredPermissions.Environment.Read);
    }

    [Fact]
    public void Map_DryRunDerivesOutputRootFromTheFixedSnapshotLayoutWithoutKey()
    {
        var invocation = IndexNowPluginOptionsMapper.Map(Request(dryRun: true, environment: new Dictionary<string, string>()));

        Assert.True(invocation.DryRun);
        Assert.Equal(_outputDir, invocation.OutputRoot);
        Assert.Equal(_snapshotPath, invocation.SnapshotPath);
        Assert.Equal(Path.Combine(_rootDir, ".cache", "indexnow"), invocation.StateDir);
        Assert.Null(invocation.Key);
    }

    [Theory]
    [InlineData("https://silushangxun.com.evil/")]
    [InlineData("https://sub.silushangxun.com/")]
    [InlineData("http://silushangxun.com/")]
    [InlineData("https://user@silushangxun.com/")]
    [InlineData("https://silushangxun.com:443/")]
    [InlineData("https://silushangxun.com/?x=1")]
    [InlineData("https://silushangxun.com/#fragment")]
    public void Map_RejectsSiteUrlInjection(string siteUrl)
    {
        var exception = Assert.Throws<IndexNowPluginOptionsException>(() =>
            IndexNowPluginOptionsMapper.Map(Request(dryRun: true, siteUrl: siteUrl)));

        Assert.Equal("plugin.indexnow.invalidSiteUrl", exception.Code);
    }

    [Fact]
    public void Map_RejectsKeyOptionAndSnapshotOutsideDotBukitLayout()
    {
        var keyRequest = Request(dryRun: true, extraOptions: new Dictionary<string, JsonElement>
        {
            ["--key"] = Json("must-not-appear")
        });
        var keyException = Assert.Throws<IndexNowPluginOptionsException>(() => IndexNowPluginOptionsMapper.Map(keyRequest));
        Assert.Equal("plugin.indexnow.unknownOption", keyException.Code);
        Assert.DoesNotContain("must-not-appear", keyException.Message, StringComparison.Ordinal);

        var invalidSnapshot = Path.Combine(_outputDir, "publish-url-snapshot.json");
        File.Copy(_snapshotPath, invalidSnapshot);
        var layoutException = Assert.Throws<IndexNowPluginOptionsException>(() =>
            IndexNowPluginOptionsMapper.Map(Request(dryRun: true, snapshot: "dist/publish-url-snapshot.json")));
        Assert.Equal("plugin.indexnow.invalidSnapshotLayout", layoutException.Code);
    }

    [Fact]
    public void Map_RealRunRequiresOnlyGrantedIndexNowKeyAndNeverEchoesIt()
    {
        const string key = "public-indexnow-key-value";
        var missingGrant = Assert.Throws<IndexNowPluginOptionsException>(() =>
            IndexNowPluginOptionsMapper.Map(Request(dryRun: false, environment: new Dictionary<string, string>
            {
                ["INDEXNOW_KEY"] = key
            }, grantEnvironment: false)));
        Assert.Equal("plugin.indexnow.envDenied", missingGrant.Code);
        Assert.DoesNotContain(key, missingGrant.Message, StringComparison.Ordinal);

        var invocation = IndexNowPluginOptionsMapper.Map(Request(
            dryRun: false,
            environment: new Dictionary<string, string> { ["INDEXNOW_KEY"] = key }));
        Assert.Equal(key, invocation.Key);
    }

    [Fact]
    public async Task Workflow_DryRunDoesNotUseNetworkWriteKeyOrMutateState()
    {
        var transport = new FakeTransport();
        var workflow = new IndexNowSubmissionWorkflow(transport, new FakeDelay());
        var stateDir = Path.Combine(_rootDir, ".cache", "indexnow");

        var result = await workflow.RunAsync(new IndexNowSubmissionRequest(
            _changeSetPath, _snapshotPath, new Uri("https://silushangxun.com/"), stateDir, _outputDir, null, true));

        Assert.True(result.Success, string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
        Assert.Empty(transport.PageRequests);
        Assert.Empty(transport.Submissions);
        Assert.False(Directory.Exists(stateDir));
        Assert.DoesNotContain(Directory.EnumerateFiles(_outputDir), path => Path.GetFileName(path).EndsWith(".txt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Workflow_PreflightsAddedAndDeletedUrlsBeforeSubmitting()
    {
        var transport = new FakeTransport
        {
            PageResponses =
            {
                ["https://silushangxun.com/one/"] = new IndexNowPageResponse(200, "https://silushangxun.com/one/"),
                ["https://silushangxun.com/two/"] = new IndexNowPageResponse(410, null)
            },
            SubmitResponses = { new IndexNowSubmitResponse(202) }
        };
        var workflow = new IndexNowSubmissionWorkflow(transport, new FakeDelay());

        var result = await workflow.RunAsync(RequestForWorkflow("key-for-public-file"));

        Assert.True(result.Success, string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
        Assert.Equal(
            ["https://silushangxun.com/one/", "https://silushangxun.com/two/"],
            transport.PageRequests);
        var submitted = Assert.Single(transport.Submissions);
        Assert.Equal(["https://silushangxun.com/one/", "https://silushangxun.com/two/"], submitted.Urls);
        Assert.Equal("key-for-public-file", File.ReadAllText(Path.Combine(_outputDir, "key-for-public-file.txt")));
    }

    [Fact]
    public async Task Workflow_RejectsCanonicalMismatchAndInjectedChangeUrlWithoutPosting()
    {
        WriteChangeSet(("added", "https://silushangxun.com.evil/one/", Hash('a')));
        var injectedTransport = new FakeTransport();
        var workflow = new IndexNowSubmissionWorkflow(injectedTransport, new FakeDelay());

        var injected = await workflow.RunAsync(RequestForWorkflow("key"));

        Assert.False(injected.Success);
        Assert.Empty(injectedTransport.PageRequests);
        Assert.Empty(injectedTransport.Submissions);

        WriteChangeSet(("added", "https://silushangxun.com/one/", Hash('a')));
        var mismatchTransport = new FakeTransport
        {
            PageResponses =
            {
                ["https://silushangxun.com/one/"] = new IndexNowPageResponse(200, "https://silushangxun.com/other/")
            }
        };
        var mismatch = await new IndexNowSubmissionWorkflow(mismatchTransport, new FakeDelay())
            .RunAsync(RequestForWorkflow("key"));
        Assert.False(mismatch.Success);
        Assert.Empty(mismatchTransport.Submissions);
    }

    [Fact]
    public async Task Workflow_PreflightNetworkFailureIsStructuredAndHasNoSubmissionSideEffects()
    {
        WriteSnapshot("https://silushangxun.com/one/");
        WriteChangeSet(("added", "https://silushangxun.com/one/", Hash('a')));
        var transport = new FakeTransport { PageException = new HttpRequestException("offline with private details") };

        var result = await new IndexNowSubmissionWorkflow(transport, new FakeDelay())
            .RunAsync(RequestForWorkflow("public-key"));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, item => item.Code == "plugin.indexnow.preflightUnavailable");
        Assert.Empty(transport.Submissions);
        Assert.False(File.Exists(Path.Combine(_outputDir, "public-key.txt")));
        Assert.False(File.Exists(Path.Combine(_rootDir, ".cache", "indexnow", "state.json")));
        Assert.DoesNotContain("private details", JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Workflow_RejectsStateDirectorySymlinkEscapeBeforeWritingOrSubmitting()
    {
        WriteSnapshot("https://silushangxun.com/one/");
        WriteChangeSet(("added", "https://silushangxun.com/one/", Hash('a')));
        var outside = Path.Combine(Path.GetTempPath(), "bukit-indexnow-workflow-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        Directory.CreateDirectory(Path.Combine(_rootDir, ".cache"));
        Directory.CreateSymbolicLink(Path.Combine(_rootDir, ".cache", "indexnow"), outside);
        try
        {
            var transport = new FakeTransport { DefaultPageResponse = new IndexNowPageResponse(200, null) };
            var result = await new IndexNowSubmissionWorkflow(transport, new FakeDelay())
                .RunAsync(RequestForWorkflow("public-key"));

            Assert.False(result.Success);
            Assert.Empty(transport.Submissions);
            Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
            Assert.False(File.Exists(Path.Combine(_outputDir, "public-key.txt")));
        }
        finally
        {
            Directory.Delete(Path.Combine(_rootDir, ".cache", "indexnow"));
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task Workflow_BatchesAtTenThousandWithStableDeduplication()
    {
        var changes = Enumerable.Range(0, 10_001)
            .Select(index => ("added", $"https://silushangxun.com/items/{index:D5}/", "sha256:" + index.ToString("x64")))
            .Concat([("added", "https://silushangxun.com/items/00000/", "sha256:" + 0.ToString("x64"))])
            .ToArray();
        WriteSnapshot(changes.Select(change => change.Item2).ToArray());
        WriteChangeSet(changes);
        var transport = new FakeTransport { DefaultPageResponse = new IndexNowPageResponse(200, null) };
        transport.SubmitResponses.Add(new IndexNowSubmitResponse(200));
        transport.SubmitResponses.Add(new IndexNowSubmitResponse(202));

        var result = await new IndexNowSubmissionWorkflow(transport, new FakeDelay())
            .RunAsync(RequestForWorkflow("key"));

        Assert.True(result.Success);
        Assert.Equal([10_000, 1], transport.Submissions.Select(item => item.Urls.Count));
        Assert.Equal("https://silushangxun.com/items/00000/", transport.Submissions[0].Urls[0]);
        Assert.Equal("https://silushangxun.com/items/10000/", transport.Submissions[1].Urls[0]);
    }

    [Fact]
    public async Task Workflow_Retries429WithinBoundAndClassifiesServerFailureAsPending()
    {
        WriteSnapshot("https://silushangxun.com/one/");
        WriteChangeSet(("updated", "https://silushangxun.com/one/", Hash('c')));
        var delay = new FakeDelay();
        var transport = new FakeTransport
        {
            DefaultPageResponse = new IndexNowPageResponse(200, null),
            SubmitResponses =
            {
                new IndexNowSubmitResponse(429),
                new IndexNowSubmitResponse(429),
                new IndexNowSubmitResponse(503)
            }
        };

        var result = await new IndexNowSubmissionWorkflow(transport, delay)
            .RunAsync(RequestForWorkflow("key"));

        Assert.False(result.Success);
        Assert.Equal(3, transport.Submissions.Count);
        Assert.Equal(2, delay.Delays.Count);
        using var state = JsonDocument.Parse(File.ReadAllText(Path.Combine(_rootDir, ".cache", "indexnow", "state.json")));
        Assert.Single(state.RootElement.GetProperty("deployed").EnumerateArray());
        Assert.Empty(state.RootElement.GetProperty("notified").EnumerateObject());
        Assert.Single(state.RootElement.GetProperty("pending").EnumerateArray());
    }

    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(422)]
    public async Task Workflow_TerminalResponsesNeverAdvanceNotifiedOrPending(int status)
    {
        WriteSnapshot("https://silushangxun.com/one/");
        WriteChangeSet(("added", "https://silushangxun.com/one/", Hash('a')));
        var transport = new FakeTransport
        {
            DefaultPageResponse = new IndexNowPageResponse(200, null),
            SubmitResponses = { new IndexNowSubmitResponse(status) }
        };

        var result = await new IndexNowSubmissionWorkflow(transport, new FakeDelay())
            .RunAsync(RequestForWorkflow("key"));

        Assert.False(result.Success);
        using var state = JsonDocument.Parse(File.ReadAllText(Path.Combine(_rootDir, ".cache", "indexnow", "state.json")));
        Assert.Empty(state.RootElement.GetProperty("notified").EnumerateObject());
        Assert.Empty(state.RootElement.GetProperty("pending").EnumerateArray());
    }

    [Fact]
    public async Task Workflow_RestoresPendingAndDoesNotRenotifyEquivalentSuccessfulUrls()
    {
        WriteSnapshot("https://silushangxun.com/one/");
        WriteChangeSet(("updated", "https://silushangxun.com/one/", Hash('c')));
        var firstTransport = new FakeTransport
        {
            DefaultPageResponse = new IndexNowPageResponse(200, null),
            SubmitException = new HttpRequestException("offline")
        };
        var workflow = new IndexNowSubmissionWorkflow(firstTransport, new FakeDelay());

        var first = await workflow.RunAsync(RequestForWorkflow("key"));
        Assert.False(first.Success);

        var secondTransport = new FakeTransport
        {
            DefaultPageResponse = new IndexNowPageResponse(200, null),
            SubmitResponses = { new IndexNowSubmitResponse(200) }
        };
        var second = await new IndexNowSubmissionWorkflow(secondTransport, new FakeDelay())
            .RunAsync(RequestForWorkflow("key"));
        Assert.True(second.Success, string.Join(" | ", second.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
        Assert.Single(secondTransport.Submissions);

        var thirdTransport = new FakeTransport { DefaultPageResponse = new IndexNowPageResponse(200, null) };
        var third = await new IndexNowSubmissionWorkflow(thirdTransport, new FakeDelay())
            .RunAsync(RequestForWorkflow("key"));
        Assert.True(third.Success);
        Assert.Empty(thirdTransport.Submissions);
    }

    [Fact]
    public async Task Workflow_CurrentChangeReplacesStalePendingForTheSameUrl()
    {
        const string url = "https://silushangxun.com/one/";
        WriteSnapshot();
        WriteChangeSet(("deleted", url, Hash('c')));
        await SaveStateAsync(IndexNowState.Empty with
        {
            Pending = [new IndexNowPendingChange("added", url, Hash('d'))]
        });
        var transport = new FakeTransport
        {
            PageResponses = { [url] = new IndexNowPageResponse(410, null) },
            SubmitResponses = { new IndexNowSubmitResponse(200) }
        };

        var result = await new IndexNowSubmissionWorkflow(transport, new FakeDelay())
            .RunAsync(RequestForWorkflow("public-key"));

        Assert.True(result.Success, string.Join(" | ", result.Diagnostics.Select(item => item.Message)));
        var submission = Assert.Single(transport.Submissions);
        Assert.Equal([url], submission.Urls);
        Assert.Single(transport.PageRequests);
        var state = await LoadStateAsync();
        Assert.Empty(state.Pending);
        Assert.Equal($"deleted\nhttps://silushangxun.com/one/\n{Hash('c')}", state.Notified[url]);
    }

    [Theory]
    [InlineData("unknown", "https://silushangxun.com/stale/", "sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd")]
    [InlineData("added", "https://silushangxun.com/stale/", "sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd")]
    [InlineData("deleted", "https://silushangxun.com.evil/stale/", "sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd")]
    public async Task Workflow_RejectsUntrustedPendingStateBeforeAnySideEffect(
        string type,
        string url,
        string hash)
    {
        WriteSnapshot();
        WriteChangeSet();
        await SaveStateAsync(IndexNowState.Empty with
        {
            Pending = [new IndexNowPendingChange(type, url, hash)]
        });
        var statePath = Path.Combine(_rootDir, ".cache", "indexnow", "state.json");
        var original = File.ReadAllBytes(statePath);
        var transport = new FakeTransport();

        var result = await new IndexNowSubmissionWorkflow(transport, new FakeDelay())
            .RunAsync(RequestForWorkflow("public-key"));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, item => item.Code == "plugin.indexnow.invalidInput");
        Assert.Empty(transport.PageRequests);
        Assert.Empty(transport.Submissions);
        Assert.False(File.Exists(Path.Combine(_outputDir, "public-key.txt")));
        Assert.Equal(original, File.ReadAllBytes(statePath));
    }

    [Fact]
    public async Task Workflow_RejectsMalformedNotifiedFingerprintBeforeItCanSuppressCurrentChange()
    {
        const string url = "https://silushangxun.com/one/";
        WriteSnapshot(url);
        WriteChangeSet(("updated", url, Hash('c')));
        await SaveStateAsync(IndexNowState.Empty with
        {
            Notified = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [url] = $"updated\nhttps://silushangxun.com.evil/one/\n{Hash('c')}"
            }
        });
        var transport = new FakeTransport();

        var result = await new IndexNowSubmissionWorkflow(transport, new FakeDelay())
            .RunAsync(RequestForWorkflow("public-key"));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, item => item.Code == "plugin.indexnow.invalidInput");
        Assert.Empty(transport.PageRequests);
        Assert.Empty(transport.Submissions);
        Assert.False(File.Exists(Path.Combine(_outputDir, "public-key.txt")));
    }

    [Fact]
    public async Task Workflow_RejectsNullStateMembersAsStructuredInvalidInput()
    {
        WriteSnapshot();
        WriteChangeSet();
        var statePath = Path.Combine(_rootDir, ".cache", "indexnow", "state.json");
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        File.WriteAllText(statePath, """
        {
          "version": 1,
          "deployed": [],
          "notified": {
            "https://silushangxun.com/one/": null
          },
          "pending": [null]
        }
        """);
        var transport = new FakeTransport();

        var result = await new IndexNowSubmissionWorkflow(transport, new FakeDelay())
            .RunAsync(RequestForWorkflow("public-key"));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, item => item.Code == "plugin.indexnow.invalidInput");
        Assert.Empty(transport.PageRequests);
        Assert.Empty(transport.Submissions);
        Assert.False(File.Exists(Path.Combine(_outputDir, "public-key.txt")));
    }

    [Fact]
    public async Task Workflow_ClearsPendingAlreadyCoveredByEquivalentNotifiedState()
    {
        const string url = "https://silushangxun.com/one/";
        var fingerprint = $"updated\nhttps://silushangxun.com/one/\n{Hash('c')}";
        WriteSnapshot(url);
        WriteChangeSet(("updated", url, Hash('c')));
        await SaveStateAsync(IndexNowState.Empty with
        {
            Notified = new Dictionary<string, string>(StringComparer.Ordinal) { [url] = fingerprint },
            Pending = [new IndexNowPendingChange("updated", url, Hash('c'))]
        });
        var transport = new FakeTransport();

        var result = await new IndexNowSubmissionWorkflow(transport, new FakeDelay())
            .RunAsync(RequestForWorkflow("public-key"));

        Assert.True(result.Success);
        Assert.Empty(transport.PageRequests);
        Assert.Empty(transport.Submissions);
        Assert.Empty((await LoadStateAsync()).Pending);
    }

    [Fact]
    public async Task StateStore_UsesAtomicStateAndRejectsTraversalAndSymlinkEscape()
    {
        var valid = IndexNowStateStore.ResolveStateFile(_rootDir, ".cache/indexnow");
        Assert.Equal(Path.Combine(_rootDir, ".cache", "indexnow", "state.json"), valid);

        Assert.Throws<InvalidOperationException>(() =>
            IndexNowStateStore.ResolveStateFile(_rootDir, "../indexnow"));

        var outside = Path.Combine(Path.GetTempPath(), "bukit-indexnow-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        Directory.CreateDirectory(Path.Combine(_rootDir, ".cache"));
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(_rootDir, ".cache", "indexnow"), outside);
            Assert.Throws<InvalidOperationException>(() =>
                IndexNowStateStore.ResolveStateFile(_rootDir, ".cache/indexnow"));
        }
        finally
        {
            Directory.Delete(Path.Combine(_rootDir, ".cache", "indexnow"));
            Directory.Delete(outside, recursive: true);
        }

        var store = new IndexNowStateStore();
        var state = IndexNowState.Empty with
        {
            Deployed = ["https://silushangxun.com/one/"],
            Notified = new Dictionary<string, string> { ["https://silushangxun.com/one/"] = "sha256:one" }
        };
        await store.SaveAsync(valid, state);
        Assert.DoesNotContain(Directory.EnumerateFiles(Path.GetDirectoryName(valid)!), path => path.EndsWith(".tmp", StringComparison.Ordinal));
        var loaded = await store.LoadAsync(valid);
        Assert.Equal(state.Version, loaded.Version);
        Assert.Equal(state.Deployed, loaded.Deployed);
        Assert.Equal(state.Notified, loaded.Notified);
        Assert.Equal(state.Pending, loaded.Pending);
    }

    [Fact]
    public async Task StateStore_RunLockRejectsContentionAndCanBeReacquiredAfterRelease()
    {
        var statePath = IndexNowStateStore.ResolveStateFile(_rootDir, ".cache/indexnow");
        var store = new IndexNowStateStore();

        await using (var first = await store.AcquireRunLockAsync(
                         statePath,
                         TimeSpan.Zero,
                         CancellationToken.None))
        {
            await Assert.ThrowsAsync<TimeoutException>(() =>
                store.AcquireRunLockAsync(statePath, TimeSpan.Zero, CancellationToken.None));
        }

        await using var reacquired = await store.AcquireRunLockAsync(
            statePath,
            TimeSpan.Zero,
            CancellationToken.None);
    }

    [Fact]
    public void KeyFileWriter_RejectsTargetSymlinkAndAtomicallyReplacesRegularFile()
    {
        const string key = "public-key";
        var target = Path.Combine(_outputDir, key + ".txt");
        var outside = Path.Combine(Path.GetTempPath(), "bukit-indexnow-key-outside-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(outside, "outside-original");
        File.CreateSymbolicLink(target, outside);
        try
        {
            Assert.Throws<InvalidOperationException>(() => IndexNowKeyFileWriter.Write(_outputDir, key));
            Assert.Equal("outside-original", File.ReadAllText(outside));

            File.Delete(target);
            File.WriteAllText(target, "old");
            Assert.Equal(target, IndexNowKeyFileWriter.Write(_outputDir, key));
            Assert.Equal(key, File.ReadAllText(target));
            Assert.DoesNotContain(
                Directory.EnumerateFiles(_outputDir),
                path => Path.GetFileName(path).StartsWith("." + key + ".txt.", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(target))
            {
                File.Delete(target);
            }

            File.Delete(outside);
        }
    }

    [Fact]
    public async Task StateStore_RunLockRejectsExistingLockSymlinkWithoutTouchingTarget()
    {
        var statePath = IndexNowStateStore.ResolveStateFile(_rootDir, ".cache/indexnow");
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        var outside = Path.Combine(Path.GetTempPath(), "bukit-indexnow-lock-outside-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(outside, "outside-original");
        File.CreateSymbolicLink(statePath + ".lock", outside);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new IndexNowStateStore().AcquireRunLockAsync(statePath, TimeSpan.Zero, CancellationToken.None));
            Assert.Equal("outside-original", File.ReadAllText(outside));
        }
        finally
        {
            File.Delete(statePath + ".lock");
            File.Delete(outside);
        }
    }

    private IndexNowSubmissionRequest RequestForWorkflow(string key)
        => new(
            _changeSetPath,
            _snapshotPath,
            new Uri("https://silushangxun.com/"),
            Path.Combine(_rootDir, ".cache", "indexnow"),
            _outputDir,
            key,
            false);

    private Task SaveStateAsync(IndexNowState state)
        => new IndexNowStateStore().SaveAsync(
            Path.Combine(_rootDir, ".cache", "indexnow", "state.json"),
            state);

    private Task<IndexNowState> LoadStateAsync()
        => new IndexNowStateStore().LoadAsync(
            Path.Combine(_rootDir, ".cache", "indexnow", "state.json"));

    private PluginInvokeRequest Request(
        bool dryRun,
        string siteUrl = "https://silushangxun.com/",
        string snapshot = "dist/.bukit/publish-url-snapshot.json",
        IReadOnlyDictionary<string, string>? environment = null,
        bool grantEnvironment = true,
        IReadOnlyDictionary<string, JsonElement>? extraOptions = null)
    {
        var options = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["--change-set"] = Json("changes.json"),
            ["--snapshot"] = Json(snapshot),
            ["--site-url"] = Json(siteUrl),
            ["--state-dir"] = Json(".cache/indexnow"),
            ["--dry-run"] = Json(dryRun)
        };
        foreach (var pair in extraOptions ?? new Dictionary<string, JsonElement>())
        {
            options[pair.Key] = pair.Value;
        }

        return new PluginInvokeRequest(
            "invoke",
            "bukit-plugin-v1",
            "req",
            new PluginHostInfo("Bukit", "2.0.0", "test"),
            new PluginInvokeCommand("submit", ["indexnow", "submit"], Options: options),
            new PluginInvokeContext(_rootDir, _rootDir, Environment: environment),
            new PluginPermissionSet(
                FileSystem: new PluginFileSystemPermission(Read: ["."], Write: [".cache/indexnow", "dist"]),
                Network: !dryRun,
                Environment: new PluginEnvironmentPermission(Read: grantEnvironment ? ["INDEXNOW_KEY"] : [])));
    }

    private void WriteSnapshot(params string[] urls)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_snapshotPath)!);
        File.WriteAllText(_snapshotPath, JsonSerializer.Serialize(new
        {
            schema = "https://bukit.dev/schemas/publish-url-snapshot.v1.json",
            siteUrl = "https://silushangxun.com/",
            routes = urls.Select(url => new { url, indexable = true, semanticHash = Hash('f') })
        }));
    }

    private void WriteChangeSet(params (string Type, string Url, string Hash)[] changes)
        => File.WriteAllText(_changeSetPath, JsonSerializer.Serialize(new
        {
            changes = changes.Select(change => new
            {
                type = change.Type,
                url = change.Url,
                semanticHash = change.Hash
            })
        }));

    private static JsonElement Json<T>(T value)
        => JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement.Clone();

    private static string Hash(char value)
        => "sha256:" + new string(value, 64);

    private sealed class FakeTransport : IIndexNowTransport
    {
        public Dictionary<string, IndexNowPageResponse> PageResponses { get; } = new(StringComparer.Ordinal);
        public IndexNowPageResponse? DefaultPageResponse { get; set; }
        public List<IndexNowSubmitResponse> SubmitResponses { get; } = [];
        public Exception? SubmitException { get; set; }
        public Exception? PageException { get; set; }
        public List<string> PageRequests { get; } = [];
        public List<IndexNowSubmissionPayload> Submissions { get; } = [];

        public Task<IndexNowPageResponse> GetPageAsync(Uri url, CancellationToken cancellationToken)
        {
            PageRequests.Add(url.AbsoluteUri);
            if (PageException is not null)
            {
                throw PageException;
            }

            if (PageResponses.TryGetValue(url.AbsoluteUri, out var response))
            {
                return Task.FromResult(response);
            }

            var fallback = DefaultPageResponse ?? throw new InvalidOperationException("Missing fake page response.");
            return Task.FromResult(fallback with { CanonicalUrl = fallback.CanonicalUrl ?? url.AbsoluteUri });
        }

        public Task<IndexNowSubmitResponse> SubmitAsync(IndexNowSubmissionPayload payload, CancellationToken cancellationToken)
        {
            Submissions.Add(payload);
            if (SubmitException is not null)
            {
                throw SubmitException;
            }

            var response = SubmitResponses.Count == 0
                ? throw new InvalidOperationException("Missing fake submit response.")
                : SubmitResponses[0];
            SubmitResponses.RemoveAt(0);
            return Task.FromResult(response);
        }
    }

    private sealed class FakeDelay : IIndexNowRetryDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            return Task.CompletedTask;
        }
    }
}
