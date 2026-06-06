using Bukit.Cli.Deploy;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class DeployProviderTests
{
    [Fact]
    public void GitHubPagesProvider_Name_ReturnsCorrectName()
    {
        var provider = new GitHubPagesDeployProvider();
        Assert.Equal("github-pages", provider.Name);
    }

    [Fact]
    public void DeployContext_WithAllFields_CreatesCorrectly()
    {
        var logger = new TestLogger();
        var context = new DeployContext
        {
            OutputDir = "/tmp/dist",
            SiteUrl = "https://example.com",
            BaseUrl = "/",
            Branch = "gh-pages",
            Message = "deploy test",
            Cname = "example.com",
            Logger = logger
        };

        Assert.Equal("/tmp/dist", context.OutputDir);
        Assert.Equal("https://example.com", context.SiteUrl);
        Assert.Equal("/", context.BaseUrl);
        Assert.Equal("gh-pages", context.Branch);
        Assert.Equal("deploy test", context.Message);
        Assert.Equal("example.com", context.Cname);
        Assert.Same(logger, context.Logger);
    }

    [Fact]
    public void DeployContext_WithNullOptionalFields_CreatesCorrectly()
    {
        var logger = new TestLogger();
        var context = new DeployContext
        {
            OutputDir = "/tmp/dist",
            SiteUrl = "",
            BaseUrl = "/",
            Branch = null,
            Message = null,
            Cname = null,
            Logger = logger
        };

        Assert.Null(context.Branch);
        Assert.Null(context.Message);
        Assert.Null(context.Cname);
    }

    [Fact]
    public void DeployResult_Success_SetsProperties()
    {
        var result = new DeployResult
        {
            Success = true,
            DeployedUrl = "https://example.github.io"
        };

        Assert.True(result.Success);
        Assert.Equal("https://example.github.io", result.DeployedUrl);
        Assert.Null(result.Error);
    }

    [Fact]
    public void DeployResult_Failure_SetsProperties()
    {
        var result = new DeployResult
        {
            Success = false,
            Error = "GITHUB_TOKEN not set"
        };

        Assert.False(result.Success);
        Assert.Equal("GITHUB_TOKEN not set", result.Error);
        Assert.Null(result.DeployedUrl);
    }

    [Fact]
    public async Task DeployAsync_NoOutputDir_ReturnsError()
    {
        var logger = new TestLogger();
        var context = new DeployContext
        {
            OutputDir = "/nonexistent/path/12345",
            SiteUrl = "https://example.com",
            BaseUrl = "/",
            Branch = "gh-pages",
            Message = "test",
            Cname = null,
            Logger = logger
        };

        var provider = new GitHubPagesDeployProvider();
        var result = await provider.DeployAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Output directory not found", result.Error);
    }

    [Fact]
    public async Task DeployAsync_EmptyOutputDir_ReturnsError()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-empty-deploy", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var logger = new TestLogger();
            var context = new DeployContext
            {
                OutputDir = dir,
                SiteUrl = "https://example.com",
                BaseUrl = "/",
                Branch = "gh-pages",
                Message = "test",
                Cname = null,
                Logger = logger
            };

            var provider = new GitHubPagesDeployProvider();
            var result = await provider.DeployAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("empty", result.Error);
        }
        finally
        {
            TestCleanup.DeleteDirectory(dir, recursive: true);
        }
    }

    [Fact]
    public async Task DeployAsync_NoGitHubToken_ReturnsError()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-no-token", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "index.html"), "<h1>test</h1>");
        try
        {
            var originalToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            try
            {
                Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);

                var logger = new TestLogger();
                var context = new DeployContext
                {
                    OutputDir = dir,
                    SiteUrl = "https://example.com",
                    BaseUrl = "/",
                    Branch = "gh-pages",
                    Message = "test",
                    Cname = null,
                    Logger = logger
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
        finally
        {
            TestCleanup.DeleteDirectory(dir, recursive: true);
        }
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
