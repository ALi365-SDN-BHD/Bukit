using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class WebhookCommandTests
{
    [Fact]
    public async Task RunAsync_MissingWebhookToken_ReturnsError()
    {
        var originalToken = Environment.GetEnvironmentVariable("BUKIT_WEBHOOK_TOKEN");
        var originalGitHubRepo = Environment.GetEnvironmentVariable("BUKIT_GITHUB_REPO");
        var originalGitHubToken = Environment.GetEnvironmentVariable("BUKIT_GITHUB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("BUKIT_WEBHOOK_TOKEN", null);
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_REPO", "owner/repo");
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_TOKEN", "ghp_test");

            var reader = new ArgReader(new[] { "webhook" });

            var code = await WebhookCommand.RunAsync(reader);

            Assert.Equal(2, code);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BUKIT_WEBHOOK_TOKEN", originalToken);
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_REPO", originalGitHubRepo);
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_TOKEN", originalGitHubToken);
        }
    }

    [Fact]
    public async Task RunAsync_MissingGitHubToken_ReturnsError()
    {
        var originalToken = Environment.GetEnvironmentVariable("BUKIT_WEBHOOK_TOKEN");
        var originalGitHubRepo = Environment.GetEnvironmentVariable("BUKIT_GITHUB_REPO");
        var originalGitHubToken = Environment.GetEnvironmentVariable("BUKIT_GITHUB_TOKEN");
        var originalTokenAlt = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("BUKIT_WEBHOOK_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_REPO", "owner/repo");
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_TOKEN", null);
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);

            var reader = new ArgReader(new[] { "webhook" });

            var code = await WebhookCommand.RunAsync(reader);

            Assert.Equal(2, code);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BUKIT_WEBHOOK_TOKEN", originalToken);
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_REPO", originalGitHubRepo);
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_TOKEN", originalGitHubToken);
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", originalTokenAlt);
        }
    }

    [Fact]
    public async Task RunAsync_MissingRepo_ReturnsError()
    {
        var originalToken = Environment.GetEnvironmentVariable("BUKIT_WEBHOOK_TOKEN");
        var originalGitHubRepo = Environment.GetEnvironmentVariable("BUKIT_GITHUB_REPO");
        var originalGitHubToken = Environment.GetEnvironmentVariable("BUKIT_GITHUB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("BUKIT_WEBHOOK_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_REPO", null);
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_TOKEN", "ghp_test");

            var reader = new ArgReader(new[] { "webhook" });

            var code = await WebhookCommand.RunAsync(reader);

            Assert.Equal(2, code);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BUKIT_WEBHOOK_TOKEN", originalToken);
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_REPO", originalGitHubRepo);
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_TOKEN", originalGitHubToken);
        }
    }

    [Fact]
    public async Task RunAsync_InvalidRepoFormat_ReturnsError()
    {
        var originalToken = Environment.GetEnvironmentVariable("BUKIT_WEBHOOK_TOKEN");
        var originalGitHubRepo = Environment.GetEnvironmentVariable("BUKIT_GITHUB_REPO");
        var originalGitHubToken = Environment.GetEnvironmentVariable("BUKIT_GITHUB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("BUKIT_WEBHOOK_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_REPO", null);
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_TOKEN", "ghp_test");

            var reader = new ArgReader(new[] { "webhook", "--repo", "invalidrepo" });

            var code = await WebhookCommand.RunAsync(reader);

            Assert.Equal(2, code);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BUKIT_WEBHOOK_TOKEN", originalToken);
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_REPO", originalGitHubRepo);
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_TOKEN", originalGitHubToken);
        }
    }

    [Fact]
    public async Task RunAsync_InvalidPort_ReturnsError()
    {
        var originalToken = Environment.GetEnvironmentVariable("BUKIT_WEBHOOK_TOKEN");
        var originalGitHubRepo = Environment.GetEnvironmentVariable("BUKIT_GITHUB_REPO");
        var originalGitHubToken = Environment.GetEnvironmentVariable("BUKIT_GITHUB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("BUKIT_WEBHOOK_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_REPO", "owner/repo");
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_TOKEN", "ghp_test");

            var reader = new ArgReader(new[] { "webhook", "--port", "not-a-port" });

            var code = await WebhookCommand.RunAsync(reader);

            Assert.Equal(2, code);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BUKIT_WEBHOOK_TOKEN", originalToken);
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_REPO", originalGitHubRepo);
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_TOKEN", originalGitHubToken);
        }
    }

    [Fact]
    public async Task RunAsync_PortOutOfRange_ReturnsError()
    {
        var originalToken = Environment.GetEnvironmentVariable("BUKIT_WEBHOOK_TOKEN");
        var originalGitHubRepo = Environment.GetEnvironmentVariable("BUKIT_GITHUB_REPO");
        var originalGitHubToken = Environment.GetEnvironmentVariable("BUKIT_GITHUB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("BUKIT_WEBHOOK_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_REPO", "owner/repo");
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_TOKEN", "ghp_test");

            var reader = new ArgReader(new[] { "webhook", "--port", "99999" });

            var code = await WebhookCommand.RunAsync(reader);

            Assert.Equal(2, code);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BUKIT_WEBHOOK_TOKEN", originalToken);
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_REPO", originalGitHubRepo);
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_TOKEN", originalGitHubToken);
        }
    }

    [Fact]
    public async Task RunAsync_Defaults_Port8787()
    {
        var originalToken = Environment.GetEnvironmentVariable("BUKIT_WEBHOOK_TOKEN");
        var originalGitHubRepo = Environment.GetEnvironmentVariable("BUKIT_GITHUB_REPO");
        var originalGitHubToken = Environment.GetEnvironmentVariable("BUKIT_GITHUB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("BUKIT_WEBHOOK_TOKEN", null);
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_REPO", "owner/repo");
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_TOKEN", "ghp_test");

            var reader = new ArgReader(new[] { "webhook" });

            var code = await WebhookCommand.RunAsync(reader);

            Assert.Equal(2, code);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BUKIT_WEBHOOK_TOKEN", originalToken);
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_REPO", originalGitHubRepo);
            Environment.SetEnvironmentVariable("BUKIT_GITHUB_TOKEN", originalGitHubToken);
        }
    }
}
