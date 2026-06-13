using System.Reflection;
using Bukit.Cli;
using Bukit.Cli.Commands;
using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class CoreBoundaryTests
{
    [Fact]
    public void CoreCliCommands_MatchStableWhitelist()
    {
        var names = BukitCliSpecs.CreateRegistry().Commands.Select(c => c.Name).ToArray();

        Assert.Equal(
            [
                "build",
                "doctor",
                "config",
                "preview",
                "clean",
                "version",
                "completion",
                "seo",
                "geo",
                "publish",
                "deploy"
            ],
            names);
    }

    [Fact]
    public void CoreCliAssembly_DoesNotContainExperimentalCommandTypes()
    {
        var assembly = typeof(BuildCommand).Assembly;
        var forbiddenTypes = new[]
        {
            "Bukit.Cli.Commands.CloneCommand",
            "Bukit.Cli.Commands.ImportCommand",
            "Bukit.Cli.Commands.NotionCommand",
            "Bukit.Cli.Commands.IntentCommand",
            "Bukit.Cli.Commands.VisualCommand",
            "Bukit.Cli.Commands.WebhookCommand",
            "Bukit.Cli.Commands.DataCommand",
            "Bukit.Cli.Commands.ThemeCommand",
            "Bukit.Cli.Commands.ThemeInstallCommand",
            "Bukit.Cli.Commands.ThemePackCommand",
            "Bukit.Cli.Commands.ThemeRegistryCommand",
            "Bukit.Cli.Commands.PluginCommand"
        };

        foreach (var typeName in forbiddenTypes)
        {
            Assert.Null(assembly.GetType(typeName));
        }
    }

    [Fact]
    public void CoreCliProject_DoesNotReferenceImporting()
    {
        var projectText = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "Bukit.Cli", "Bukit.Cli.csproj"));

        Assert.DoesNotContain("Bukit.Importing", projectText, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginRegistry_DoesNotLoadExternalProtocolSource()
    {
        var sourceText = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "Bukit.Engine", "Plugins", "PluginRegistry.cs"));

        Assert.DoesNotContain("external-protocol", sourceText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ExternalProtocolPluginSource", sourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void DeployProvider_RequiresExplicitGitHubPagesWhenDeploySectionPresent()
    {
        var missingProvider = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(CreateConfig(provider: null)));
        Assert.Contains("deploy.provider is required when deploy section is present.", missingProvider.Message, StringComparison.Ordinal);

        ConfigValidator.Validate(CreateConfig(provider: "github-pages"));
        ConfigValidator.Validate(CreateConfig(provider: "GitHub-Pages"));

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(CreateConfig(provider: "custom")));
        Assert.Contains("deploy.provider must be 'github-pages' in Bukit 1.0.", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SeoAndPublishDiff_DoNotExposeAllowCrossSchema()
    {
        var registry = BukitCliSpecs.CreateRegistry();
        var seoDiffOptions = registry.ResolveSubcommand(registry.Resolve("seo")!, "diff")!.Options!;
        var publishDiffOptions = registry.ResolveSubcommand(registry.Resolve("publish")!, "diff")!.Options!;

        Assert.DoesNotContain(seoDiffOptions, o => o.Name == "--allow-cross-schema");
        Assert.DoesNotContain(publishDiffOptions, o => o.Name == "--allow-cross-schema");
    }

    private static AppConfig CreateConfig(string? provider) => new()
    {
        Site = new SiteConfig { Name = "test", Title = "Test" },
        Content = new ContentConfig
        {
            Sources =
            [
                new ContentSourceConfig
                {
                    Type = "markdown",
                    Markdown = new MarkdownConfig { Dir = "content" }
                }
            ]
        },
        Deploy = new DeployConfig { Provider = provider }
    };

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "Bukit.Cli", "Bukit.Cli.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
