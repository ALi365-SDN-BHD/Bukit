using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class DeployCommandTests
{
    [Fact]
    public async Task RunAsync_DryRunSkipBuild_ReturnsZero()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-deploy-command-" + Guid.NewGuid().ToString("N"));
        var originalError = Console.Error;
        var writer = new StringWriter();

        try
        {
            Directory.CreateDirectory(root);
            var siteYaml = Path.Combine(root, "site.yaml");
            File.WriteAllText(siteYaml, """
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
            """);

            Console.SetError(writer);

            var exitCode = await DeployCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--config"] = siteYaml,
                    ["--dry-run"] = "true",
                    ["--skip-build"] = "true",
                    ["--output"] = "dist",
                    ["--branch"] = "pages",
                    ["--message"] = "deploy test"
                },
                Array.Empty<string>()));

            Assert.Equal(0, exitCode);
            var output = writer.ToString();
            Assert.Contains("--dry-run", output, StringComparison.Ordinal);
            Assert.Contains("branch: pages", output, StringComparison.Ordinal);
            Assert.Contains("message: deploy test", output, StringComparison.Ordinal);
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
    public async Task RunAsync_DryRunSkipBuildWithNotionSourceWithoutNotionToken_ReturnsZero()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-deploy-command-" + Guid.NewGuid().ToString("N"));
        var originalError = Console.Error;
        var originalToken = Environment.GetEnvironmentVariable("NOTION_TOKEN");
        var writer = new StringWriter();

        try
        {
            Directory.CreateDirectory(root);
            var siteYaml = Path.Combine(root, "site.yaml");
            File.WriteAllText(siteYaml, """
            site:
              name: test
              title: Test
              url: https://example.com
            content:
              sources:
                - type: notion
                  name: page
                  notion:
                    databaseId: abc
            deploy:
              provider: github-pages
            """);

            Environment.SetEnvironmentVariable("NOTION_TOKEN", null);
            Console.SetError(writer);

            var exitCode = await DeployCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--config"] = siteYaml,
                    ["--dry-run"] = "true",
                    ["--skip-build"] = "true"
                },
                Array.Empty<string>()));

            Assert.Equal(0, exitCode);
            var output = writer.ToString();
            Assert.Contains("--dry-run", output, StringComparison.Ordinal);
            Assert.Contains("Would deploy", output, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NOTION_TOKEN", originalToken);
            Console.SetError(originalError);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RunAsync_DryRunWithoutSkipBuildAndNotionToken_ReturnsConfigError()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-deploy-command-" + Guid.NewGuid().ToString("N"));
        var originalError = Console.Error;
        var originalToken = Environment.GetEnvironmentVariable("NOTION_TOKEN");
        var writer = new StringWriter();

        try
        {
            Directory.CreateDirectory(root);
            var siteYaml = Path.Combine(root, "site.yaml");
            File.WriteAllText(siteYaml, """
            site:
              name: test
              title: Test
              url: https://example.com
            content:
              sources:
                - type: notion
                  name: page
                  notion:
                    databaseId: abc
            deploy:
              provider: github-pages
            """);

            Environment.SetEnvironmentVariable("NOTION_TOKEN", null);
            Console.SetError(writer);

            var exitCode = await DeployCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--config"] = siteYaml,
                    ["--dry-run"] = "true"
                },
                Array.Empty<string>()));

            Assert.Equal(1, exitCode);
            Assert.Contains("NOTION_TOKEN is required for notion provider", writer.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NOTION_TOKEN", originalToken);
            Console.SetError(originalError);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RunAsync_DryRunWithDeployMissingProvider_ReturnsConfigError()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-deploy-command-" + Guid.NewGuid().ToString("N"));
        var originalError = Console.Error;
        var writer = new StringWriter();

        try
        {
            Directory.CreateDirectory(root);
            var siteYaml = Path.Combine(root, "site.yaml");
            File.WriteAllText(siteYaml, """
            site:
              name: test
              title: Test
            content:
              sources:
                - type: markdown
                  name: page
                  collection: page
                  markdown:
                    dir: content
            deploy:
              branch: gh-pages
            """);

            Console.SetError(writer);

            var exitCode = await DeployCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--config"] = siteYaml,
                    ["--dry-run"] = "true",
                    ["--skip-build"] = "true"
                },
                Array.Empty<string>()));

            Assert.Equal(1, exitCode);
            Assert.Contains("deploy.provider is required when deploy section is present.", writer.ToString(), StringComparison.Ordinal);
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
    public async Task RunAsync_SkipBuildWithUnsupportedProvider_ReturnsConfigError()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-deploy-command-" + Guid.NewGuid().ToString("N"));
        var originalError = Console.Error;
        var writer = new StringWriter();

        try
        {
            Directory.CreateDirectory(root);
            var siteYaml = Path.Combine(root, "site.yaml");
            File.WriteAllText(siteYaml, """
            site:
              name: test
              title: Test
            content:
              sources:
                - type: markdown
                  name: page
                  collection: page
                  markdown:
                    dir: content
            deploy:
              provider: custom
            """);

            Console.SetError(writer);

            var exitCode = await DeployCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--config"] = siteYaml,
                    ["--skip-build"] = "true"
                },
                Array.Empty<string>()));

            Assert.Equal(1, exitCode);
            Assert.Contains("deploy.provider must be 'github-pages' in Bukit 1.0.", writer.ToString(), StringComparison.Ordinal);
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
    public async Task RunAsync_DryRunSkipBuildWithoutDeploySection_ReturnsZero()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-deploy-command-" + Guid.NewGuid().ToString("N"));
        var originalError = Console.Error;
        var writer = new StringWriter();

        try
        {
            Directory.CreateDirectory(root);
            var siteYaml = Path.Combine(root, "site.yaml");
            File.WriteAllText(siteYaml, """
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
            """);

            Console.SetError(writer);

            var exitCode = await DeployCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--config"] = siteYaml,
                    ["--dry-run"] = "true",
                    ["--skip-build"] = "true",
                    ["--output"] = "dist"
                },
                Array.Empty<string>()));

            Assert.Equal(0, exitCode);
            var output = writer.ToString();
            Assert.Contains("--dry-run", output, StringComparison.Ordinal);
            Assert.Contains("branch: gh-pages", output, StringComparison.Ordinal);
            Assert.Contains("message: bukit deploy", output, StringComparison.Ordinal);
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
    public async Task RunAsync_DryRun_AppliesCliBaseUrlAndSiteUrlOverrides()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-deploy-command-" + Guid.NewGuid().ToString("N"));
        var originalError = Console.Error;
        var writer = new StringWriter();

        try
        {
            Directory.CreateDirectory(root);
            var siteYaml = Path.Combine(root, "site.yaml");
            File.WriteAllText(siteYaml, """
            site:
              name: test
              title: Test
              url: https://example.com
              baseUrl: /
            content:
              sources:
                - type: markdown
                  name: page
                  collection: page
                  markdown:
                    dir: content
            deploy:
              provider: github-pages
            """);

            Console.SetError(writer);

            var exitCode = await DeployCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--config"] = siteYaml,
                    ["--dry-run"] = "true",
                    ["--skip-build"] = "true",
                    ["--base-url"] = "docs",
                    ["--site-url"] = "https://docs.example.com",
                    ["--branch"] = "pages",
                    ["--message"] = "override deploy"
                },
                Array.Empty<string>()));

            Assert.Equal(0, exitCode);
            var output = writer.ToString();
            Assert.Contains("baseUrl: /docs", output, StringComparison.Ordinal);
            Assert.Contains("siteUrl: https://docs.example.com", output, StringComparison.Ordinal);
            Assert.Contains("branch: pages", output, StringComparison.Ordinal);
            Assert.Contains("message: override deploy", output, StringComparison.Ordinal);
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

    [Theory]
    [InlineData(false, "Output directory not found:")]
    [InlineData(true, "Output directory is empty:")]
    public async Task Deploy_SkipBuild_ValidatesOutputDirectoryExists(bool createEmptyOutputDir, string expectedError)
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-deploy-command-" + Guid.NewGuid().ToString("N"));
        var originalError = Console.Error;
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        var originalToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        var writer = new StringWriter();

        try
        {
            Directory.CreateDirectory(root);
            if (createEmptyOutputDir)
            {
                Directory.CreateDirectory(Path.Combine(root, "dist"));
            }

            var siteYaml = Path.Combine(root, "site.yaml");
            File.WriteAllText(siteYaml, """
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
            build:
              output: dist
            """);

            Environment.SetEnvironmentVariable("PATH", string.Empty);
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
            Console.SetError(writer);

            var exitCode = await DeployCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--config"] = siteYaml,
                    ["--skip-build"] = "true"
                },
                Array.Empty<string>()));

            Assert.Equal(1, exitCode);
            var output = writer.ToString();
            Assert.Contains(expectedError, output, StringComparison.Ordinal);
            Assert.DoesNotContain("git command not found", output, StringComparison.Ordinal);
            Assert.DoesNotContain("GITHUB_TOKEN", output, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", originalToken);
            Console.SetError(originalError);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RunAsync_SkipBuild_WhenProviderFails_ReturnsOne()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-deploy-command-" + Guid.NewGuid().ToString("N"));
        var originalError = Console.Error;
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        var originalToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        var writer = new StringWriter();

        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "dist"));
            File.WriteAllText(Path.Combine(root, "dist", "index.html"), "<h1>Hello</h1>");

            var siteYaml = Path.Combine(root, "site.yaml");
            File.WriteAllText(siteYaml, """
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
            build:
              output: dist
            """);

            Environment.SetEnvironmentVariable("PATH", string.Empty);
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", "secret-token");
            Console.SetError(writer);

            var exitCode = await DeployCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--config"] = siteYaml,
                    ["--skip-build"] = "true"
                },
                Array.Empty<string>()));

            Assert.Equal(1, exitCode);
            Assert.Contains("Deployment failed: git command not found.", writer.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", originalToken);
            Console.SetError(originalError);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
