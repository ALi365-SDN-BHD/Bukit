using Bukit.Cli.Commands;
using Bukit.Cli.Shared.Cli.Binding;
using Xunit;

namespace Bukit.Cli.Tests;

// Dry-run coverage for the deploy target precedence: --repo > deploy.repo > origin.
public sealed class DeployCommandRepoTargetTests
{
    private static async Task<(int ExitCode, string Output)> RunDryRunAsync(string? deployRepoLine, string? cliRepo)
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-deploy-repo-target-" + Guid.NewGuid().ToString("N"));
        var originalError = Console.Error;
        var writer = new StringWriter();

        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "dist"));
            File.WriteAllText(Path.Combine(root, "dist", ".bukit-build-state.json"), "{\"version\":1,\"status\":\"completed\"}");
            File.WriteAllText(Path.Combine(root, "dist", "index.html"), "<h1>Hello</h1>");
            var siteYaml = Path.Combine(root, "site.yaml");
            File.WriteAllText(siteYaml, $$"""
            site:
              name: test
              title: Test
              url: https://example.com
            content:
              sources:
                - type: markdown
                  name: page
                  collection: page
                  markdown:
                    dir: content
            deploy:
              provider: github-pages
            {{deployRepoLine}}
            """);

            Console.SetError(writer);

            var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--config"] = siteYaml,
                ["--dry-run"] = "true",
                ["--skip-build"] = "true",
                ["--output"] = "dist",
            };
            if (cliRepo is not null)
            {
                options["--repo"] = cliRepo;
            }

            var exitCode = await DeployCommand.RunAsync(new CliBoundCommand(options, Array.Empty<string>()));
            return (exitCode, writer.ToString());
        }
        finally
        {
            Console.SetError(originalError);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RunAsync_DryRun_CliRepoOverridesConfigRepo()
    {
        var (exitCode, output) = await RunDryRunAsync("  repo: config-owner/config-site", cliRepo: "cli-owner/cli-site");

        Assert.Equal(0, exitCode);
        Assert.Contains("repo: cli-owner/cli-site (from --repo)", output, StringComparison.Ordinal);
        Assert.DoesNotContain("config-owner/config-site", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_DryRun_ConfigRepoUsedWhenCliOmitted()
    {
        var (exitCode, output) = await RunDryRunAsync("  repo: config-owner/config-site", cliRepo: null);

        Assert.Equal(0, exitCode);
        Assert.Contains("repo: config-owner/config-site (from deploy.repo)", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_DryRun_NoRepoConfigured_ReportsOrigin()
    {
        var (exitCode, output) = await RunDryRunAsync(deployRepoLine: null, cliRepo: null);

        Assert.Equal(0, exitCode);
        Assert.Contains("repo: origin", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_InvalidCliRepo_ReturnsOneWithoutEchoingValue()
    {
        const string pasted = "https://github.com/secret-owner/secret-site.git";
        var (exitCode, output) = await RunDryRunAsync(deployRepoLine: null, cliRepo: pasted);

        Assert.Equal(1, exitCode);
        Assert.Contains("--repo must be 'owner/name'", output, StringComparison.Ordinal);
        Assert.DoesNotContain(pasted, output, StringComparison.Ordinal);
    }
}
