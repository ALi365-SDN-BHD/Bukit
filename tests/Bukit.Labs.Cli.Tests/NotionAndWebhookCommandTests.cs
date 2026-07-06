using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

[Collection("Console")]
public sealed class NotionAndWebhookCommandTests : IDisposable
{
    private readonly string _rootDir;

    public NotionAndWebhookCommandTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-labs-notion-webhook-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
    }

    [Fact]
    public async Task Webhook_RunAsync_Help_ReturnsZero()
    {
        var result = await CommandTestSupport.CaptureAsync(() =>
            WebhookCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["help"])));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage: bukit webhook [start] [options]", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Webhook_RunAsync_UnknownSubcommand_ReturnsTwo()
    {
        var result = await CommandTestSupport.CaptureAsync(() =>
            WebhookCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), ["push"])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unknown webhook subcommand: push", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("Usage: bukit webhook [start] [options]", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Webhook_RunAsync_InvalidPort_ReturnsTwo()
    {
        var result = await CommandTestSupport.CaptureAsync(() =>
            WebhookCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?> { ["--port"] = "70000" },
                [])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Invalid --port.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Webhook_RunAsync_MissingTokenEnvironmentVariable_ReturnsTwo()
    {
        using var webhookToken = new CommandTestSupport.EnvironmentVariableScope("BUKIT_WEBHOOK_TOKEN", null);

        var result = await CommandTestSupport.CaptureAsync(() =>
            WebhookCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), [])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Missing env: BUKIT_WEBHOOK_TOKEN", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Webhook_RunAsync_InvalidRepositoryValue_ReturnsTwo()
    {
        using var webhookToken = new CommandTestSupport.EnvironmentVariableScope("BUKIT_WEBHOOK_TOKEN", "secret");
        using var repo = new CommandTestSupport.EnvironmentVariableScope("BUKIT_GITHUB_REPO", "missing-slash");

        var result = await CommandTestSupport.CaptureAsync(() =>
            WebhookCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), [])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Missing --repo <owner/repo> or env: BUKIT_GITHUB_REPO", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Webhook_RunAsync_MissingGitHubToken_ReturnsTwo()
    {
        using var webhookToken = new CommandTestSupport.EnvironmentVariableScope("BUKIT_WEBHOOK_TOKEN", "secret");
        using var repo = new CommandTestSupport.EnvironmentVariableScope("BUKIT_GITHUB_REPO", "owner/repo");
        using var githubToken = new CommandTestSupport.EnvironmentVariableScope("BUKIT_GITHUB_TOKEN", null);
        using var fallbackToken = new CommandTestSupport.EnvironmentVariableScope("GITHUB_TOKEN", null);

        var result = await CommandTestSupport.CaptureAsync(() =>
            WebhookCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>(), [])));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Missing env: BUKIT_GITHUB_TOKEN (or GITHUB_TOKEN)", result.StdErr, StringComparison.Ordinal);
    }
}
