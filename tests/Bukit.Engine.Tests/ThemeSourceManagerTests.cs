using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ThemeSourceManagerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bukit-theme-source-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Resolve_ExistingRemoteTheme_DoesNotPullDuringBuildByDefault()
    {
        Directory.CreateDirectory(_root);
        var repoPath = Path.Combine(_root, ThemeSourceManager.SafeNameForTests("https://example.com/theme.git"));
        Directory.CreateDirectory(repoPath);
        var calls = new List<string>();
        var runner = new FakeGitRunner(args =>
        {
            calls.Add(args);
            return new GitResult(true, string.Empty, string.Empty, 0, false);
        });

        var resolved = ThemeSourceManager.Resolve("https://example.com/theme.git", _root, gitRunner: runner);

        Assert.NotNull(resolved);
        Assert.DoesNotContain("pull", calls);
    }

    [Fact]
    public void Resolve_WhenVersionTagDoesNotExist_ThrowsConfigException()
    {
        Directory.CreateDirectory(_root);
        var runner = new FakeGitRunner(args =>
        {
            if (args.StartsWith("clone ", StringComparison.Ordinal))
            {
                var repoPath = Path.Combine(_root, ThemeSourceManager.SafeNameForTests("https://example.com/theme.git"));
                Directory.CreateDirectory(repoPath);
                return new GitResult(true, string.Empty, string.Empty, 0, false);
            }

            if (args == "fetch --tags")
            {
                return new GitResult(true, string.Empty, string.Empty, 0, false);
            }

            if (args == "checkout v9.9.9")
            {
                return new GitResult(false, string.Empty, "pathspec 'v9.9.9' did not match", 1, false);
            }

            return new GitResult(false, string.Empty, "unexpected", 1, false);
        });

        var ex = Assert.Throws<ConfigException>(() =>
            ThemeSourceManager.Resolve("https://example.com/theme.git@v9.9.9", _root, gitRunner: runner));

        Assert.Contains("https://example.com/theme.git", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("v9.9.9", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class FakeGitRunner : IGitRunner
    {
        private readonly Func<string, GitResult> _handler;

        public FakeGitRunner(Func<string, GitResult> handler)
        {
            _handler = handler;
        }

        public Task<GitResult> RunAsync(string args, string workingDirectory, TimeSpan timeout, CancellationToken cancellationToken)
            => Task.FromResult(_handler(args));
    }
}
