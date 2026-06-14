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
    public void SanitizeError_RedactsToken()
    {
        var result = InvokePrivateStatic<string>(
            nameof(GitHubPagesDeployProvider),
            "SanitizeError",
            ["push failed for token-123", "token-123"]);

        Assert.Equal("push failed for ***", result);
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
    public void CopyDirectory_SkipsNestedGitDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-gh-pages-copy-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var dest = Path.Combine(root, "dest");

        try
        {
            Directory.CreateDirectory(Path.Combine(source, ".git"));
            Directory.CreateDirectory(Path.Combine(source, "assets"));
            File.WriteAllText(Path.Combine(source, "index.html"), "<h1>Hello</h1>");
            File.WriteAllText(Path.Combine(source, "assets", "app.css"), "body{}");
            File.WriteAllText(Path.Combine(source, ".git", "config"), "[core]");

            InvokePrivateStatic<object?>(
                nameof(GitHubPagesDeployProvider),
                "CopyDirectory",
                [source, dest]);

            Assert.True(File.Exists(Path.Combine(dest, "index.html")));
            Assert.True(File.Exists(Path.Combine(dest, "assets", "app.css")));
            Assert.False(Directory.Exists(Path.Combine(dest, ".git")));
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
    public void CreateAskpassScript_AndCleanupAskpassScript_CreateAndDeleteScript()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit-gh-pages-askpass-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempDir);
            var scriptPath = InvokePrivateStatic<string>(
                nameof(GitHubPagesDeployProvider),
                "CreateAskpassScript",
                [tempDir, "secret-token"]);

            Assert.True(File.Exists(scriptPath));
            var contents = File.ReadAllText(scriptPath);
            Assert.Contains("secret-token", contents, StringComparison.Ordinal);

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

    private static T InvokePrivateStatic<T>(string typeName, string methodName, object?[] args)
    {
        var type = typeof(GitHubPagesDeployProvider);
        Assert.Equal(typeName, type.Name);

        var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method!.Invoke(null, args);
        return result is null ? default! : (T)result;
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

        public DeployContext CreateContext(bool keepHistory = false, bool force = false, string? branch = null, string? message = null, string? cname = null)
            => new()
            {
                OutputDir = OutputDir,
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
        }

        public void SetGithubToken(string? token) => SetEnv("GITHUB_TOKEN", token);

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

        public string PrependPath(string? existingPath)
            => string.IsNullOrWhiteSpace(existingPath)
                ? BinDir
                : BinDir + Path.PathSeparator + existingPath;

        public string ReadLog()
            => File.Exists(LogPath) ? File.ReadAllText(LogPath) : string.Empty;

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("BUKIT_FAKE_GIT_REMOTE_URL", null);
            Environment.SetEnvironmentVariable("BUKIT_FAKE_GIT_REMOTE_HEADS", null);
            Environment.SetEnvironmentVariable("BUKIT_FAKE_GIT_PUSH_MODE", null);
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
                if "%BUKIT_FAKE_GIT_PUSH_MODE%"=="forbidden" goto push_forbidden
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
                """);
                return;
            }

            var unixPath = Path.Combine(BinDir, "git");
            File.WriteAllText(unixPath, """
            #!/bin/sh
            set -eu
            echo "$PWD|$*" >> "$BUKIT_FAKE_GIT_LOG"
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
              if [ "$mode" = "forbidden" ]; then
                printf 'remote: 403 Forbidden %s\n' "${GITHUB_TOKEN:-missing}" >&2
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
