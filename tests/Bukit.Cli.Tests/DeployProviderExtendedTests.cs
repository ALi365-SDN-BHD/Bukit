using System.Reflection;
using Bukit.Cli.Deploy;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class DeployProviderExtendedTests : IDisposable
{
    private readonly string _tempDir;

    private static readonly MethodInfo s_sanitizeError = typeof(GitHubPagesDeployProvider)
        .GetMethod("SanitizeError", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo s_augmentErrorHint = typeof(GitHubPagesDeployProvider)
        .GetMethod("AugmentErrorHint", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo s_resolveGit = typeof(GitHubPagesDeployProvider)
        .GetMethod("ResolveGit", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo s_copyDirectory = typeof(GitHubPagesDeployProvider)
        .GetMethod("CopyDirectory", BindingFlags.NonPublic | BindingFlags.Static)!;

    public DeployProviderExtendedTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-deploy-ext-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void SanitizeError_HidesToken()
    {
        var message = "Error with token ghp_abc123secret in url";
        var token = "ghp_abc123secret";

        var result = (string)s_sanitizeError.Invoke(null, new object[] { message, token })!;

        Assert.DoesNotContain("ghp_abc123secret", result, StringComparison.Ordinal);
        Assert.Contains("***", result, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizeError_EmptyToken_ReturnsUnchanged()
    {
        var message = "Error without token";

        var result = (string)s_sanitizeError.Invoke(null, new object[] { message, "" })!;

        Assert.Equal(message, result);
    }

    [Fact]
    public void AugmentErrorHint_Forbidden_AddsScopeHint()
    {
        var message = "403 Forbidden";

        var result = (string)s_augmentErrorHint.Invoke(null, new object[] { message })!;

        Assert.Contains("repo' scope", result, StringComparison.Ordinal);
    }

    [Fact]
    public void AugmentErrorHint_PermissionDenied_AddsVerifyHint()
    {
        var message = "Permission denied";

        var result = (string)s_augmentErrorHint.Invoke(null, new object[] { message })!;

        Assert.Contains("Verify your GITHUB_TOKEN", result, StringComparison.Ordinal);
    }

    [Fact]
    public void AugmentErrorHint_NetworkError_AddsNetworkHint()
    {
        var message = "Could not resolve host";

        var result = (string)s_augmentErrorHint.Invoke(null, new object[] { message })!;

        Assert.Contains("network connection", result, StringComparison.Ordinal);
    }

    [Fact]
    public void AugmentErrorHint_UnknownError_ReturnsUnchanged()
    {
        var message = "Some random error";

        var result = (string)s_augmentErrorHint.Invoke(null, new object[] { message })!;

        Assert.Equal(message, result);
    }

    [Fact]
    public void ResolveGit_ReturnsNonNullWhenGitOnPath()
    {
        var result = s_resolveGit.Invoke(null, null);

        Assert.NotNull(result);
    }

    [Fact]
    public void CopyDirectory_CopiesFilesCorrectly()
    {
        var sourceDir = Path.Combine(_tempDir, "src");
        var destDir = Path.Combine(_tempDir, "dst");
        Directory.CreateDirectory(Path.Combine(sourceDir, "sub"));
        File.WriteAllText(Path.Combine(sourceDir, "index.html"), "<h1>hello</h1>");
        File.WriteAllText(Path.Combine(sourceDir, "sub", "style.css"), "body {}");

        s_copyDirectory.Invoke(null, new object[] { sourceDir, destDir });

        Assert.True(File.Exists(Path.Combine(destDir, "index.html")));
        Assert.True(File.Exists(Path.Combine(destDir, "sub", "style.css")));
        Assert.Equal("<h1>hello</h1>", File.ReadAllText(Path.Combine(destDir, "index.html")));
    }

    [Fact]
    public void CopyDirectory_SkipsGitDirectory()
    {
        var sourceDir = Path.Combine(_tempDir, "src2");
        var destDir = Path.Combine(_tempDir, "dst2");
        Directory.CreateDirectory(Path.Combine(sourceDir, ".git"));
        File.WriteAllText(Path.Combine(sourceDir, ".git", "config"), "git data");
        File.WriteAllText(Path.Combine(sourceDir, "index.html"), "<h1>hello</h1>");

        s_copyDirectory.Invoke(null, new object[] { sourceDir, destDir });

        Assert.True(File.Exists(Path.Combine(destDir, "index.html")));
        Assert.False(Directory.Exists(Path.Combine(destDir, ".git")));
    }

    [Fact]
    public void Name_ReturnsGitHubPages()
    {
        var provider = new GitHubPagesDeployProvider();
        Assert.Equal("github-pages", provider.Name);
    }

    [Fact]
    public async Task DeployAsync_NoGitHubToken_ReturnsError()
    {
        var dir = Path.Combine(_tempDir, "no-token");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "index.html"), "<h1>test</h1>");

        var originalToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);

            var context = new DeployContext
            {
                OutputDir = dir,
                SiteUrl = "https://example.com",
                BaseUrl = "/",
                Branch = "gh-pages",
                Message = "test",
                Cname = null,
                Logger = new TestLogger()
            };

            var provider = new GitHubPagesDeployProvider();
            var result = await provider.DeployAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("GITHUB_TOKEN", result.Error);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", originalToken);
        }
    }

    [Fact]
    public async Task DeployAsync_EmptyOutputDir_ReturnsError()
    {
        var dir = Path.Combine(_tempDir, "empty-dir");
        Directory.CreateDirectory(dir);

        var context = new DeployContext
        {
            OutputDir = dir,
            SiteUrl = "https://example.com",
            BaseUrl = "/",
            Branch = "gh-pages",
            Message = "test",
            Cname = null,
            Logger = new TestLogger()
        };

        var provider = new GitHubPagesDeployProvider();
        var result = await provider.DeployAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("empty", result.Error);
    }

    private sealed class TestLogger : ILogger
    {
        public List<string> Infos { get; } = new();
        public List<string> Errors { get; } = new();
        public List<string> Warns { get; } = new();
        public List<string> Debugs { get; } = new();

        public void Info(string message) => Infos.Add(message);
        public void Error(string message) => Errors.Add(message);
        public void Warn(string message) => Warns.Add(message);
        public void Debug(string message) => Debugs.Add(message);
    }
}
