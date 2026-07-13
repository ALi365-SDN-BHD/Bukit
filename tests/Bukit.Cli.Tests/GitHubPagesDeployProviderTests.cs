using System.Diagnostics;
using System.Reflection;
using Bukit.Cli.Deploy;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class GitHubPagesDeployProviderTests
{
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
        Assert.Contains("clone --single-branch --branch gh-pages --depth 1 https://github.com/ali/docs.git .", log, StringComparison.Ordinal);
        Assert.Contains("SNAPSHOT nojekyll", log, StringComparison.Ordinal);
        Assert.Contains("SNAPSHOT cname=docs.example.com", log, StringComparison.Ordinal);
        Assert.Contains("SNAPSHOT index", log, StringComparison.Ordinal);
        Assert.DoesNotContain("SNAPSHOT old", log, StringComparison.Ordinal);
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
        Assert.Contains("push --force origin gh-pages", scope.FakeGit.ReadLog(), StringComparison.Ordinal);
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
        Assert.Contains("push --force origin gh-pages", log, StringComparison.Ordinal);
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

        var started = DateTime.UtcNow;
        while (!File.Exists(scope.FakeGit.PushSleepStartedPath) && DateTime.UtcNow - started < TimeSpan.FromSeconds(2))
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
        Assert.Equal("Unable to determine GitHub repository. Ensure you are in a git repository with a remote 'origin' pointing to GitHub.", result.Error);
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
        Assert.Equal("Unable to determine GitHub repository. Ensure you are in a git repository with a remote 'origin' pointing to GitHub.", result.Error);
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
        Assert.Equal("Unable to determine GitHub repository. Ensure you are in a git repository with a remote 'origin' pointing to GitHub.", result.Error);
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
        Assert.Equal("Unable to determine GitHub repository. Ensure you are in a git repository with a remote 'origin' pointing to GitHub.", result.Error);
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
        Assert.Contains("push --force origin gh-pages", scope.FakeGit.ReadLog(), StringComparison.Ordinal);
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
        Assert.Contains("clone --single-branch --branch gh-pages --depth 1 https://github.com/ali/docs.git .", log, StringComparison.Ordinal);
        Assert.DoesNotContain("clone --force", log, StringComparison.Ordinal);
        Assert.Contains("push --force origin gh-pages", log, StringComparison.Ordinal);
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

        public GitHubPagesDeployTestScope(bool includeFakeGitInPath = true)
        {
            _root = Path.Combine(Path.GetTempPath(), "bukit-gh-pages-provider-" + Guid.NewGuid().ToString("N"));
            WorktreeDir = Path.Combine(_root, "worktree");
            OutputDir = Path.Combine(_root, "output");
            Directory.CreateDirectory(WorktreeDir);
            Directory.CreateDirectory(OutputDir);
            Logger = new RecordingLogger();
            FakeGit = new FakeGitHarness(_root);

            SetEnv("PATH", includeFakeGitInPath
                ? FakeGit.PrependPath(Environment.GetEnvironmentVariable("PATH"))
                : string.Empty);
            SetEnv("BUKIT_FAKE_GIT_LOG", FakeGit.LogPath);
            SetEnv("BUKIT_FAKE_GIT_STATE", FakeGit.StateDir);
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

        public void Dispose()
        {
            FakeGit.Dispose();

            foreach (var entry in _originalEnv)
            {
                Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            }

            if (Directory.Exists(_root))
            {
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

        public string PushSleepStartedPath => Path.Combine(StateDir, "push-sleep-started.marker");

        public string PushSleepCompletedPath => Path.Combine(StateDir, "push-sleep-completed.marker");

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
            Environment.SetEnvironmentVariable("BUKIT_DEPLOY_GIT_TIMEOUT_SECONDS", null);
        }

        private void WriteScript()
        {
            if (OperatingSystem.IsWindows())
            {
                var scriptPath = Path.Combine(BinDir, "git.cmd");
                File.WriteAllText(scriptPath, """
                @echo off
                setlocal EnableDelayedExpansion
                echo %CD%^|%*>>"%BUKIT_FAKE_GIT_LOG%"
                if not "%GIT_ASKPASS%"=="" echo ASKPASS %GIT_ASKPASS%>>"%BUKIT_FAKE_GIT_LOG%"
                if "%~1"=="remote" if "%~2"=="get-url" if "%~3"=="origin" goto remote_get_url
                if "%~1"=="ls-remote" goto ls_remote
                if "%~1"=="clone" goto clone_repo
                if "%~1"=="push" goto push_repo
                exit /b 0

                :remote_get_url
                if not "%BUKIT_FAKE_GIT_REMOTE_URL%"=="" echo %BUKIT_FAKE_GIT_REMOTE_URL%
                exit /b 0

                :ls_remote
                if not "%BUKIT_FAKE_GIT_REMOTE_HEADS%"=="" echo %BUKIT_FAKE_GIT_REMOTE_HEADS%
                exit /b 0

                :clone_repo
                mkdir .git >nul 2>nul
                >old.txt echo stale
                exit /b 0

                :push_repo
                if "%BUKIT_FAKE_GIT_PUSH_MODE%"=="nonff-once" if not exist "%BUKIT_FAKE_GIT_STATE%\nonff.marker" if not "%~2"=="--force" goto push_nonff
                if "%BUKIT_FAKE_GIT_PUSH_MODE%"=="sleep" goto push_sleep
                if "%BUKIT_FAKE_GIT_PUSH_MODE%"=="forbidden" goto push_forbidden
                if "%BUKIT_FAKE_GIT_PUSH_MODE%"=="askpass-leak" goto push_askpass_leak
                if exist ".nojekyll" echo SNAPSHOT nojekyll>>"%BUKIT_FAKE_GIT_LOG%"
                if exist "CNAME" (
                  set /p cname=<CNAME
                  echo SNAPSHOT cname=!cname!>>"%BUKIT_FAKE_GIT_LOG%"
                )
                if exist "index.html" echo SNAPSHOT index>>"%BUKIT_FAKE_GIT_LOG%"
                if exist "old.txt" echo SNAPSHOT old>>"%BUKIT_FAKE_GIT_LOG%"
                exit /b 0

                :push_nonff
                >"%BUKIT_FAKE_GIT_STATE%\nonff.marker" echo 1
                1>&2 echo ^! [rejected] gh-pages -^> gh-pages ^(non-fast-forward^)
                exit /b 1

                :push_forbidden
                1>&2 echo remote: 403 Forbidden %GITHUB_TOKEN%
                exit /b 1

                :push_askpass_leak
                1>&2 echo fatal: cannot run %GIT_ASKPASS% for token %GITHUB_TOKEN%
                exit /b 1

                :push_sleep
                echo started > "%BUKIT_FAKE_GIT_STATE%\push-sleep-started.marker"
                for /l %%I in (1,1,120) do ping -n 2 127.0.0.1 >nul
                echo completed > "%BUKIT_FAKE_GIT_STATE%\push-sleep-completed.marker"
                if exist ".nojekyll" echo SNAPSHOT nojekyll>>"%BUKIT_FAKE_GIT_LOG%"
                if exist "CNAME" (
                  set /p cname=<CNAME
                  echo SNAPSHOT cname=!cname!>>"%BUKIT_FAKE_GIT_LOG%"
                )
                if exist "index.html" echo SNAPSHOT index>>"%BUKIT_FAKE_GIT_LOG%"
                if exist "old.txt" echo SNAPSHOT old>>"%BUKIT_FAKE_GIT_LOG%"
                exit /b 0
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
            if [ "${1:-}" = "remote" ] && [ "${2:-}" = "get-url" ] && [ "${3:-}" = "origin" ]; then
              if [ -n "${BUKIT_FAKE_GIT_REMOTE_URL:-}" ]; then
                printf '%s\n' "$BUKIT_FAKE_GIT_REMOTE_URL"
              fi
              exit 0
            fi
            if [ "${1:-}" = "ls-remote" ]; then
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
              if [ "$mode" = "nonff-once" ] && [ ! -f "$marker" ] && [ "${2:-}" != "--force" ]; then
                printf '1\n' > "$marker"
                printf '! [rejected] gh-pages -> gh-pages (non-fast-forward)\n' >&2
                exit 1
              fi
              if [ "$mode" = "sleep" ]; then
                printf 'started\n' > "$BUKIT_FAKE_GIT_STATE/push-sleep-started.marker"
                sleep 30
                printf 'completed\n' > "$BUKIT_FAKE_GIT_STATE/push-sleep-completed.marker"
              fi
              if [ "$mode" = "forbidden" ]; then
                printf 'remote: 403 Forbidden %s\n' "${GITHUB_TOKEN:-missing}" >&2
                exit 1
              fi
              if [ "$mode" = "askpass-leak" ]; then
                printf 'fatal: cannot run %s for token %s\n' "${GIT_ASKPASS:-missing}" "${GITHUB_TOKEN:-missing}" >&2
                exit 1
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
}
