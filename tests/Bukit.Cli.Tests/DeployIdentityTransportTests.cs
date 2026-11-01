using System.Diagnostics;
using System.Text;
using Bukit.Cli.Commands;
using Bukit.Cli.Deploy;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class DeployIdentityTransportTests
{
    [Fact]
    public async Task CrossProjectConfig_UsesConfiguredProjectIdentity()
    {
        using var scope = new RealDeployScope();
        var a = await scope.CreateProjectAsync("project-a", "https://github.com/owner/repo-a.git");
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        var refsBefore = scope.SnapshotRefs(a);
        var indexBefore = File.ReadAllBytes(Path.Combine(a, ".git", "index"));
        var sourceBefore = File.ReadAllBytes(Path.Combine(a, "README.md"));
        using var cwd = new CurrentDirectoryScope(a);

        var result = await scope.DeployAsync(b);

        Assert.True(result.Success, result.Error);
        Assert.Equal("https://owner.github.io/repo-b", result.DeployedUrl);
        Assert.NotEmpty(scope.Accesses);
        Assert.All(scope.Accesses, line => Assert.Contains(scope.BareRemote, line, StringComparison.Ordinal));
        Assert.DoesNotContain(scope.Accesses, line => line.Contains("repo-a.git", StringComparison.Ordinal));
        Assert.DoesNotContain(scope.Accesses, line => line.Contains(scope.BareRemoteA, StringComparison.Ordinal));
        Assert.Equal("new page", (await scope.GitAsync(scope.Root, "--git-dir", scope.BareRemote, "show", "refs/heads/gh-pages:index.html")).Trim());
        Assert.Empty((await scope.GitAsync(scope.Root, "--git-dir", scope.BareRemoteA, "for-each-ref", "--format=%(refname)")).Trim());
        Assert.Equal(refsBefore, scope.SnapshotRefs(a));
        Assert.Equal(indexBefore, File.ReadAllBytes(Path.Combine(a, ".git", "index")));
        Assert.Equal(sourceBefore, File.ReadAllBytes(Path.Combine(a, "README.md")));
    }

    [Fact]
    public async Task CrossProjectConfig_WithSpacesAndUnicodeInPaths_UsesConfiguredProject()
    {
        using var scope = new RealDeployScope(specialPath: true);
        var a = await scope.CreateProjectAsync("project-a", "https://github.com/owner/repo-a.git");
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        using var cwd = new CurrentDirectoryScope(a);

        var result = await scope.DeployAsync(b);

        Assert.True(result.Success, result.Error);
        Assert.NotEmpty(scope.Accesses);
        Assert.All(scope.Accesses, line => Assert.Contains(scope.BareRemote, line, StringComparison.Ordinal));
        Assert.DoesNotContain(scope.Accesses, line => line.Contains(scope.BareRemoteA, StringComparison.Ordinal));
    }

    [Fact]
    public async Task CwdOutsideRepository_UsesConfiguredProjectIdentity()
    {
        using var scope = new RealDeployScope();
        var outside = Path.Combine(scope.Root, "outside");
        Directory.CreateDirectory(outside);
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        using var cwd = new CurrentDirectoryScope(outside);

        var result = await scope.DeployAsync(b);

        Assert.True(result.Success, result.Error);
        Assert.Equal("https://owner.github.io/repo-b", result.DeployedUrl);
    }

    [Fact]
    public async Task MissingProjectOrigin_FailsWithoutUsingOtherRepository()
    {
        using var scope = new RealDeployScope();
        var a = await scope.CreateProjectAsync("project-a", "https://github.com/owner/repo-a.git");
        var independent = Path.Combine(scope.Root, "independent");
        Directory.CreateDirectory(independent);
        using var cwd = new CurrentDirectoryScope(a);

        var result = await scope.DeployAsync(independent);

        Assert.False(result.Success);
        Assert.Contains("Unable to determine GitHub repository", result.Error, StringComparison.Ordinal);
        Assert.Empty(scope.Accesses);
    }

    [Fact]
    public async Task UnmappedOrigin_IsRejectedByTransportDoubleWithoutAccess()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/unmapped.git");

        var result = await scope.DeployAsync(b);

        Assert.False(result.Success);
        Assert.Contains(OperatingSystem.IsWindows()
            ? "no allowed local bare repository"
            : "unexpected transport url", result.Error, StringComparison.Ordinal);
        Assert.Empty(scope.Accesses);
        Assert.Empty((await scope.GitAsync(scope.Root, "--git-dir", scope.BareRemote, "for-each-ref", "--format=%(refname)")).Trim());
    }

    [Fact]
    public async Task ConfigurationInRepositorySubdirectory_ResolvesOwningRepository()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        var subdirectory = Path.Combine(b, "site", "nested");
        Directory.CreateDirectory(subdirectory);

        var result = await scope.DeployAsync(subdirectory);

        Assert.True(result.Success, result.Error);
        Assert.Equal("https://owner.github.io/repo-b", result.DeployedUrl);
    }

    [Fact]
    public async Task OutputOverride_DoesNotChangeRepositoryIdentity()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        var separateOutput = Path.Combine(scope.Root, "separate-output");
        scope.WriteOutput(separateOutput);

        var result = await scope.DeployAsync(b, outputDir: separateOutput);

        Assert.True(result.Success, result.Error);
        Assert.Equal("https://owner.github.io/repo-b", result.DeployedUrl);
    }

    [Fact]
    public async Task InheritedGitDirFromOtherRepository_IsIgnored()
    {
        using var scope = new RealDeployScope();
        var a = await scope.CreateProjectAsync("project-a", "https://github.com/owner/repo-a.git");
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        scope.SetEnv("GIT_DIR", Path.Combine(a, ".git"));

        var result = await scope.DeployAsync(b);

        Assert.True(result.Success, result.Error);
        Assert.Equal("https://owner.github.io/repo-b", result.DeployedUrl);
    }

    [Fact]
    public async Task InheritedRepositoryOverrides_DoNotRedirectIdentityOrStaging()
    {
        using var scope = new RealDeployScope();
        var a = await scope.CreateProjectAsync("project-a", "https://github.com/owner/repo-a.git");
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        var indexSentinel = Path.Combine(a, ".git", "index-sentinel");
        File.WriteAllText(indexSentinel, "keep");
        var refsBefore = scope.SnapshotRefs(a);
        var headBefore = File.ReadAllText(Path.Combine(a, ".git", "HEAD"));
        scope.SetEnv("GIT_WORK_TREE", a);
        scope.SetEnv("GIT_COMMON_DIR", Path.Combine(a, ".git"));
        scope.SetEnv("GIT_INDEX_FILE", indexSentinel);
        scope.SetEnv("GIT_OBJECT_DIRECTORY", Path.Combine(a, ".git", "objects"));
        scope.SetEnv("GIT_CONFIG_COUNT", "1");
        scope.SetEnv("GIT_CONFIG_KEY_0", "remote.origin.url");
        scope.SetEnv("GIT_CONFIG_VALUE_0", "https://github.com/owner/repo-a.git");

        var result = await scope.DeployAsync(b);

        Assert.True(result.Success, result.Error);
        Assert.Equal("https://owner.github.io/repo-b", result.DeployedUrl);
        Assert.Equal("keep", File.ReadAllText(indexSentinel));
        Assert.Equal(refsBefore, scope.SnapshotRefs(a));
        Assert.Equal(headBefore, File.ReadAllText(Path.Combine(a, ".git", "HEAD")));
    }

    [Fact]
    public async Task GlobalSameNameOrigin_DoesNotOverrideProjectOrigin()
    {
        using var scope = new RealDeployScope();
        var a = await scope.CreateProjectAsync("project-a", "https://github.com/owner/repo-a.git");
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        scope.WriteGlobalConfig($"""
            [remote "origin"]
                url = https://github.com/owner/repo-a.git
                pushurl = https://github.com/owner/repo-a.git
            """);

        var result = await scope.DeployAsync(b);

        Assert.True(result.Success, result.Error);
        Assert.Equal("https://owner.github.io/repo-b", result.DeployedUrl);
        Assert.NotEmpty(scope.Accesses);
    }

    [Fact]
    public async Task WorktreeOriginWinsAndMissingWorktreeKeyFallsBackToLocal()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        await scope.GitAsync(b, "config", "extensions.worktreeConfig", "true");

        var fallback = await scope.DeployAsync(b);
        Assert.True(fallback.Success, fallback.Error);
        Assert.Equal("https://owner.github.io/repo-b", fallback.DeployedUrl);

        await scope.GitAsync(b, "config", "--worktree", "remote.origin.url", "https://github.com/owner/repo-worktree.git");
        var overrideResult = await scope.DeployAsync(b);
        Assert.True(overrideResult.Success, overrideResult.Error);
        Assert.Equal("https://owner.github.io/repo-worktree", overrideResult.DeployedUrl);
    }

    [Fact]
    public async Task EmptyFirstOriginValue_FailsInsteadOfUsingLaterValue()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", null);
        await scope.GitAsync(b, "config", "--local", "--add", "remote.origin.url", "");
        await scope.GitAsync(b, "config", "--local", "--add", "remote.origin.url", "https://github.com/owner/later.git");

        var result = await scope.DeployAsync(b);

        Assert.False(result.Success);
        Assert.Empty(scope.Accesses);
    }

    [Fact]
    public async Task LocalIncludeOrigin_IsUsedByScopedIdentity()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", null);
        var include = Path.Combine(b, ".git", "included-origin.inc");
        File.WriteAllText(include, """
            [remote "origin"]
                url = https://github.com/owner/repo-included.git
            """);
        await scope.GitAsync(b, "config", "--local", "include.path", include);

        var result = await scope.DeployAsync(b);

        Assert.True(result.Success, result.Error);
        Assert.Equal("https://owner.github.io/repo-included", result.DeployedUrl);
    }

    [Fact]
    public async Task InvalidFirstOriginValue_FailsInsteadOfUsingLaterValue()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", null);
        await scope.GitAsync(b, "config", "--local", "--add", "remote.origin.url", "not a remote url");
        await scope.GitAsync(b, "config", "--local", "--add", "remote.origin.url", "https://github.com/owner/later.git");

        var result = await scope.DeployAsync(b);

        Assert.False(result.Success);
        Assert.Empty(scope.Accesses);
    }

    [Fact]
    public async Task SystemMatchingRewrite_BlocksTransport()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        scope.WriteSystemConfig("""
            [url "https://evil.example/"]
                insteadOf = https://github.com/
            """);

        var result = await scope.DeployAsync(b);

        Assert.False(result.Success);
        Assert.Contains("URL rewrite", result.Error, StringComparison.Ordinal);
        Assert.Empty(scope.Accesses);
    }

    [Fact]
    public async Task OriginConfigReadError_IsNotTreatedAsMissingKey()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        await File.AppendAllTextAsync(Path.Combine(b, ".git", "config"), "\n[broken\nvalue\n");

        var result = await scope.DeployAsync(b);

        Assert.False(result.Success);
        Assert.DoesNotContain("Unable to determine GitHub repository", result.Error, StringComparison.Ordinal);
        Assert.Empty(scope.Accesses);
    }

    [Fact]
    public async Task MatchingUrlRewrite_BlocksTransportBeforeAnyAccess()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        scope.WriteGlobalConfig("""
            [url "https://evil.example/"]
                insteadOf = https://github.com/
            """);

        var result = await scope.DeployAsync(b);

        Assert.False(result.Success);
        Assert.Contains("URL rewrite", result.Error, StringComparison.Ordinal);
        Assert.Empty(scope.Accesses);
    }

    [Fact]
    public async Task EmptyRewritePrefix_BlocksTransport()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        scope.WriteGlobalConfig("""
            [url "https://evil.example/"]
                insteadOf =
            """);

        var result = await scope.DeployAsync(b);

        Assert.False(result.Success);
        Assert.Empty(scope.Accesses);
    }

    [Fact]
    public async Task GlobalMatchingRewrite_BlocksTransport()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        scope.WriteGlobalConfig("""
            [url "https://evil.example/"]
                insteadOf = https://github.com/
            """);

        var result = await scope.DeployAsync(b);

        Assert.False(result.Success);
        Assert.Empty(scope.Accesses);
    }

    [Fact]
    public async Task UnrelatedUrlRewrite_StillDeploys()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        scope.WriteGlobalConfig("""
            [url "https://github.com/"]
                insteadOf = gh:
            """);

        var result = await scope.DeployAsync(b);

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public async Task HasConfigConditionalInclude_ActivatedByPrivateRemote_BlocksBeforeTransport()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        var include = Path.Combine(scope.Root, "conditional-rewrite.inc");
        File.WriteAllText(include, """
            [url "https://evil.example/"]
                insteadOf = https://github.com/
            """);
        scope.WriteGlobalConfig($"""
            [includeIf "hasconfig:remote.*.url:https://github.com/owner/repo-b.git"]
                path = {include.Replace('\\', '/')}
            """);

        var result = await scope.DeployAsync(b);

        Assert.False(result.Success);
        Assert.Contains("URL rewrite", result.Error, StringComparison.Ordinal);
        Assert.Empty(scope.Accesses);
    }

    [Fact]
    public async Task GitdirConditionalInclude_ActivatedByRepositoryInit_BlocksBeforeTransport()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        var include = Path.Combine(scope.Root, "gitdir-rewrite.inc");
        File.WriteAllText(include, """
            [url "https://evil.example/"]
                insteadOf = https://github.com/
            """);
        scope.WriteGlobalConfig($"""
            [includeIf "gitdir:**/bukit-deploy-*/checkout/.git"]
                path = {include.Replace('\\', '/')}
            """);

        var result = await scope.DeployAsync(b);

        Assert.False(result.Success);
        Assert.Contains("URL rewrite", result.Error, StringComparison.Ordinal);
        Assert.Empty(scope.Accesses);
    }

    [Fact]
    public async Task UnrelatedGitdirCondition_DoesNotBlock()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        scope.WriteGlobalConfig("""
            [includeIf "gitdir:**/never-matching-worktree/**"]
                path = /nonexistent/bukit-unrelated.inc
            """);

        var result = await scope.DeployAsync(b);

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public async Task ExistingBranch_PreservesHistoryAndPushSemantics()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        await scope.SeedRemoteBranchAsync("gh-pages", "old.html", "old page");

        var result = await scope.DeployAsync(b, keepHistory: true, branch: "gh-pages");

        Assert.True(result.Success, result.Error);
        Assert.Contains(scope.Accesses, line => line.Contains("fetch", StringComparison.Ordinal) && !line.Contains("--depth", StringComparison.Ordinal));
        Assert.Contains(scope.Accesses, line => line.Contains("push", StringComparison.Ordinal) && line.Contains("gh-pages", StringComparison.Ordinal));
        Assert.Equal("new page", (await scope.GitAsync(scope.Root, "--git-dir", scope.BareRemote, "show", "gh-pages:index.html")).Trim());
        var files = await scope.GitAsync(scope.Root, "--git-dir", scope.BareRemote, "ls-tree", "--name-only", "gh-pages");
        Assert.DoesNotContain("old.html", files);
    }

    [Fact]
    public async Task NewBranchAndShallowFetch_UsePrivateRemote()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        await scope.SeedRemoteBranchAsync("gh-pages", "old.html", "old page");

        var shallow = await scope.DeployAsync(b, keepHistory: false, branch: "gh-pages");
        Assert.True(shallow.Success, shallow.Error);
        Assert.Contains(scope.Accesses, line => line.Contains("--depth 1", StringComparison.Ordinal));

        var fresh = await scope.DeployAsync(b, keepHistory: false, branch: "fresh-pages");
        Assert.True(fresh.Success, fresh.Error);
        var files = await scope.GitAsync(scope.Root, "--git-dir", scope.BareRemote, "ls-tree", "--name-only", "fresh-pages");
        Assert.Contains("index.html", files);
    }

    [Fact]
    public async Task RepositoryDefaultBranchName_CanBeFetchedAndPushed()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        scope.WriteGlobalConfig("""
            [init]
                defaultBranch = main
            """);
        await scope.SeedRemoteBranchAsync("main", "old.html", "old page");

        var result = await scope.DeployAsync(b, keepHistory: true, branch: "main");

        Assert.True(result.Success, result.Error);
        Assert.Equal("new page", (await scope.GitAsync(scope.Root, "--git-dir", scope.BareRemote, "show", "main:index.html")).Trim());
    }

    [Fact]
    public async Task SameNameBranchAndTag_FetchesTheQueriedBranchHead()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        await scope.SeedSameNameBranchAndTagAsync("gh-pages");

        var history = await scope.DeployAsync(b, keepHistory: true, branch: "gh-pages");

        Assert.True(history.Success, history.Error);
        var tree = await scope.GitAsync(scope.Root, "--git-dir", scope.BareRemote, "ls-tree", "--name-only", "refs/heads/gh-pages");
        Assert.Contains("index.html", tree);
        Assert.DoesNotContain("tag-only.txt", tree);
    }

    [Fact]
    public async Task SameNameBranchAndTag_ShallowFetchUsesTheQueriedBranchHead()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        await scope.SeedSameNameBranchAndTagAsync("gh-pages");

        var shallow = await scope.DeployAsync(b, keepHistory: false, branch: "gh-pages");

        Assert.True(shallow.Success, shallow.Error);
        var tree = await scope.GitAsync(scope.Root, "--git-dir", scope.BareRemote, "ls-tree", "--name-only", "refs/heads/gh-pages");
        Assert.Contains("index.html", tree);
        Assert.DoesNotContain("tag-only.txt", tree);
    }

    [Fact]
    public async Task RewriteDiagnostic_DoesNotEchoBaseUrlCredentials()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        scope.WriteGlobalConfig("""
            [url "https://another-user:other-secret@evil.example/"]
                insteadOf = https://github.com/
            """);

        var result = await scope.DeployAsync(b);

        Assert.False(result.Success);
        Assert.Contains("URL rewrite", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("other-secret", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("another-user", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("evil.example", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForcePushAndRepeatedDeployments_Succeed()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");

        var first = await scope.DeployAsync(b, force: true, branch: "gh-pages");
        var second = await scope.DeployAsync(b, force: true, branch: "gh-pages");

        Assert.True(first.Success, first.Error);
        Assert.True(second.Success, second.Error);
        Assert.Equal(2, scope.Accesses.Count(line => line.Contains("--force", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CliDeployWithConfig_UsesConfiguredProjectIdentityAndTransport()
    {
        using var scope = new RealDeployScope();
        var a = await scope.CreateProjectAsync("project-a", "https://github.com/owner/repo-a.git");
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        var refsBefore = scope.SnapshotRefs(a);
        var indexBefore = File.ReadAllBytes(Path.Combine(a, ".git", "index"));
        var sourceBefore = File.ReadAllBytes(Path.Combine(a, "README.md"));
        scope.WriteSiteConfig(b);
        scope.WriteOutput(Path.Combine(b, "dist"));
        using var cwd = new CurrentDirectoryScope(a);

        var exitCode = await DeployCommand.RunAsync(CliTestHelper.CreateCommand(
            "deploy", ["--config", Path.Combine(b, "site.yaml"), "--skip-build"]));

        Assert.Equal(0, exitCode);
        Assert.NotEmpty(scope.Accesses);
        Assert.All(scope.Accesses, line =>
        {
            Assert.Contains("https://github.com/owner/repo-b.git", line, StringComparison.Ordinal);
            Assert.Contains(scope.BareRemote, line, StringComparison.Ordinal);
        });
        Assert.DoesNotContain(scope.Accesses, line => line.Contains("repo-a.git", StringComparison.Ordinal));
        Assert.Equal("new page", (await scope.GitAsync(scope.Root, "--git-dir", scope.BareRemote, "show", "refs/heads/gh-pages:index.html")).Trim());
        Assert.Empty((await scope.GitAsync(scope.Root, "--git-dir", scope.BareRemoteA, "for-each-ref", "--format=%(refname)")).Trim());
        Assert.Equal(refsBefore, scope.SnapshotRefs(a));
        Assert.Equal(indexBefore, File.ReadAllBytes(Path.Combine(a, ".git", "index")));
        Assert.Equal(sourceBefore, File.ReadAllBytes(Path.Combine(a, "README.md")));
    }

    [Fact]
    public async Task LinkedWorktree_UsesOwningProjectOriginAndKeepsParentEnvironment()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        var worktree = Path.Combine(scope.Root, "linked-worktree");
        await scope.GitAsync(b, "worktree", "add", "-b", "linked", worktree);
        var before = scope.SnapshotEnvironment();

        var result = await scope.DeployAsync(worktree);

        Assert.True(result.Success, result.Error);
        Assert.Equal("https://owner.github.io/repo-b", result.DeployedUrl);
        Assert.Equal(before, scope.SnapshotEnvironment());
    }

    [Fact]
    public async Task WorkingDirectoryAndAskpassEnvironmentRemainUntouched()
    {
        using var scope = new RealDeployScope();
        var b = await scope.CreateProjectAsync("project-b", "https://github.com/owner/repo-b.git");
        var cwd = Environment.CurrentDirectory;
        var before = scope.SnapshotEnvironment();

        var result = await scope.DeployAsync(b);

        Assert.True(result.Success, result.Error);
        Assert.Equal(cwd, Environment.CurrentDirectory);
        Assert.Equal(before, scope.SnapshotEnvironment());
    }

    [Theory]
    [InlineData("direct")]
    [InlineData("pushurl")]
    [InlineData("insteadOf")]
    [InlineData("pushInsteadOf")]
    public async Task TransportShim_PushUsesEffectiveTarget(string configuration)
    {
        using var scope = new RealDeployScope();
        var project = await CreateShimProjectAsync(scope);
        const string a = "https://github.com/owner/repo-a.git";
        const string b = "https://github.com/owner/repo-b.git";
        if (configuration == "pushurl")
            await scope.GitAsync(project, "config", "remote.bukit-deploy-shim.pushurl", a);
        else if (configuration != "direct")
            await scope.GitAsync(project, "config", $"url.{a}.{configuration}", b);

        var expectedUrl = configuration == "direct" ? b : a;
        var target = configuration == "direct" ? scope.BareRemote : scope.BareRemoteA;
        var other = configuration == "direct" ? scope.BareRemoteA : scope.BareRemote;
        Assert.Equal(expectedUrl, (await scope.GitAsync(project, "remote", "get-url", "--push", "--all", "bukit-deploy-shim")).Trim());

        var result = await scope.ShimAsync(project, "push", "bukit-deploy-shim", "HEAD:refs/heads/shim-probe");

        Assert.True(result.ExitCode == 0, result.Error);
        Assert.Equal("shim-project", (await scope.GitAsync(scope.Root, "--git-dir", target, "show", "refs/heads/shim-probe:README.md")).Trim());
        Assert.Empty((await scope.GitAsync(scope.Root, "--git-dir", other, "for-each-ref")).Trim());
        Assert.StartsWith($"ACCESS {expectedUrl}|{target}|push ", Assert.Single(scope.Accesses), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ls-remote", false)]
    [InlineData("ls-remote", true)]
    [InlineData("fetch", false)]
    [InlineData("fetch", true)]
    public async Task TransportShim_ReadUsesEffectiveFetchTarget(string command, bool rewrite)
    {
        using var scope = new RealDeployScope();
        var project = await CreateShimProjectAsync(scope);
        await scope.GitAsync(project, "push", scope.BareRemote, "HEAD:refs/heads/shim-probe");
        var bHead = (await scope.GitAsync(project, "rev-parse", "HEAD")).Trim();
        File.WriteAllText(Path.Combine(project, "README.md"), "remote A sentinel");
        await scope.GitAsync(project, "add", "README.md");
        await scope.GitAsync(project, "commit", "-m", "remote A");
        await scope.GitAsync(project, "push", scope.BareRemoteA, "HEAD:refs/heads/shim-probe");
        var aHead = (await scope.GitAsync(project, "rev-parse", "HEAD")).Trim();
        Assert.NotEqual(aHead, bHead);
        await scope.GitAsync(project, "config", "remote.bukit-deploy-shim.pushurl", "https://github.com/owner/repo-a.git");
        if (rewrite)
            await scope.GitAsync(project, "config", "url.https://github.com/owner/repo-a.git.insteadOf", "https://github.com/owner/repo-b.git");

        var result = await scope.ShimAsync(project, command, "bukit-deploy-shim", "refs/heads/shim-probe");

        Assert.True(result.ExitCode == 0, result.Error);
        var actualHead = command == "fetch"
            ? (await scope.GitAsync(project, "rev-parse", "FETCH_HEAD")).Trim()
            : result.Output.Split('\t')[0];
        Assert.Equal(rewrite ? aHead : bHead, actualHead);
        var expectedUrl = $"https://github.com/owner/repo-{(rewrite ? "a" : "b")}.git";
        var target = rewrite ? scope.BareRemoteA : scope.BareRemote;
        Assert.StartsWith($"ACCESS {expectedUrl}|{target}|{command} ", Assert.Single(scope.Accesses), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("multiple")]
    [InlineData("unmapped")]
    public async Task TransportShim_InvalidEffectivePushTargetsFailBeforeAccess(string configuration)
    {
        using var scope = new RealDeployScope();
        var project = await CreateShimProjectAsync(scope);
        if (configuration == "multiple")
        {
            await scope.GitAsync(project, "config", "--add", "remote.bukit-deploy-shim.pushurl", "https://github.com/owner/repo-a.git");
            await scope.GitAsync(project, "config", "--add", "remote.bukit-deploy-shim.pushurl", "https://github.com/owner/repo-b.git");
        }
        else
            await scope.GitAsync(project, "config", "remote.bukit-deploy-shim.pushurl", "https://github.com/owner/unmapped.git");

        var result = await scope.ShimAsync(project, "push", "bukit-deploy-shim", "HEAD:refs/heads/shim-probe");

        Assert.Equal(93, result.ExitCode);
        Assert.Contains(configuration == "multiple" ? "exactly one effective transport URL" : "no allowed local bare repository", result.Error, StringComparison.Ordinal);
        Assert.Empty(scope.Accesses);
        foreach (var remote in new[] { scope.BareRemoteA, scope.BareRemote })
            Assert.Empty((await scope.GitAsync(scope.Root, "--git-dir", remote, "for-each-ref")).Trim());
    }

    private static async Task<string> CreateShimProjectAsync(RealDeployScope scope)
    {
        var project = await scope.CreateProjectAsync("shim-project", null);
        await scope.GitAsync(project, "remote", "add", "bukit-deploy-shim", "https://github.com/owner/repo-b.git");
        var askpass = Path.Combine(scope.Root, "shim-askpass");
        File.WriteAllText(askpass, "local transport fixture");
        scope.SetEnv("GIT_ASKPASS", askpass);
        return project;
    }

    private sealed class RealDeployScope : IDisposable
    {
        private readonly Dictionary<string, string?> _original = new(StringComparer.Ordinal);
        private readonly string _gitPath;
        private readonly string _binDir;
        private readonly string _globalConfig;
        private readonly string _systemConfig;

        public RealDeployScope(bool specialPath = false)
        {
            var prefix = specialPath ? "bukit deploy & 测试 " : "bukit-deploy-identity-";
            Root = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            _gitPath = ResolveGit();
            _binDir = Path.Combine(Root, "bin");
            Directory.CreateDirectory(_binDir);
            BareRemote = Path.Combine(Root, "remote-b.git");
            BareRemoteA = Path.Combine(Root, "remote-a.git");
            AccessLog = Path.Combine(Root, "transport-access.log");
            _globalConfig = Path.Combine(Root, "global.gitconfig");
            File.WriteAllText(_globalConfig, string.Empty);
            _systemConfig = Path.Combine(Root, "system.gitconfig");
            File.WriteAllText(_systemConfig, string.Empty);
            try
            {
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
                SetEnv("GIT_CONFIG_GLOBAL", _globalConfig);
                SetEnv("GIT_CONFIG_SYSTEM", _systemConfig);
                SetEnv("GIT_ALLOW_PROTOCOL", "file");
                RunGit(Root, "--version").GetAwaiter().GetResult();
                RunGit(Root, "init", "--bare", BareRemote).GetAwaiter().GetResult();
                RunGit(Root, "init", "--bare", BareRemoteA).GetAwaiter().GetResult();
                SetEnv("GITHUB_TOKEN", "dummy-deploy-token");
                SetEnv("BUKIT_DEPLOY_GIT_TIMEOUT_SECONDS", "20");
                WriteWrapper();
                SetEnv("BUKIT_DEPLOY_ACCESS_LOG", AccessLog);
                SetEnv("BUKIT_DEPLOY_BARE_A", BareRemoteA);
                SetEnv("BUKIT_DEPLOY_BARE_B", BareRemote);
                SetEnv("BUKIT_DEPLOY_REAL_GIT", _gitPath);
                SetEnv("BUKIT_TEST_GIT_SCRIPT", null);
                SetEnv("BUKIT_DEPLOY_SIMPLE_LOG", null);
                SetEnv("BUKIT_DEPLOY_ASKPASS_LOG", null);
                SetEnv("BUKIT_DEPLOY_PUSH_BEHAVIOR", null);
                SetEnv("BUKIT_DEPLOY_PUSH_MARKER", null);
                SetEnv("PATH", _binDir + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public string Root { get; }
        public string BareRemote { get; }
        public string BareRemoteA { get; }
        public string AccessLog { get; }
        public IReadOnlyList<string> Accesses => File.Exists(AccessLog) ? File.ReadAllLines(AccessLog) : [];

        public void SetEnv(string name, string? value)
        {
            if (!_original.ContainsKey(name))
            {
                _original[name] = Environment.GetEnvironmentVariable(name);
            }

            Environment.SetEnvironmentVariable(name, value);
        }

        public Dictionary<string, string?> SnapshotEnvironment()
            => _original.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable, StringComparer.Ordinal);

        public async Task<string> CreateProjectAsync(string name, string? originUrl)
        {
            var path = Path.Combine(Root, name);
            Directory.CreateDirectory(path);
            await GitAsync(path, "init");
            await GitAsync(path, "config", "user.email", "deploy-tests@example.test");
            await GitAsync(path, "config", "user.name", "Deploy Tests");
            File.WriteAllText(Path.Combine(path, "README.md"), name);
            await GitAsync(path, "add", "README.md");
            await GitAsync(path, "commit", "-m", "seed");
            if (originUrl is not null)
            {
                await GitAsync(path, "remote", "add", "origin", originUrl);
            }

            return path;
        }

        public void WriteSiteConfig(string projectRoot, string output = "dist")
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, "content"));
            File.WriteAllText(Path.Combine(projectRoot, "site.yaml"), $$"""
                site:
                  name: deploy-test
                  title: Deploy Test
                  url: https://example.test
                  baseUrl: /
                build:
                  output: {{output}}
                content:
                  sources:
                    - type: markdown
                      name: pages
                      collection: page
                      markdown:
                        dir: content
                deploy:
                  provider: github-pages
                  branch: gh-pages
                """);
        }

        public void WriteOutput(string outputDir)
        {
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, "index.html"), "new page");
            var reportDir = Path.Combine(outputDir, ".bukit");
            Directory.CreateDirectory(reportDir);
            File.WriteAllText(Path.Combine(reportDir, "publish-audit-report.json"),
                "{\"schema\":\"https://bukit.dev/schemas/publish-audit-report.v1.json\",\"documents\":[]}");
            File.WriteAllText(Path.Combine(outputDir, ".bukit-build-state.json"), "{\"version\":1,\"status\":\"completed\"}");
        }

        public void WriteGlobalConfig(string content) => File.WriteAllText(_globalConfig, content);

        public void WriteSystemConfig(string content) => File.WriteAllText(_systemConfig, content);

        public async Task SeedSameNameBranchAndTagAsync(string branch)
        {
            var seed = Path.Combine(Root, "seed-branch-tag-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(seed);
            await GitAsync(seed, "init");
            await GitAsync(seed, "config", "user.email", "deploy-tests@example.test");
            await GitAsync(seed, "config", "user.name", "Deploy Tests");
            await GitAsync(seed, "checkout", "-B", branch);
            await File.WriteAllTextAsync(Path.Combine(seed, "branch-only.txt"), "branch base");
            await GitAsync(seed, "add", "-A");
            await GitAsync(seed, "commit", "-m", "branch base");
            await GitAsync(seed, "checkout", "-B", "tag-source");
            await File.WriteAllTextAsync(Path.Combine(seed, "tag-only.txt"), "tag commit");
            await GitAsync(seed, "add", "-A");
            await GitAsync(seed, "commit", "-m", "tag commit");
            await GitAsync(seed, "tag", branch);
            await GitAsync(seed, "push", BareRemote, $"refs/heads/{branch}:refs/heads/{branch}");
            await GitAsync(seed, "push", BareRemote, $"refs/tags/{branch}:refs/tags/{branch}");
        }

        public async Task SeedRemoteBranchAsync(string branch, string fileName, string content)
        {
            var seed = Path.Combine(Root, "seed-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(seed);
            await GitAsync(seed, "init");
            await GitAsync(seed, "config", "user.email", "deploy-tests@example.test");
            await GitAsync(seed, "config", "user.name", "Deploy Tests");
            await GitAsync(seed, "checkout", "-B", branch);
            await File.WriteAllTextAsync(Path.Combine(seed, fileName), content);
            await GitAsync(seed, "add", "-A");
            await GitAsync(seed, "commit", "-m", "seed branch");
            await GitAsync(seed, "push", BareRemote, branch);
        }

        public async Task<DeployResult> DeployAsync(
            string projectRoot,
            string? outputDir = null,
            bool keepHistory = false,
            bool force = false,
            string? branch = "gh-pages")
        {
            var output = outputDir ?? Path.Combine(Root, "output-" + Guid.NewGuid().ToString("N"));
            if (outputDir is null)
            {
                WriteOutput(output);
            }

            var context = new DeployContext
            {
                OutputDir = output,
                SiteUrl = "https://example.test",
                BaseUrl = "/",
                Branch = branch,
                Message = "deploy test",
                Cname = null,
                KeepHistory = keepHistory,
                Force = force,
                Logger = new TestLogger()
            };
            return await GitHubPagesDeployProvider.DeployAsync(context, projectRoot, CancellationToken.None);
        }

        public async Task<string> GitAsync(string workingDirectory, params string[] arguments)
        {
            using var process = StartGit(workingDirectory, arguments);
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            var output = await stdout;
            var error = await stderr;
            Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)} failed: {error}");
            return output;
        }

        public async Task<(int ExitCode, string Output, string Error)> ShimAsync(string workingDirectory, params string[] arguments)
        {
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            start.ArgumentList.Add(WindowsGitShim.AssemblyPath);
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
                await Task.WhenAll(output, error).WaitAsync(TimeSpan.FromSeconds(5));
                return (process.ExitCode, await output, await error);
            }
            finally
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
        }

        public string SnapshotRefs(string repository)
            => string.Join("\n", Directory.EnumerateFiles(Path.Combine(repository, ".git", "refs"), "*", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(path => Path.GetRelativePath(repository, path) + ":" + File.ReadAllText(path)));

        public void Dispose()
        {
            foreach (var entry in _original)
            {
                Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            }

            try
            {
                if (Directory.Exists(Root))
                {
                    if (OperatingSystem.IsWindows())
                    {
                        foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                        {
                            File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
                        }
                    }

                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private Task<string> RunGit(string workingDirectory, params string[] arguments) => GitAsync(workingDirectory, arguments);

        private Process StartGit(string workingDirectory, IReadOnlyList<string> arguments)
        {
            var start = new ProcessStartInfo(_gitPath)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            return Process.Start(start)!;
        }

        private void WriteWrapper()
        {
            if (OperatingSystem.IsWindows())
            {
                WindowsGitShim.Install(_binDir);
                return;
            }

            var wrapper = Path.Combine(_binDir, "git");
            var quote = (string value) => "'" + value.Replace("'", "'\\''") + "'";
            File.WriteAllText(wrapper, $$"""
                #!/bin/sh
                set -eu
                case "${1:-}" in
                  ls-remote|fetch|push)
                    remote_name=""
                    for arg in "$@"; do
                      case "$arg" in
                        bukit-deploy-*) remote_name="$arg" ;;
                      esac
                    done
                    [ -n "$remote_name" ] || { echo "missing private remote" >&2; exit 92; }
                    url=$("$BUKIT_DEPLOY_REAL_GIT" -C "$PWD" config --local --get "remote.$remote_name.url")
                    case "$url" in
                      https://github.com/owner/repo-a.git) target="$BUKIT_DEPLOY_BARE_A" ;;
                      https://github.com/owner/repo-b.git) target="$BUKIT_DEPLOY_BARE_B" ;;
                      https://github.com/owner/repo-included.git) target="$BUKIT_DEPLOY_BARE_B" ;;
                      https://github.com/owner/repo-worktree.git) target="$BUKIT_DEPLOY_BARE_B" ;;
                      *) echo "unexpected transport url: $url" >&2; exit 93 ;;
                    esac
                    printf 'ACCESS %s|%s|%s\n' "$url" "$target" "$*" >> "$BUKIT_DEPLOY_ACCESS_LOG"
                    mapped=""
                    for arg in "$@"; do
                      case "$arg" in
                        bukit-deploy-*) arg="$target" ;;
                      esac
                      mapped="$mapped '$arg'"
                    done
                    eval "exec {{quote(_gitPath)}} $mapped"
                    ;;
                  *)
                    exec {{quote(_gitPath)}} "$@"
                    ;;
                esac
                """);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(wrapper, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }

        private static string ResolveGit()
        {
            var names = OperatingSystem.IsWindows() ? new[] { "git.exe", "git.cmd" } : new[] { "git" };
            foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                         .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var name in names)
                {
                    var candidate = Path.Combine(directory.Trim(), name);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            throw new InvalidOperationException("git not found");
        }
    }

    private sealed class TestLogger : ILogger
    {
        public void Debug(string message)
        {
        }

        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
        }

        public void Error(string message)
        {
        }
    }
}
