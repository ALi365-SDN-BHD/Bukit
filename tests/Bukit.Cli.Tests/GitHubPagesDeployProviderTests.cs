using System.Diagnostics;
using System.Reflection;
using System.Text;
using Bukit.Cli.Deploy;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class GitHubPagesDeployProviderTests
{
    [Fact]
    public async Task WaitForTerminationGraceAsync_IncompleteCleanup_ReturnsFalseWithinGrace()
    {
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopwatch = Stopwatch.StartNew();

        var wait = GitHubPagesDeployProvider.WaitForTerminationGraceAsync(
            never.Task,
            TimeSpan.FromMilliseconds(50));
        var completed = await wait.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.False(completed);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task BoundedGitCollector_GetText_SealsSnapshotAgainstLatePumpWrites()
    {
        using var process = new Process();
        using var stream = new TwoPhaseReadStream("before", "after");
        using var collector = new GitHubPagesDeployProvider.BoundedGitCollector(1024);
        Task readTask = collector.ReadAsync(stream, process);
        await stream.SecondReadStarted.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal("before", collector.GetText());

        stream.ReleaseSecondRead();
        await readTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal("before", collector.GetText());
    }

    [Fact]
    public async Task BoundedGitCollector_GetDiagnosticText_MalformedUtf8_EmitsByteBoundedHeadAndTail()
    {
        var bytes = Enumerable.Repeat((byte)0xff, 96 * 1024).ToArray();
        using var process = new Process();
        using var stream = new MemoryStream(bytes);
        using var collector = new GitHubPagesDeployProvider.BoundedGitCollector(128 * 1024);

        await collector.ReadAsync(stream, process);

        var diagnostic = collector.GetDiagnosticText();
        AssertDiagnosticSlicesAreUtf8ByteBounded(diagnostic);
    }

    [Fact]
    public async Task ResolveGit_ProbeChildInheritsStdout_ReturnsWithinDrainDeadline()
    {
        if (OperatingSystem.IsWindows()) return;

        var root = Path.Combine(Path.GetTempPath(), "bukit-resolve-git-" + Guid.NewGuid().ToString("N"));
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        Directory.CreateDirectory(root);
        try
        {
            WriteExecutable(root, "which", "( /bin/sleep 12 ) &\nexit 0");
            Environment.SetEnvironmentVariable("PATH", root);

            var stopwatch = Stopwatch.StartNew();
            var resolveTask = Task.Run(() => InvokePrivateStatic<string?>(
                nameof(GitHubPagesDeployProvider),
                "ResolveGit",
                [null]));
            var result = await resolveTask.WaitAsync(TimeSpan.FromSeconds(7));
            stopwatch.Stop();

            Assert.Null(result);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(7),
                $"ResolveGit took {stopwatch.Elapsed.TotalSeconds:F1}s with an inherited stdout pipe.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DeployAsync_UserPagesRepoWithDotRemoteUrl_DeploysToRootGithubIoUrl()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "git@github.com:ali/ali.github.io.git";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal("https://ali.github.io", result.DeployedUrl);
    }

    [Fact]
    public async Task DeployAsync_ExistingBranchWithoutKeepHistory_UsesDepthOneCloneAndStagesCopiedOutput()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.FakeGit.RemoteHeads = "abc123\trefs/heads/gh-pages";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(cname: "docs.example.com"), CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal("https://docs.example.com", result.DeployedUrl);

        var log = scope.FakeGit.ReadLog();
        Assert.Contains("init", log, StringComparison.Ordinal);
        Assert.Contains("fetch --depth 1 bukit-deploy-", log, StringComparison.Ordinal);
        Assert.Contains(" gh-pages", log, StringComparison.Ordinal);
        Assert.Contains("checkout -B gh-pages FETCH_HEAD", log, StringComparison.Ordinal);
        Assert.Contains("SNAPSHOT nojekyll", log, StringComparison.Ordinal);
        Assert.Contains("SNAPSHOT cname=docs.example.com", log, StringComparison.Ordinal);
        Assert.Contains("SNAPSHOT index", log, StringComparison.Ordinal);
        Assert.DoesNotContain("SNAPSHOT old", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeployAsync_LsRemoteFailure_FailsClosedWithoutInitializingOrPushing()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.FakeGit.LsRemoteMode = "forbidden";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(
            scope.CreateContext(force: true),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("ls-remote", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", result.Error, StringComparison.Ordinal);
        var log = scope.FakeGit.ReadLog();
        Assert.Contains("ls-remote --heads", log, StringComparison.Ordinal);
        Assert.Contains("|init", log, StringComparison.Ordinal);
        Assert.DoesNotContain("|fetch", log, StringComparison.Ordinal);
        Assert.DoesNotContain("|push ", log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("-main")]
    [InlineData("main/")]
    [InlineData("/main")]
    [InlineData("refs/heads/main")]
    [InlineData("HEAD")]
    [InlineData("feature..branch")]
    [InlineData("main branch")]
    [InlineData("main@{1}")]
    public async Task DeployAsync_InvalidBranch_IsRejected(string branch)
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(branch: branch), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("deploy.branch is not a valid Git branch name", result.Error, StringComparison.Ordinal);
        Assert.Equal(string.Empty, scope.FakeGit.ReadLog());
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("example.com/path")]
    [InlineData("example.com:443")]
    [InlineData("  example.com")]
    [InlineData("example..com")]
    [InlineData("_example.com")]
    [InlineData("example.com.\n")]
    public async Task DeployAsync_InvalidCname_IsRejected(string cname)
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(cname: cname), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("deploy.cname must be a single domain name", result.Error, StringComparison.Ordinal);
        Assert.Equal(string.Empty, scope.FakeGit.ReadLog());
    }

    [Fact]
    public async Task DeployAsync_NormalizesCnameToLowerCase()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(cname: "WWW.Example.Com"), CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal("https://www.example.com", result.DeployedUrl);
        var log = scope.FakeGit.ReadLog();
        Assert.Contains("SNAPSHOT cname=www.example.com", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeployAsync_NonFastForwardWithoutForce_ReturnsFriendlyError()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.FakeGit.PushMode = "nonff-once";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Non-fast-forward push rejected. The remote branch has diverged; rerun with --force to overwrite it.", result.Error);
        Assert.Empty(scope.Logger.Warnings);
        Assert.Empty(scope.Logger.Errors);
        Assert.DoesNotContain("secret-token", string.Join('\n', scope.Logger.Infos), StringComparison.Ordinal);
        AssertDeploymentCleanupSucceeded(scope);
    }

    [Fact]
    public async Task Deploy_NonFastForwardRequiresForce()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.FakeGit.PushMode = "nonff-once";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var first = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);
        var second = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(force: true), CancellationToken.None);

        Assert.False(first.Success);
        Assert.Equal("Non-fast-forward push rejected. The remote branch has diverged; rerun with --force to overwrite it.", first.Error);
        Assert.True(second.Success, second.Error);
        Assert.Matches(@"push --force bukit-deploy-[0-9a-f]+ gh-pages", scope.FakeGit.ReadLog());
    }

    [Fact]
    public async Task DeployAsync_NonFastForwardWithForce_PushesWithForce()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.FakeGit.PushMode = "nonff-once";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(force: true), CancellationToken.None);

        Assert.True(result.Success, result.Error);

        var log = scope.FakeGit.ReadLog();
        Assert.Matches(@"push --force bukit-deploy-[0-9a-f]+ gh-pages", log);
    }

    [Fact]
    public async Task DeployAsync_PushForbidden_SanitizesTokenAndAddsRepoScopeHint()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.FakeGit.PushMode = "forbidden";
        scope.SetGithubToken("top-secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.DoesNotContain("top-secret-token", result.Error, StringComparison.Ordinal);
        Assert.Contains("***", result.Error, StringComparison.Ordinal);
        Assert.Contains("Ensure your GITHUB_TOKEN has 'repo' scope", result.Error, StringComparison.Ordinal);
        Assert.Contains(scope.Logger.Errors, message => message.Contains("***", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Deploy_ErrorMessage_DoesNotContainGitHubToken()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.FakeGit.PushMode = "forbidden";
        scope.SetGithubToken("ghp_TEST_SECRET_TOKEN_123");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.DoesNotContain("ghp_TEST_SECRET_TOKEN_123", result.Error, StringComparison.Ordinal);
        Assert.Contains("***", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("ghp_TEST_SECRET_TOKEN_123", string.Join('\n', scope.Logger.Errors), StringComparison.Ordinal);
        Assert.DoesNotContain("ghp_TEST_SECRET_TOKEN_123", string.Join('\n', scope.Logger.Infos), StringComparison.Ordinal);
        Assert.DoesNotContain("ghp_TEST_SECRET_TOKEN_123", string.Join('\n', scope.Logger.Warnings), StringComparison.Ordinal);
        Assert.DoesNotContain("ghp_TEST_SECRET_TOKEN_123", scope.FakeGit.ReadLog(), StringComparison.Ordinal);
        AssertDeploymentCleanupSucceeded(scope);
    }

    [Fact]
    public async Task Deploy_GitFailure_DoesNotLogToken()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.FakeGit.PushMode = "forbidden";
        scope.SetGithubToken("ghp_TEST_SECRET_TOKEN_123");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.False(result.Success);
        var allMessages = string.Join('\n', scope.Logger.Errors);
        Assert.DoesNotContain("ghp_TEST_SECRET_TOKEN_123", allMessages, StringComparison.Ordinal);
        Assert.Contains("***", allMessages, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deploy_PushFailure_SanitizesToken()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.FakeGit.PushMode = "forbidden";
        scope.SetGithubToken("ghp_TEST_SECRET_TOKEN_123");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.DoesNotContain("ghp_TEST_SECRET_TOKEN_123", result.Error, StringComparison.Ordinal);
        Assert.Contains("***", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeployAsync_CancelDuringPush_PropagatesOperationCanceledException_AndCleansUp()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.FakeGit.PushMode = "sleep";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        using var cts = new CancellationTokenSource();
        var deployTask = new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), cts.Token);

        var started = Stopwatch.StartNew();
        var readinessTimeout = OperatingSystem.IsWindows() ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(2);
        while (!File.Exists(scope.FakeGit.PushSleepStartedPath) && !deployTask.IsCompleted && started.Elapsed < readinessTimeout)
        {
            await Task.Delay(25);
        }

        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await deployTask);

        Assert.True(File.Exists(scope.FakeGit.PushSleepStartedPath), "Push sleep should have started.");
        Assert.False(File.Exists(scope.FakeGit.PushSleepCompletedPath), "Push sleep should not have completed.");
        AssertDeploymentCleanupSucceeded(scope);
    }

    [Fact]
    public async Task DeployAsync_GitCommandTimeout_CleansUpAndReturnsFriendlyError()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.FakeGit.PushMode = "sleep";
        scope.SetGithubToken("secret-token");
        scope.SetDeployTimeoutSeconds("1");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Git command timed out during GitHub Pages deployment", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", result.Error, StringComparison.Ordinal);
        Assert.True(File.Exists(scope.FakeGit.PushSleepStartedPath), "Push sleep should have started.");
        Assert.False(File.Exists(scope.FakeGit.PushSleepCompletedPath), "Push sleep should be terminated before completion.");
        AssertDeploymentCleanupSucceeded(scope);
    }

    [Fact]
    public async Task DeployAsync_GitCommandTimeout_WithInheritedPipe_ReturnsWithinDrainDeadline()
    {
        if (OperatingSystem.IsWindows()) return;

        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.FakeGit.PushMode = "sleep-with-inherited-pipe";
        scope.SetGithubToken("secret-token");
        scope.SetDeployTimeoutSeconds("1");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);
        var stopwatch = Stopwatch.StartNew();

        var result = await new GitHubPagesDeployProvider().DeployAsync(
            scope.CreateContext(),
            CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        stopwatch.Stop();
        Assert.False(result.Success);
        Assert.Contains("Git command timed out during GitHub Pages deployment", result.Error, StringComparison.Ordinal);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(8),
            $"Git timeout + inherited-pipe drain took {stopwatch.Elapsed.TotalSeconds:F1}s, expected < 8s");
        AssertDeploymentCleanupSucceeded(scope);
    }

    [Fact]
    public async Task DeployAsync_PushFloodsStdoutAndStderr_CompletesWithoutFalseTimeout()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.FakeGit.PushMode = "flood-success";
        scope.SetGithubToken("secret-token");
        scope.SetDeployTimeoutSeconds("5");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(
            scope.CreateContext(),
            CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal("https://ali.github.io/docs", result.DeployedUrl);
        AssertDeploymentCleanupSucceeded(scope);
    }

    [Fact]
    public async Task DeployAsync_PushOutputBeyondStreamCap_FailsWithoutUnboundedCapture()
    {
        if (OperatingSystem.IsWindows()) return;

        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.FakeGit.PushMode = "flood-over-limit";
        scope.SetGithubToken("secret-token");
        scope.SetDeployTimeoutSeconds("10");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(
            scope.CreateContext(),
            CancellationToken.None);

        var error = Assert.IsType<string>(result.Error);
        Assert.False(result.Success);
        Assert.Contains("produced more than 4194304 bytes on stdout", error, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(error) < 4096,
            $"Git overflow error was {Encoding.UTF8.GetByteCount(error)} UTF-8 bytes.");
        Assert.False(File.Exists(scope.FakeGit.PushFloodOverflowCompletedPath),
            "Git overflow should terminate the process tree before its child can finish.");
        AssertDeploymentCleanupSucceeded(scope);
    }

    [Fact]
    public async Task DeployAsync_PushParentExitsWithInheritedPipe_ReturnsWithinDrainDeadline()
    {
        if (OperatingSystem.IsWindows()) return;

        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.FakeGit.PushMode = "child-inherits-pipe";
        scope.SetGithubToken("secret-token");
        scope.SetDeployTimeoutSeconds("10");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var stopwatch = Stopwatch.StartNew();
        var result = await new GitHubPagesDeployProvider().DeployAsync(
            scope.CreateContext(),
            CancellationToken.None);
        stopwatch.Stop();

        Assert.True(result.Success, result.Error);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Git drain took {stopwatch.Elapsed.TotalSeconds:F1}s with an inherited pipe.");
        AssertDeploymentCleanupSucceeded(scope);
    }

    [Fact]
    public async Task DeployAsync_PushFloodThenFailure_ReturnsReadableSanitizedError()
    {
        const string token = "ghp_FLOOD_SECRET_TOKEN_123";
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.FakeGit.PushMode = "flood-failure";
        scope.SetGithubToken(token);
        scope.SetDeployTimeoutSeconds("5");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(
            scope.CreateContext(),
            CancellationToken.None);

        Assert.False(result.Success);
        var error = Assert.IsType<string>(result.Error);
        Assert.Contains("FLOOD_FAILURE", error, StringComparison.Ordinal);
        Assert.DoesNotContain(token, error, StringComparison.Ordinal);
        Assert.Contains("***", error, StringComparison.Ordinal);
        Assert.Contains("stderr-flood-0", error, StringComparison.Ordinal);
        Assert.Contains("[truncated", error, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(error) <= 68 * 1024,
            $"Git failure diagnostic was {Encoding.UTF8.GetByteCount(error)} UTF-8 bytes.");
        AssertDeploymentCleanupSucceeded(scope);
    }

    [Fact]
    public async Task Deploy_AskpassScript_DoesNotLeakInError()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.FakeGit.PushMode = "askpass-leak";
        scope.SetGithubToken("ghp_TEST_SECRET_TOKEN_123");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.False(result.Success);
        var askpassPath = scope.FakeGit.ReadAskpassPath();
        Assert.False(string.IsNullOrWhiteSpace(askpassPath));
        Assert.DoesNotContain("ghp_TEST_SECRET_TOKEN_123", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(askpassPath!, result.Error, StringComparison.Ordinal);
        Assert.Contains("[redacted-path]", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deploy_AskpassFile_IsDeletedAfterFailure()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.FakeGit.PushMode = "forbidden";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.False(result.Success);
        AssertDeploymentCleanupSucceeded(scope);
    }

    [Fact]
    public async Task Deploy_AskpassFile_IsDeletedAfterSuccess()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.True(result.Success, result.Error);
        var askpassPath = scope.FakeGit.ReadAskpassPath();
        Assert.False(string.IsNullOrWhiteSpace(askpassPath));
        Assert.False(File.Exists(askpassPath));
    }

    [Fact]
    public async Task Deploy_TempDir_IsDeletedAfterFailure()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.FakeGit.PushMode = "forbidden";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.False(result.Success);
        AssertDeploymentCleanupSucceeded(scope);
    }

    [Fact]
    public async Task Deploy_TempDir_IsDeletedAfterSuccess()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.True(result.Success, result.Error);
        var askpassPath = scope.FakeGit.ReadAskpassPath();
        Assert.False(string.IsNullOrWhiteSpace(askpassPath));
        Assert.False(Directory.Exists(Path.GetDirectoryName(askpassPath)!));
    }

    [Fact]
    public async Task DeployAsync_MissingGitOnPath_ReturnsHelpfulError()
    {
        using var scope = new GitHubPagesDeployTestScope(includeFakeGitInPath: false);
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("git command not found. Please install git and ensure it is in PATH.", result.Error);
    }

    [Fact]
    public async Task Deploy_OutputDirMissing_ReturnsFriendlyError()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.SetGithubToken("secret-token");

        var missingOutput = Path.Combine(scope.WorktreeDir, "missing-output");
        var result = await new GitHubPagesDeployProvider().DeployAsync(
            scope.CreateContext(outputDir: missingOutput),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal($"Output directory not found: {missingOutput}", result.Error);
    }

    [Fact]
    public async Task Deploy_OutputDirEmpty_ReturnsFriendlyError()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.SetGithubToken("secret-token");

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal($"Output directory is empty: {scope.OutputDir}", result.Error);
    }

    [Theory]
    [InlineData("https://github.com/ali/docs.git", "https://ali.github.io/docs")]
    [InlineData("git@github.com:ali/docs.git", "https://ali.github.io/docs")]
    public async Task Deploy_GitHubRemoteUrlParser_SupportsHttpsAndSsh(string remoteUrl, string expectedUrl)
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = remoteUrl;
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal(expectedUrl, result.DeployedUrl);
    }

    [Theory]
    [InlineData("https://gitlab.com/ali/docs.git")]
    [InlineData("https://example.com/github.com/ali/docs.git")]
    [InlineData("not a remote url")]
    public async Task Deploy_GitHubRemoteUrlParser_RejectsUnsupportedRemote_DoNotLeakToken(string remoteUrl)
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = remoteUrl;
        scope.SetGithubToken("ghp_TEST_SECRET_TOKEN_123");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Unable to determine GitHub repository", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("ghp_TEST_SECRET_TOKEN_123", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("ghp_TEST_SECRET_TOKEN_123", string.Join('\n', scope.Logger.Errors), StringComparison.Ordinal);
        Assert.DoesNotContain("ghp_TEST_SECRET_TOKEN_123", string.Join('\n', scope.Logger.Infos), StringComparison.Ordinal);
        Assert.DoesNotContain("ghp_TEST_SECRET_TOKEN_123", string.Join('\n', scope.Logger.Warnings), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deploy_GitHubRemoteUrlParser_SupportsSshScpStyle()
    {
        await AssertGitHubRemoteUrlDeploysAsync("git@github.com:ali/docs.git", "https://ali.github.io/docs");
    }

    [Fact]
    public async Task Deploy_GitHubRemoteUrlParser_SupportsHttpsGit()
    {
        await AssertGitHubRemoteUrlDeploysAsync("https://github.com/ali/docs.git", "https://ali.github.io/docs");
    }

    [Fact]
    public async Task Deploy_GitHubRemoteUrlParser_SupportsHttpsWithoutGitSuffix()
    {
        await AssertGitHubRemoteUrlDeploysAsync("https://github.com/ali/docs", "https://ali.github.io/docs");
    }

    [Fact]
    public async Task Deploy_GitHubRemoteUrlParser_SupportsSshUrl()
    {
        await AssertGitHubRemoteUrlDeploysAsync("ssh://git@github.com/ali/docs.git", "https://ali.github.io/docs");
    }

    [Fact]
    public async Task Deploy_GitHubRemoteUrlParser_SupportsHttpsTrailingSlash()
    {
        await AssertGitHubRemoteUrlDeploysAsync("https://github.com/ali/docs/", "https://ali.github.io/docs");
    }

    [Fact]
    public async Task Deploy_GitHubRemoteUrlParser_RejectsNonGitHubRemote()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://gitlab.com/ali/docs.git";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Unable to determine GitHub repository", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deploy_GitHubRemoteUrlParser_RejectsEmbeddedGitHubHost()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://example.com/github.com/ali/docs.git";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Unable to determine GitHub repository", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deploy_GitHubRemoteUrlParser_RejectsMalformedRemote()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "not a remote url";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Unable to determine GitHub repository", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deploy_NonFastForwardWithForce_UsesForcePush()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.FakeGit.PushMode = "nonff-once";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(force: true), CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Matches(@"push --force bukit-deploy-[0-9a-f]+ gh-pages", scope.FakeGit.ReadLog());
    }

    [Fact]
    public async Task Deploy_ForceFlag_OnlyAffectsPush()
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.FakeGit.RemoteHeads = "abc123\trefs/heads/gh-pages";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(force: true), CancellationToken.None);

        Assert.True(result.Success, result.Error);
        var log = scope.FakeGit.ReadLog();
        Assert.Contains("init", log, StringComparison.Ordinal);
        Assert.Contains("fetch --depth 1 bukit-deploy-", log, StringComparison.Ordinal);
        Assert.Contains(" gh-pages", log, StringComparison.Ordinal);
        Assert.Contains("checkout -B gh-pages FETCH_HEAD", log, StringComparison.Ordinal);
        Assert.Matches(@"push --force bukit-deploy-[0-9a-f]+ gh-pages", log);
    }

    [Fact]
    public void SanitizeError_RedactsToken()
    {
        var result = InvokePrivateStatic<string>(
            nameof(GitHubPagesDeployProvider),
            "SanitizeError",
            ["push failed for token-123", "token-123", Array.Empty<string?>()]);

        Assert.Equal("push failed for ***", result);
    }

    [Fact]
    public void SanitizeError_RedactsSensitivePaths()
    {
        var result = InvokePrivateStatic<string>(
            nameof(GitHubPagesDeployProvider),
            "SanitizeError",
            [
                "cannot run /tmp/bukit-deploy-123/git-askpass from /tmp/bukit-deploy-123",
                "token",
                new[] { "/tmp/bukit-deploy-123/git-askpass", "/tmp/bukit-deploy-123" }
            ]);

        Assert.DoesNotContain("/tmp/bukit-deploy-123", result, StringComparison.Ordinal);
        Assert.Contains("[redacted-path]", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("403 Forbidden", "Ensure your GITHUB_TOKEN has 'repo' scope")]
    [InlineData("fatal: unable to access remote: Could not resolve host: github.com", "Check your network connection and ensure GitHub is reachable.")]
    [InlineData("Permission denied", "Verify your GITHUB_TOKEN is valid and has 'repo' scope.")]
    public void AugmentErrorHint_KnownFailureModes_AppendsHelpfulHint(string message, string expectedHint)
    {
        var result = InvokePrivateStatic<string>(
            nameof(GitHubPagesDeployProvider),
            "AugmentErrorHint",
            [message]);

        Assert.Contains(expectedHint, result, StringComparison.Ordinal);
    }

    [Fact]
    public void CopyDirectory_SkipsInternalBuildArtifactsAndPreservesPublicDotfiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-gh-pages-copy-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var dest = Path.Combine(root, "dest");
        var outside = Path.Combine(root, "outside");

        try
        {
            Directory.CreateDirectory(Path.Combine(source, ".git"));
            Directory.CreateDirectory(Path.Combine(source, ".bukit"));
            Directory.CreateDirectory(Path.Combine(source, ".GIT"));
            Directory.CreateDirectory(Path.Combine(source, ".BUKIT"));
            Directory.CreateDirectory(Path.Combine(source, ".well-known"));
            Directory.CreateDirectory(Path.Combine(source, "assets"));
            Directory.CreateDirectory(outside);
            File.WriteAllText(Path.Combine(source, "index.html"), "<h1>Hello</h1>");
            File.WriteAllText(Path.Combine(source, "assets", "app.css"), "body{}");
            File.WriteAllText(Path.Combine(source, ".git", "config"), "[core]");
            File.WriteAllText(Path.Combine(source, ".bukit", "publish-audit-report.json"), "{}");
            File.WriteAllText(Path.Combine(source, ".bukit-build-state.json"), "{}");
            File.WriteAllText(Path.Combine(source, ".bukit-output-marker"), "bukit");
            File.WriteAllText(Path.Combine(source, ".BUKIT-BUILD-STATE.JSON"), "{}");
            File.WriteAllText(Path.Combine(source, ".well-known", "security.txt"), "Contact: mailto:security@example.com");
            File.WriteAllText(Path.Combine(outside, "secret.txt"), "secret");
            Directory.CreateSymbolicLink(Path.Combine(source, "linked-outside"), outside);
            File.CreateSymbolicLink(Path.Combine(source, "linked-secret.txt"), Path.Combine(outside, "secret.txt"));

            InvokePrivateStatic<object?>(
                nameof(GitHubPagesDeployProvider),
                "CopyDirectory",
                [source, dest]);

            Assert.True(File.Exists(Path.Combine(dest, "index.html")));
            Assert.True(File.Exists(Path.Combine(dest, "assets", "app.css")));
            Assert.True(File.Exists(Path.Combine(dest, ".well-known", "security.txt")));
            Assert.False(Directory.Exists(Path.Combine(dest, ".git")));
            Assert.False(Directory.Exists(Path.Combine(dest, ".bukit")));
            Assert.False(Directory.Exists(Path.Combine(dest, ".GIT")));
            Assert.False(Directory.Exists(Path.Combine(dest, ".BUKIT")));
            Assert.False(File.Exists(Path.Combine(dest, ".bukit-build-state.json")));
            Assert.False(File.Exists(Path.Combine(dest, ".bukit-output-marker")));
            Assert.False(File.Exists(Path.Combine(dest, ".BUKIT-BUILD-STATE.JSON")));
            Assert.False(Directory.Exists(Path.Combine(dest, "linked-outside")));
            Assert.False(File.Exists(Path.Combine(dest, "linked-secret.txt")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void HasDeployableOutputFiles_IgnoresInternalArtifactsAndSymlinks()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-gh-pages-output-scan-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(root, "output");
        var outside = Path.Combine(root, "outside.txt");
        try
        {
            Directory.CreateDirectory(Path.Combine(output, ".bukit"));
            File.WriteAllText(Path.Combine(output, ".bukit", "publish-audit-report.json"), "{\"documents\":[]}");
            File.WriteAllText(Path.Combine(output, ".bukit-output-marker"), "bukit");
            File.WriteAllText(outside, "outside");
            File.CreateSymbolicLink(Path.Combine(output, "linked.txt"), outside);

            Assert.False(InvokePrivateStatic<bool>(
                nameof(GitHubPagesDeployProvider),
                "HasDeployableOutputFiles",
                [output]));

            Directory.CreateDirectory(Path.Combine(output, ".well-known"));
            File.WriteAllText(Path.Combine(output, ".well-known", "security.txt"), "Contact: mailto:security@example.com");

            Assert.True(InvokePrivateStatic<bool>(
                nameof(GitHubPagesDeployProvider),
                "HasDeployableOutputFiles",
                [output]));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DeployAsync_PublicOutputContainsKnownNotionIdentifier_StopsBeforeStagingOrPush()
    {
        const string notionId = "39bfa39a-5013-81ae-9516-fbd448f3bd47";
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", $"<p>posts:{notionId}</p>");
        scope.WriteOutputFile(
            ".bukit/publish-audit-report.json",
            $$"""{"schema":"https://bukit.dev/schemas/publish-audit-report.v1.json","documents":[{"source":"notion","sourceItemId":"posts:{{notionId}}"}]}""");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("BKT-DEPLOY-PRIVACY-0001", result.Error, StringComparison.Ordinal);
        Assert.Contains("index.html", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(notionId, result.Error, StringComparison.OrdinalIgnoreCase);
        var log = scope.FakeGit.ReadLog();
        Assert.DoesNotContain("add -A", log, StringComparison.Ordinal);
        Assert.DoesNotContain("push origin", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeployAsync_EscapedIdentifier_StopsBeforeGitWrites()
    {
        const string notionId = "39bfa39a-5013-81ae-9516-fbd448f3bd47";
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Safe</h1>");
        scope.WriteOutputFile("escaped.json", """{"reference":"39bf\u006139a-5013-81ae-9516-fbd448f3bd47"}""");
        WriteDeployIdentityReport(scope.OutputDir, notionId);
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("BKT-DEPLOY-PRIVACY-0001", result.Error, StringComparison.Ordinal);
        Assert.Contains("escaped.json", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(notionId, result.Error, StringComparison.OrdinalIgnoreCase);
        var log = scope.FakeGit.ReadLog();
        Assert.DoesNotContain("add -A", log, StringComparison.Ordinal);
        Assert.DoesNotContain("|push ", log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"x\":")]
    [InlineData("{\"x\":\"\\uD800\"}")]
    [InlineData("{\"\\uD800\":\"x\"}")]
    public async Task DeployAsync_IncompletePrivacyScan_ReturnsPrivacyErrorBeforeGitWrites(string json)
    {
        const string notionId = "39bfa39a-5013-81ae-9516-fbd448f3bd47";
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Safe</h1>");
        scope.WriteOutputFile("undecodable.json", json);
        WriteDeployIdentityReport(scope.OutputDir, notionId);
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("BKT-DEPLOY-PRIVACY-0001", result.Error, StringComparison.Ordinal);
        Assert.Contains("unable to complete", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contains internal Notion provenance", result.Error, StringComparison.Ordinal);
        var log = scope.FakeGit.ReadLog();
        Assert.DoesNotContain("add -A", log, StringComparison.Ordinal);
        Assert.DoesNotContain("|push ", log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".json")]
    [InlineData(".jsonld")]
    public async Task DeployAsync_OverDeepJson_ReturnsIncompleteBeforeGitWrites(string extension)
    {
        const string notionId = "39bfa39a-5013-81ae-9516-fbd448f3bd47";
        var escaped = string.Concat(notionId.Select(ch => $"\\u{(int)ch:x4}"));
        var depth65 = new string('[', 65) + "\"" + escaped + "\"" + new string(']', 65);
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Safe</h1>");
        scope.WriteOutputFile("depth65" + extension, depth65);
        WriteDeployIdentityReport(scope.OutputDir, notionId);
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("BKT-DEPLOY-PRIVACY-0001", result.Error, StringComparison.Ordinal);
        Assert.Contains("unable to complete", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contains internal Notion provenance", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(notionId, result.Error, StringComparison.OrdinalIgnoreCase);
        var log = scope.FakeGit.ReadLog();
        Assert.DoesNotContain("add -A", log, StringComparison.Ordinal);
        Assert.DoesNotContain("|push ", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeployAsync_ValidUnicodeAndUnknownUuid_ProceedsToPush()
    {
        const string notionId = "39bfa39a-5013-81ae-9516-fbd448f3bd47";
        const string businessId = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = "https://github.com/ali/docs.git";
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Safe</h1>");
        scope.WriteOutputFile("emoji.json", """{"x":"\uD83D\uDE00"}""");
        scope.WriteOutputFile("business.json", $$"""{"businessId":"{{businessId}}"}""");
        WriteDeployIdentityReport(scope.OutputDir, notionId);
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Contains("|push ", scope.FakeGit.ReadLog(), StringComparison.Ordinal);
    }

    private static void WriteDeployIdentityReport(string outputDir, string notionId)
    {
        Directory.CreateDirectory(Path.Combine(outputDir, ".bukit"));
        File.WriteAllText(
            Path.Combine(outputDir, ".bukit", "publish-audit-report.json"),
            $$"""{"schema":"https://bukit.dev/schemas/publish-audit-report.v1.json","documents":[{"source":"notion","sourceItemId":"posts:{{notionId}}"}]}""");
    }

    [Fact]
    public void DeploymentPrivacyValidator_IgnoresInternalReportsAndUnrelatedBusinessUuid()
    {
        const string notionId = "39bfa39a-5013-81ae-9516-fbd448f3bd47";
        const string businessId = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";
        var root = Path.Combine(Path.GetTempPath(), "bukit-deploy-privacy-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(root, "output");
        var staged = Path.Combine(root, "staged");

        try
        {
            Directory.CreateDirectory(Path.Combine(output, ".bukit"));
            Directory.CreateDirectory(staged);
            File.WriteAllText(
                Path.Combine(output, ".bukit", "publish-audit-report.json"),
                $$"""{"schema":"https://bukit.dev/schemas/publish-audit-report.v1.json","documents":[{"source":"notion","sourceItemId":"posts:{{notionId}}"}]}""");
            File.WriteAllText(Path.Combine(staged, "public.json"), $$"""{"businessId":"{{businessId}}"}""");

            var errors = DeploymentPrivacyValidator.Validate(output, staged);

            Assert.Empty(errors);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeploymentPrivacyValidator_FailsClosedWhenIdentityReportIsMissingOrMalformed(bool malformed)
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-deploy-privacy-report-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(root, "output");
        var staged = Path.Combine(root, "staged");
        try
        {
            Directory.CreateDirectory(Path.Combine(output, ".bukit"));
            Directory.CreateDirectory(staged);
            File.WriteAllText(Path.Combine(staged, "index.html"), "<h1>Safe</h1>");
            if (malformed)
            {
                File.WriteAllText(Path.Combine(output, ".bukit", "publish-audit-report.json"), "{");
            }

            var errors = DeploymentPrivacyValidator.Validate(output, staged);

            Assert.Contains(errors, error => error.Contains("BKT-DEPLOY-PRIVACY-0002", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void DeploymentPrivacyValidator_DetectsCompactIdentifierAndStructuredMarkersWithoutEchoingUuidPath()
    {
        const string notionId = "39bfa39a-5013-81ae-9516-fbd448f3bd47";
        const string unknownUuid = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";
        var root = Path.Combine(Path.GetTempPath(), "bukit-deploy-privacy-markers-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(root, "output");
        var staged = Path.Combine(root, "staged");
        try
        {
            Directory.CreateDirectory(Path.Combine(output, ".bukit"));
            Directory.CreateDirectory(staged);
            File.WriteAllText(
                Path.Combine(output, ".bukit", "publish-audit-report.json"),
                $$"""{"schema":"https://bukit.dev/schemas/publish-audit-report.v1.json","documents":[{"source":"notion","sourceItemId":"posts:{{notionId}}"}]}""");
            File.WriteAllText(Path.Combine(staged, "compact.txt"), notionId.Replace("-", string.Empty, StringComparison.Ordinal));
            File.WriteAllText(Path.Combine(staged, unknownUuid + ".json"), """{"source":"not\u0069on"}""");
            File.WriteAllText(Path.Combine(staged, "metadata.yaml"), "sourceKey: notion\n");

            var errors = DeploymentPrivacyValidator.Validate(output, staged);

            Assert.Contains(errors, error => error.Contains("compact.txt", StringComparison.Ordinal));
            Assert.Contains(errors, error => error.Contains("[redacted-notion-id].json", StringComparison.Ordinal));
            Assert.Contains(errors, error => error.Contains("metadata.yaml", StringComparison.Ordinal));
            Assert.DoesNotContain(errors, error => error.Contains(unknownUuid, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"schema\":\"https://bukit.dev/schemas/wrong.v1.json\",\"documents\":[]}")]
    [InlineData("{\"schema\":\"https://bukit.dev/schemas/publish-audit-report.v1.json\",\"documents\":{}}")]
    public void DeploymentPrivacyValidator_FailsClosedWhenIdentityReportContractIsInvalid(string reportJson)
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-deploy-privacy-contract-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(root, "output");
        var staged = Path.Combine(root, "staged");
        try
        {
            Directory.CreateDirectory(Path.Combine(output, ".bukit"));
            Directory.CreateDirectory(staged);
            File.WriteAllText(Path.Combine(output, ".bukit", "publish-audit-report.json"), reportJson);
            File.WriteAllText(Path.Combine(staged, "index.html"), "<h1>Safe</h1>");

            var errors = DeploymentPrivacyValidator.Validate(output, staged);

            Assert.Contains(errors, error => error.Contains("BKT-DEPLOY-PRIVACY-0002", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void CreateAskpassScript_AndCleanupAskpassScript_CreateEnvBackedScriptWithoutTokenLiteral()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit-gh-pages-askpass-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempDir);
            var token = "secret-token\"'$()&|";
            var scriptPath = InvokePrivateStatic<string>(
                nameof(GitHubPagesDeployProvider),
                "CreateAskpassScript",
                [tempDir, token]);

            Assert.True(File.Exists(scriptPath));
            var contents = File.ReadAllText(scriptPath);
            Assert.DoesNotContain(token, contents, StringComparison.Ordinal);
            Assert.Contains("BUKIT_GITHUB_TOKEN", contents, StringComparison.Ordinal);
            Assert.Equal(token, RunAskpassScript(scriptPath, token));

            InvokePrivateStatic<object?>(
                nameof(GitHubPagesDeployProvider),
                "CleanupAskpassScript",
                [scriptPath]);

            Assert.False(File.Exists(scriptPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static string RunAskpassScript(string scriptPath, string token)
    {
        var psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", $"/c \"{scriptPath}\"")
            : new ProcessStartInfo(scriptPath);
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        psi.Environment["BUKIT_GITHUB_TOKEN"] = token;

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start askpass script.");
        var output = proc.StandardOutput.ReadToEnd();
        var error = proc.StandardError.ReadToEnd();
        Assert.True(proc.WaitForExit(5000), "askpass script did not exit.");
        Assert.Equal(0, proc.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(error), error);
        return output.TrimEnd('\r', '\n');
    }

    private static T InvokePrivateStatic<T>(string typeName, string methodName, object?[] args)
    {
        var type = typeof(GitHubPagesDeployProvider);
        Assert.Equal(typeName, type.Name);

        var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method!.Invoke(null, args);
        return result is null ? default! : (T)result;
    }

    private static void AssertDiagnosticSlicesAreUtf8ByteBounded(string diagnostic)
    {
        const string markerStart = "\n... [truncated ";
        var markerIndex = diagnostic.IndexOf(markerStart, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, "Expected a diagnostic truncation marker.");
        var tailStart = diagnostic.IndexOf("] ...\n", markerIndex, StringComparison.Ordinal);
        Assert.True(tailStart >= 0, "Expected a complete diagnostic truncation marker.");

        var head = diagnostic[..markerIndex];
        var tail = diagnostic[(tailStart + "] ...\n".Length)..];
        Assert.True(Encoding.UTF8.GetByteCount(head) <= 32 * 1024);
        Assert.True(Encoding.UTF8.GetByteCount(tail) <= 32 * 1024);
    }

    private static string WriteExecutable(string directory, string name, string body)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, "#!/bin/sh\n" + body + "\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        return path;
    }

    private static async Task AssertGitHubRemoteUrlDeploysAsync(string remoteUrl, string expectedUrl)
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.FakeGit.RemoteUrl = remoteUrl;
        scope.SetGithubToken("secret-token");
        scope.WriteOutputFile("index.html", "<h1>Hello</h1>");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);

        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal(expectedUrl, result.DeployedUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("[]")]
    [InlineData("{\"version\":2,\"status\":\"completed\"}")]
    [InlineData("{\"version\":1,\"status\":\"started\"}")]
    [InlineData("{\"version\":1,\"status\":42}")]
    public async Task DeployAsync_UnreadyOutputNeverInvokesGit(string? state)
    {
        using var scope = new GitHubPagesDeployTestScope();
        scope.SetGithubToken("dummy-token");
        scope.WriteOutputFile("index.html", "hello");
        var path = Path.Combine(scope.OutputDir, ".bukit-build-state.json");
        File.Delete(path);
        if (state is not null) File.WriteAllText(path, state);
        var result = await new GitHubPagesDeployProvider().DeployAsync(scope.CreateContext(), CancellationToken.None);
        Assert.False(result.Success);
        Assert.Contains("Rebuild", result.Error);
        Assert.DoesNotContain("ls-remote", scope.FakeGit.ReadLog());
    }

    [Fact]
    public async Task DeployAsync_RealLocalGitClonesAndPushesWithAskpassAlive()
        => await DeployRealLocalGitAndAssertCleanupAsync("success");

    [WindowsFact]
    public async Task DeployAsync_RealLocalGitPushFailure_CleansReadOnlyObjectsAndAskpass()
        => await DeployRealLocalGitAndAssertCleanupAsync("reject");

    [WindowsFact]
    public async Task DeployAsync_RealLocalGitPushCancellation_CleansReadOnlyObjectsAndAskpass()
        => await DeployRealLocalGitAndAssertCleanupAsync("wait");

    private static async Task DeployRealLocalGitAndAssertCleanupAsync(string pushBehavior)
    {
        var realGit = InvokePrivateStatic<string?>(nameof(GitHubPagesDeployProvider), "ResolveGit", [null])!;
        using var scope = new GitHubPagesDeployTestScope();
        scope.SetGithubToken("dummy-token");
        scope.WriteOutputFile("index.html", "new page");
        using var cwd = new CurrentDirectoryScope(scope.WorktreeDir);
        var remote = Path.Combine(scope.WorktreeDir, "remote.git");
        var accessLog = Path.Combine(scope.WorktreeDir, "transport-access.log");
        if (OperatingSystem.IsWindows())
        {
            scope.UseRealGitShim(realGit, remote, accessLog, Path.Combine(scope.WorktreeDir, "askpass-path"), pushBehavior);
        }

        async Task<string> Git(params string[] args)
        {
            var start = new ProcessStartInfo(realGit) { WorkingDirectory = scope.WorktreeDir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (var arg in args) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(5));
                Assert.True(process.ExitCode == 0, await stderr);
                return await stdout;
            }
            finally
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
        }
        await Git("init", "--bare", remote);
        await Git("init");
        await Git("config", "user.name", "test");
        await Git("config", "user.email", "test@example.invalid");
        await Git("checkout", "-b", "pages");
        File.WriteAllText(Path.Combine(scope.WorktreeDir, "old.html"), "old page");
        await Git("add", "old.html");
        await Git("commit", "-m", "seed");
        await Git("push", remote, "pages");
        await Git("remote", "add", "origin", "https://github.com/test/local.git");
        string Quote(string value) => "'" + value.Replace("'", "'\\''") + "'";
        // Controlled transport double: real git for every command, but the deploy-private
        // remote name is mapped to the local bare repository. Records each real access.
        if (!OperatingSystem.IsWindows())
        {
            WriteExecutable(scope.FakeGit.BinDir, "git", $$"""
            set -eu
            case "${1:-}" in
              ls-remote|fetch|push)
                if [ -n "${GIT_ASKPASS:-}" ]; then
                  [ -f "$GIT_ASKPASS" ] || exit 71
                  printf '%s' "$GIT_ASKPASS" > {{Quote(Path.Combine(scope.WorktreeDir, "askpass-path"))}}
                fi
                printf 'ACCESS %s\n' {{Quote(remote)}} >> {{Quote(accessLog)}}
                mapped=""
                for arg in "$@"; do
                  case "$arg" in
                    bukit-deploy-*) arg={{Quote(remote)}} ;;
                  esac
                  mapped="$mapped '$arg'"
                done
                eval "exec {{Quote(realGit)}} $mapped"
                ;;
              *)
                exec {{Quote(realGit)}} "$@"
                ;;
            esac
            """);
        }
        using var cancellation = new CancellationTokenSource();
        var deployment = new GitHubPagesDeployProvider().DeployAsync(
            scope.CreateContext(keepHistory: true, branch: "pages", cname: "WWW.Example.Com"),
            cancellation.Token);
        if (pushBehavior == "wait")
        {
            var readiness = Stopwatch.StartNew();
            var marker = Path.Combine(scope.WorktreeDir, "real-push-started");
            try
            {
                while (!File.Exists(marker) && !deployment.IsCompleted && readiness.Elapsed < TimeSpan.FromSeconds(30))
                {
                    await Task.Delay(25);
                }
                Assert.True(File.Exists(marker), "Real Git fetch must finish and the push barrier must be reached before cancellation.");
            }
            finally
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await deployment.WaitAsync(TimeSpan.FromSeconds(10)));
            }
        }
        else
        {
            var result = await deployment;
            if (pushBehavior == "success")
            {
                Assert.True(result.Success, result.Error);
                Assert.Equal("https://www.example.com", result.DeployedUrl);
                Assert.Equal("new page", (await Git("--git-dir", remote, "show", "pages:index.html")).Trim());
                Assert.Equal("www.example.com", (await Git("--git-dir", remote, "show", "pages:CNAME")).Trim());
            }
            else
            {
                Assert.False(result.Success);
                Assert.Contains("fixture push rejected", result.Error, StringComparison.Ordinal);
            }
        }
        var files = await Git("--git-dir", remote, "ls-tree", "--name-only", "pages");
        if (pushBehavior == "success") Assert.DoesNotContain("old.html", files);
        else Assert.Equal("old page", (await Git("--git-dir", remote, "show", "pages:old.html")).Trim());
        Assert.DoesNotContain("askpass", files);
        Assert.DoesNotContain(".bukit", files);
        var askpass = File.ReadAllText(Path.Combine(scope.WorktreeDir, "askpass-path"));
        Assert.False(File.Exists(askpass));
        Assert.False(Directory.Exists(Path.GetDirectoryName(askpass)), "The deployment temporary repository must be removed, including read-only Git objects.");
        var accesses = File.ReadAllLines(accessLog);
        Assert.Equal(3, accesses.Length);
        Assert.All(accesses, line => Assert.Equal("ACCESS " + remote, line));
    }

    private static void AssertDeploymentCleanupSucceeded(GitHubPagesDeployTestScope scope)
    {
        var askpassPath = scope.FakeGit.ReadAskpassPath();
        Assert.False(string.IsNullOrWhiteSpace(askpassPath));
        Assert.False(File.Exists(askpassPath));
        Assert.False(Directory.Exists(Path.GetDirectoryName(askpassPath)!));
    }

    private sealed class GitHubPagesDeployTestScope : IDisposable
    {
        private readonly string _root;
        private readonly Dictionary<string, string?> _originalEnv = new(StringComparer.Ordinal);
        private bool _hasRealGitRepositories;

        public GitHubPagesDeployTestScope(bool includeFakeGitInPath = true)
        {
            _root = Path.Combine(Path.GetTempPath(), "bukit-gh-pages-provider-" + Guid.NewGuid().ToString("N"));
            WorktreeDir = Path.Combine(_root, "worktree");
            OutputDir = Path.Combine(_root, "output");
            Directory.CreateDirectory(WorktreeDir);
            Directory.CreateDirectory(Path.Combine(WorktreeDir, ".git"));
            Directory.CreateDirectory(OutputDir);
            File.WriteAllText(Path.Combine(OutputDir, ".bukit-build-state.json"), "{\"version\":1,\"status\":\"completed\"}");
            Logger = new RecordingLogger();
            FakeGit = new FakeGitHarness(_root);

            SetEnv("PATH", includeFakeGitInPath
                ? FakeGit.PrependPath(Environment.GetEnvironmentVariable("PATH"))
                : string.Empty);
            SetEnv("BUKIT_FAKE_GIT_LOG", FakeGit.LogPath);
            SetEnv("BUKIT_FAKE_GIT_STATE", FakeGit.StateDir);
            if (OperatingSystem.IsWindows())
            {
                SetEnv("BUKIT_TEST_GIT_SCRIPT", Path.Combine(FakeGit.BinDir, "git.ps1"));
            }
        }

        public string WorktreeDir { get; }
        public string OutputDir { get; }
        public RecordingLogger Logger { get; }
        public FakeGitHarness FakeGit { get; }

        public DeployContext CreateContext(bool keepHistory = false, bool force = false, string? branch = null, string? message = null, string? cname = null, string? outputDir = null)
            => new()
            {
                OutputDir = outputDir ?? OutputDir,
                SiteUrl = "https://example.com",
                BaseUrl = "/",
                Branch = branch,
                Message = message,
                Cname = cname,
                KeepHistory = keepHistory,
                Force = force,
                Logger = Logger
            };

        public void WriteOutputFile(string relativePath, string content)
        {
            var fullPath = Path.Combine(OutputDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
            if (!relativePath.Replace('\\', '/').StartsWith(".bukit/", StringComparison.OrdinalIgnoreCase))
            {
                var reportPath = Path.Combine(OutputDir, ".bukit", "publish-audit-report.json");
                if (!File.Exists(reportPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
                    File.WriteAllText(reportPath, "{\"schema\":\"https://bukit.dev/schemas/publish-audit-report.v1.json\",\"documents\":[]}");
                }
            }
        }

        public void SetGithubToken(string? token) => SetEnv("GITHUB_TOKEN", token);

        public void SetDeployTimeoutSeconds(string? seconds) => SetEnv("BUKIT_DEPLOY_GIT_TIMEOUT_SECONDS", seconds);

        public void UseRealGitShim(string realGit, string remote, string accessLog, string askpassLog, string pushBehavior = "success")
        {
            _hasRealGitRepositories = true;
            var config = Path.Combine(_root, "isolated.gitconfig");
            File.WriteAllText(config, string.Empty);
            foreach (var name in new[]
                     {
                         "GIT_DIR", "GIT_WORK_TREE", "GIT_COMMON_DIR", "GIT_INDEX_FILE",
                         "GIT_OBJECT_DIRECTORY", "GIT_ALTERNATE_OBJECT_DIRECTORIES", "GIT_IMPLICIT_WORK_TREE",
                         "GIT_GRAFT_FILE", "GIT_NO_REPLACE_OBJECTS", "GIT_REPLACE_REF_BASE", "GIT_PREFIX",
                         "GIT_INTERNAL_SUPER_PREFIX", "GIT_SHALLOW_FILE", "GIT_CONFIG",
                         "GIT_CONFIG_PARAMETERS", "GIT_CONFIG_COUNT", "GIT_CONFIG_NOSYSTEM"
                     })
            {
                SetEnv(name, null);
            }
            foreach (var name in Environment.GetEnvironmentVariables().Keys.Cast<string>()
                         .Where(name => name.StartsWith("GIT_CONFIG_KEY_", StringComparison.OrdinalIgnoreCase)
                                        || name.StartsWith("GIT_CONFIG_VALUE_", StringComparison.OrdinalIgnoreCase))
                         .ToArray())
            {
                SetEnv(name, null);
            }
            SetEnv("GIT_CONFIG_GLOBAL", config);
            SetEnv("GIT_CONFIG_SYSTEM", config);
            SetEnv("GIT_ALLOW_PROTOCOL", "file");
            SetEnv("BUKIT_TEST_GIT_SCRIPT", null);
            SetEnv("BUKIT_DEPLOY_REAL_GIT", realGit);
            SetEnv("BUKIT_DEPLOY_BARE_B", remote);
            SetEnv("BUKIT_DEPLOY_ACCESS_LOG", accessLog);
            SetEnv("BUKIT_DEPLOY_ASKPASS_LOG", askpassLog);
            SetEnv("BUKIT_DEPLOY_SIMPLE_LOG", "1");
            SetEnv("BUKIT_DEPLOY_PUSH_BEHAVIOR", pushBehavior);
            SetEnv("BUKIT_DEPLOY_PUSH_MARKER", Path.Combine(WorktreeDir, "real-push-started"));
        }

        public void Dispose()
        {
            FakeGit.Dispose();

            foreach (var entry in _originalEnv)
            {
                Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            }

            if (Directory.Exists(_root))
            {
                if (_hasRealGitRepositories && OperatingSystem.IsWindows())
                {
                    var options = new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        AttributesToSkip = FileAttributes.ReparsePoint
                    };
                    foreach (var file in Directory.EnumerateFiles(_root, "*", options))
                    {
                        File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
                    }
                }
                Directory.Delete(_root, recursive: true);
            }
        }

        private void SetEnv(string key, string? value)
        {
            if (!_originalEnv.ContainsKey(key))
            {
                _originalEnv[key] = Environment.GetEnvironmentVariable(key);
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private sealed class FakeGitHarness : IDisposable
    {
        public FakeGitHarness(string root)
        {
            BinDir = Path.Combine(root, "fake-git-bin");
            StateDir = Path.Combine(root, "fake-git-state");
            LogPath = Path.Combine(root, "fake-git.log");
            Directory.CreateDirectory(BinDir);
            Directory.CreateDirectory(StateDir);
            WriteScript();
        }

        public string BinDir { get; }
        public string StateDir { get; }
        public string LogPath { get; }

        public string? RemoteUrl
        {
            get => Environment.GetEnvironmentVariable("BUKIT_FAKE_GIT_REMOTE_URL");
            set => Environment.SetEnvironmentVariable("BUKIT_FAKE_GIT_REMOTE_URL", value);
        }

        public string? RemoteHeads
        {
            get => Environment.GetEnvironmentVariable("BUKIT_FAKE_GIT_REMOTE_HEADS");
            set => Environment.SetEnvironmentVariable("BUKIT_FAKE_GIT_REMOTE_HEADS", value);
        }

        public string? PushMode
        {
            get => Environment.GetEnvironmentVariable("BUKIT_FAKE_GIT_PUSH_MODE");
            set => Environment.SetEnvironmentVariable("BUKIT_FAKE_GIT_PUSH_MODE", value);
        }

        public string? LsRemoteMode
        {
            get => Environment.GetEnvironmentVariable("BUKIT_FAKE_GIT_LS_REMOTE_MODE");
            set => Environment.SetEnvironmentVariable("BUKIT_FAKE_GIT_LS_REMOTE_MODE", value);
        }

        public string PushSleepStartedPath => Path.Combine(StateDir, "push-sleep-started.marker");

        public string PushSleepCompletedPath => Path.Combine(StateDir, "push-sleep-completed.marker");

        public string PushFloodOverflowCompletedPath => Path.Combine(StateDir, "push-flood-overflow-completed.marker");

        public string PrependPath(string? existingPath)
            => string.IsNullOrWhiteSpace(existingPath)
                ? BinDir
                : BinDir + Path.PathSeparator + existingPath;

        public string ReadLog()
            => File.Exists(LogPath) ? File.ReadAllText(LogPath) : string.Empty;

        public string? ReadAskpassPath()
            => File.ReadLines(LogPath)
                .Where(line => line.StartsWith("ASKPASS ", StringComparison.Ordinal))
                .Select(line => line["ASKPASS ".Length..])
                .LastOrDefault();

        public void SetDeployTimeoutSeconds(string? seconds) => Environment.SetEnvironmentVariable("BUKIT_DEPLOY_GIT_TIMEOUT_SECONDS", seconds);

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("BUKIT_FAKE_GIT_REMOTE_URL", null);
            Environment.SetEnvironmentVariable("BUKIT_FAKE_GIT_REMOTE_HEADS", null);
            Environment.SetEnvironmentVariable("BUKIT_FAKE_GIT_PUSH_MODE", null);
            Environment.SetEnvironmentVariable("BUKIT_FAKE_GIT_LS_REMOTE_MODE", null);
            Environment.SetEnvironmentVariable("BUKIT_DEPLOY_GIT_TIMEOUT_SECONDS", null);
        }

        private void WriteScript()
        {
            if (OperatingSystem.IsWindows())
            {
                WindowsGitShim.Install(BinDir);
                File.WriteAllText(Path.Combine(BinDir, "git.ps1"), """
                $log = $env:BUKIT_FAKE_GIT_LOG
                Add-Content -LiteralPath $log -Value ("$($PWD.Path)|" + ($args -join ' '))
                if ($env:GIT_ASKPASS) { Add-Content -LiteralPath $log -Value "ASKPASS $($env:GIT_ASKPASS)" }
                function Write-Nul([string]$value) { [Console]::Out.Write($value + [char]0) }

                if ($args.Count -ge 1 -and $args[0] -eq 'config') {
                    $last = $args[-1]
                    if ($last -eq 'remote.origin.url') {
                        if ($args[1] -eq '--worktree') {
                            if ($env:BUKIT_FAKE_GIT_WORKTREE_MODE -eq 'missing') { exit 1 }
                            $url = if ($env:BUKIT_FAKE_GIT_WORKTREE_URL) { $env:BUKIT_FAKE_GIT_WORKTREE_URL } else { $env:BUKIT_FAKE_GIT_REMOTE_URL }
                            if (-not $url) { exit 1 }
                            Write-Nul $url
                            exit 0
                        }
                        if ($env:BUKIT_FAKE_GIT_CONFIG_ERROR -eq '1') { [Console]::Error.Write("fatal: bad config`n"); exit 128 }
                        if (-not $env:BUKIT_FAKE_GIT_REMOTE_URL) { exit 1 }
                        Write-Nul $env:BUKIT_FAKE_GIT_REMOTE_URL
                        exit 0
                    }
                    if ($args -contains '--get-regexp') {
                        $path = Join-Path $env:BUKIT_FAKE_GIT_STATE 'rewrites'
                        if (Test-Path -LiteralPath $path) { [Console]::Out.Write([IO.File]::ReadAllText($path)); exit 0 }
                        exit 1
                    }
                    if ($last -like 'remote.*.url') {
                        $name = $last.Substring(7, $last.Length - 11)
                        $path = Join-Path $env:BUKIT_FAKE_GIT_STATE "remote.$name.url"
                        if (Test-Path -LiteralPath $path) { Write-Nul ([IO.File]::ReadAllText($path)); exit 0 }
                        exit 1
                    }
                    if ($last -like 'remote.*.pushurl') {
                        $name = $last.Substring(7, $last.Length - 15)
                        $path = Join-Path $env:BUKIT_FAKE_GIT_STATE "remote.$name.pushurl"
                        if (Test-Path -LiteralPath $path) { Write-Nul ([IO.File]::ReadAllText($path)); exit 0 }
                        exit 1
                    }
                    exit 0
                }

                if ($args[0] -eq 'init') { New-Item -ItemType Directory -Force .git | Out-Null; exit 0 }
                if ($args[0] -eq 'remote' -and $args[1] -eq 'add') {
                    Set-Content -LiteralPath (Join-Path $env:BUKIT_FAKE_GIT_STATE "remote.$($args[2]).url") -Value $args[3] -NoNewline
                    exit 0
                }
                if ($args[0] -eq 'fetch' -or $args[0] -eq 'checkout') { exit 0 }
                if ($args[0] -eq 'remote' -and $args[1] -eq 'get-url' -and $args[2] -eq 'origin') {
                    if ($env:BUKIT_FAKE_GIT_REMOTE_URL) { [Console]::Out.Write($env:BUKIT_FAKE_GIT_REMOTE_URL) }
                    exit 0
                }
                if ($args[0] -eq 'ls-remote') {
                    if ($env:BUKIT_FAKE_GIT_LS_REMOTE_MODE -eq 'forbidden') {
                        [Console]::Error.Write("remote: 403 Forbidden $($env:GITHUB_TOKEN)`n")
                        exit 1
                    }
                    if ($env:BUKIT_FAKE_GIT_REMOTE_HEADS) { [Console]::Out.Write($env:BUKIT_FAKE_GIT_REMOTE_HEADS + "`n") }
                    exit 0
                }
                if ($args[0] -eq 'push') {
                    $mode = if ($env:BUKIT_FAKE_GIT_PUSH_MODE) { $env:BUKIT_FAKE_GIT_PUSH_MODE } else { 'success' }
                    $marker = Join-Path $env:BUKIT_FAKE_GIT_STATE 'nonff.marker'
                    $force = $args -contains '--force'
                    if ($mode -eq 'nonff-once' -and -not (Test-Path -LiteralPath $marker) -and -not $force) {
                        Set-Content -LiteralPath $marker -Value '1'
                        [Console]::Error.Write("! [rejected] gh-pages -> gh-pages (non-fast-forward)`n")
                        exit 1
                    }
                    if ($mode -eq 'sleep') {
                        Set-Content -LiteralPath (Join-Path $env:BUKIT_FAKE_GIT_STATE 'push-sleep-started.marker') -Value 'started'
                        Start-Sleep -Seconds 30
                        Set-Content -LiteralPath (Join-Path $env:BUKIT_FAKE_GIT_STATE 'push-sleep-completed.marker') -Value 'completed'
                    }
                    if ($mode -eq 'forbidden') {
                        [Console]::Error.Write("remote: 403 Forbidden $($env:GITHUB_TOKEN)`n")
                        exit 1
                    }
                    if ($mode -eq 'askpass-leak') {
                        [Console]::Error.Write("fatal: cannot run $($env:GIT_ASKPASS) for token $($env:GITHUB_TOKEN)`n")
                        exit 1
                    }
                    if ($mode -eq 'flood-over-limit') {
                        [Console]::Out.Write(('x' * 5242880))
                        Set-Content -LiteralPath (Join-Path $env:BUKIT_FAKE_GIT_STATE 'push-flood-overflow-completed.marker') -Value 'completed'
                        exit 0
                    }
                    if ($mode -eq 'flood-success' -or $mode -eq 'flood-failure') {
                        for ($i = 0; $i -lt 4096; $i++) {
                            [Console]::Out.WriteLine("stdout-flood-$i-abcdefghijklmnopqrstuvwxyz0123456789")
                            [Console]::Error.WriteLine("stderr-flood-$i-abcdefghijklmnopqrstuvwxyz0123456789")
                        }
                        if ($mode -eq 'flood-failure') {
                            [Console]::Error.Write("FLOOD_FAILURE token=$($env:GITHUB_TOKEN)`n")
                            exit 1
                        }
                        exit 0
                    }
                    if (Test-Path -LiteralPath '.nojekyll') { Add-Content -LiteralPath $log -Value 'SNAPSHOT nojekyll' }
                    if (Test-Path -LiteralPath 'CNAME') { Add-Content -LiteralPath $log -Value ("SNAPSHOT cname=" + (Get-Content -LiteralPath 'CNAME' -Raw).Trim()) }
                    if (Test-Path -LiteralPath 'index.html') { Add-Content -LiteralPath $log -Value 'SNAPSHOT index' }
                    if (Test-Path -LiteralPath 'old.txt') { Add-Content -LiteralPath $log -Value 'SNAPSHOT old' }
                    exit 0
                }
                exit 0
                """);
                return;
            }

            var unixPath = Path.Combine(BinDir, "git");
            File.WriteAllText(unixPath, """
            #!/bin/sh
            set -eu
            echo "$PWD|$*" >> "$BUKIT_FAKE_GIT_LOG"
            if [ -n "${GIT_ASKPASS:-}" ]; then
              printf 'ASKPASS %s\n' "$GIT_ASKPASS" >> "$BUKIT_FAKE_GIT_LOG"
            fi
            if [ "${1:-}" = "config" ]; then
              last=""
              for arg in "$@"; do last="$arg"; done
              if [ "$last" = "remote.origin.url" ]; then
                if [ "${2:-}" = "--worktree" ]; then
                  if [ "${BUKIT_FAKE_GIT_WORKTREE_MODE:-present}" = "missing" ]; then
                    exit 1
                  fi
                  url="${BUKIT_FAKE_GIT_WORKTREE_URL:-${BUKIT_FAKE_GIT_REMOTE_URL:-}}"
                  if [ -z "$url" ]; then
                    exit 1
                  fi
                  printf '%s\0' "$url"
                  exit 0
                fi
                if [ "${BUKIT_FAKE_GIT_CONFIG_ERROR:-}" = "1" ]; then
                  printf 'fatal: bad config\n' >&2
                  exit 128
                fi
                if [ -z "${BUKIT_FAKE_GIT_REMOTE_URL:-}" ]; then
                  exit 1
                fi
                printf '%s\0' "$BUKIT_FAKE_GIT_REMOTE_URL"
                exit 0
              fi
              if [ "${4:-}" = "--get-regexp" ] || [ "${3:-}" = "--get-regexp" ]; then
                if [ -f "$BUKIT_FAKE_GIT_STATE/rewrites" ]; then
                  cat "$BUKIT_FAKE_GIT_STATE/rewrites"
                  exit 0
                fi
                exit 1
              fi
              case "$last" in
                remote.*.url)
                  name="${last#remote.}"
                  name="${name%.url}"
                  if [ -f "$BUKIT_FAKE_GIT_STATE/remote.$name.url" ]; then
                    printf '%s\0' "$(cat "$BUKIT_FAKE_GIT_STATE/remote.$name.url")"
                    exit 0
                  fi
                  exit 1
                  ;;
                remote.*.pushurl)
                  name="${last#remote.}"
                  name="${name%.pushurl}"
                  if [ -f "$BUKIT_FAKE_GIT_STATE/remote.$name.pushurl" ]; then
                    printf '%s\0' "$(cat "$BUKIT_FAKE_GIT_STATE/remote.$name.pushurl")"
                    exit 0
                  fi
                  exit 1
                  ;;
              esac
              exit 0
            fi
            if [ "${1:-}" = "init" ]; then
              mkdir -p .git
              exit 0
            fi
            if [ "${1:-}" = "remote" ] && [ "${2:-}" = "add" ]; then
              printf '%s' "${4:-}" > "$BUKIT_FAKE_GIT_STATE/remote.${3:-}.url"
              exit 0
            fi
            if [ "${1:-}" = "fetch" ]; then
              exit 0
            fi
            if [ "${1:-}" = "checkout" ]; then
              exit 0
            fi
            if [ "${1:-}" = "remote" ] && [ "${2:-}" = "get-url" ] && [ "${3:-}" = "origin" ]; then
              if [ -n "${BUKIT_FAKE_GIT_REMOTE_URL:-}" ]; then
                printf '%s\n' "$BUKIT_FAKE_GIT_REMOTE_URL"
              fi
              exit 0
            fi
            if [ "${1:-}" = "ls-remote" ]; then
              if [ "${BUKIT_FAKE_GIT_LS_REMOTE_MODE:-}" = "forbidden" ]; then
                printf 'remote: 403 Forbidden %s\n' "${GITHUB_TOKEN:-missing}" >&2
                exit 1
              fi
              if [ -n "${BUKIT_FAKE_GIT_REMOTE_HEADS:-}" ]; then
                printf '%s\n' "$BUKIT_FAKE_GIT_REMOTE_HEADS"
              fi
              exit 0
            fi
            if [ "${1:-}" = "clone" ]; then
              mkdir -p .git
              printf 'stale\n' > old.txt
              exit 0
            fi
            if [ "${1:-}" = "push" ]; then
              mode="${BUKIT_FAKE_GIT_PUSH_MODE:-success}"
              marker="$BUKIT_FAKE_GIT_STATE/nonff.marker"
              force=0
              for arg in "$@"; do
                if [ "$arg" = "--force" ]; then force=1; fi
              done
              if [ "$mode" = "nonff-once" ] && [ ! -f "$marker" ] && [ "$force" -eq 0 ]; then
                printf '1\n' > "$marker"
                printf '! [rejected] gh-pages -> gh-pages (non-fast-forward)\n' >&2
                exit 1
              fi
              if [ "$mode" = "sleep" ]; then
                printf 'started\n' > "$BUKIT_FAKE_GIT_STATE/push-sleep-started.marker"
                sleep 30
                printf 'completed\n' > "$BUKIT_FAKE_GIT_STATE/push-sleep-completed.marker"
              fi
              if [ "$mode" = "sleep-with-inherited-pipe" ]; then
                ( /bin/sleep 30 ) &
                wait
              fi
              if [ "$mode" = "forbidden" ]; then
                printf 'remote: 403 Forbidden %s\n' "${GITHUB_TOKEN:-missing}" >&2
                exit 1
              fi
              if [ "$mode" = "askpass-leak" ]; then
                printf 'fatal: cannot run %s for token %s\n' "${GIT_ASKPASS:-missing}" "${GITHUB_TOKEN:-missing}" >&2
                exit 1
              fi
              if [ "$mode" = "flood-over-limit" ]; then
                head -c 5242880 /dev/zero
                printf 'completed\n' > "$BUKIT_FAKE_GIT_STATE/push-flood-overflow-completed.marker"
                exit 0
              fi
              if [ "$mode" = "child-inherits-pipe" ]; then
                ( /bin/sleep 3 ) &
                exit 0
              fi
              if [ "$mode" = "flood-success" ] || [ "$mode" = "flood-failure" ]; then
                flood_index=0
                while [ "$flood_index" -lt 4096 ]; do
                  printf 'stdout-flood-%s-abcdefghijklmnopqrstuvwxyz0123456789\n' "$flood_index"
                  printf 'stderr-flood-%s-abcdefghijklmnopqrstuvwxyz0123456789\n' "$flood_index" >&2
                  flood_index=$((flood_index + 1))
                done
                if [ "$mode" = "flood-failure" ]; then
                  printf 'FLOOD_FAILURE token=%s\n' "${GITHUB_TOKEN:-missing}" >&2
                  exit 1
                fi
                exit 0
              fi
              [ -f ".nojekyll" ] && printf 'SNAPSHOT nojekyll\n' >> "$BUKIT_FAKE_GIT_LOG"
              [ -f "CNAME" ] && printf 'SNAPSHOT cname=%s\n' "$(cat CNAME)" >> "$BUKIT_FAKE_GIT_LOG"
              [ -f "index.html" ] && printf 'SNAPSHOT index\n' >> "$BUKIT_FAKE_GIT_LOG"
              [ -f "old.txt" ] && printf 'SNAPSHOT old\n' >> "$BUKIT_FAKE_GIT_LOG"
              exit 0
            fi
            exit 0
            """);
            File.SetUnixFileMode(unixPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Infos { get; } = [];
        public List<string> Warnings { get; } = [];
        public List<string> Errors { get; } = [];

        public void Debug(string message)
        {
        }

        public void Info(string message) => Infos.Add(message);

        public void Warn(string message) => Warnings.Add(message);

        public void Error(string message) => Errors.Add(message);
    }

    private sealed class TwoPhaseReadStream(string first, string second) : Stream
    {
        private readonly byte[] _first = Encoding.UTF8.GetBytes(first);
        private readonly byte[] _second = Encoding.UTF8.GetBytes(second);
        private readonly TaskCompletionSource _secondReadStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseSecondRead =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _readCount;

        internal Task SecondReadStarted => _secondReadStarted.Task;
        internal void ReleaseSecondRead() => _releaseSecondRead.TrySetResult();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var readCount = Interlocked.Increment(ref _readCount);
            if (readCount == 1)
            {
                _first.AsSpan().CopyTo(buffer.Span);
                return _first.Length;
            }

            if (readCount == 2)
            {
                _secondReadStarted.TrySetResult();
                await _releaseSecondRead.Task.WaitAsync(cancellationToken);
                _second.AsSpan().CopyTo(buffer.Span);
                return _second.Length;
            }

            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
