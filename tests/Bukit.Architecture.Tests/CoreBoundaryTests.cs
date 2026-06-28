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
                "dev",
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
    public void LabsSources_DoNotUseCoreCliCommandNamespaces()
    {
        var labsDir = Path.Combine(FindRepoRoot(), "experimental");
        if (!Directory.Exists(labsDir))
        {
            return;
        }

        var offenders = Directory
            .EnumerateFiles(labsDir, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new { Path = path, Line = index + 1, Text = line }))
            .Where(x => x.Text.Contains("namespace Bukit.Cli", StringComparison.Ordinal)
                || x.Text.Contains("using Bukit.Cli.Commands", StringComparison.Ordinal))
            .Select(x => $"{Path.GetRelativePath(labsDir, x.Path)}:{x.Line}: {x.Text.Trim()}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void CoreCliProject_DoesNotReferenceOutOfCoreProjects()
    {
        var projectText = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "Bukit.Cli", "Bukit.Cli.csproj"));

        Assert.DoesNotContain("Bukit.Importing", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("Bukit.PluginHost", projectText, StringComparison.Ordinal);
        Assert.DoesNotContain("Bukit.Plugin.Abstractions", projectText, StringComparison.Ordinal);
    }

    [Fact]
    public void CoreSolution_IncludesOnlyCoreProjects()
    {
        var solutionText = File.ReadAllText(Path.Combine(FindRepoRoot(), "bukit.slnx"));
        var projectPaths = solutionText
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("<Project Path=", StringComparison.Ordinal))
            .Select(line => line.Split('"')[1])
            .ToArray();

        Assert.Equal(
            [
                "src/Bukit.Cli/Bukit.Cli.csproj",
                "src/Bukit.Cli.Shared/Bukit.Cli.Shared.csproj",
                "src/Bukit.Config/Bukit.Config.csproj",
                "src/Bukit.Content/Bukit.Content.csproj",
                "src/Bukit.Engine.Abstractions/Bukit.Engine.Abstractions.csproj",
                "src/Bukit.Engine/Bukit.Engine.csproj",
                "src/Bukit.Notion/Bukit.Notion.csproj",
                "src/Bukit.Rendering/Bukit.Rendering.csproj",
                "src/Bukit.Routing/Bukit.Routing.csproj",
                "src/Bukit.Shared/Bukit.Shared.csproj",
                "src/Bukit.Theme/Bukit.Theme.csproj",
                "tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj",
                "tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj",
                "tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj",
                "tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj",
                "tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj",
                "tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj",
                "tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj",
                "tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj",
                "tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj",
                "tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj"
            ],
            projectPaths);
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
    public void DeployConfig_DoesNotExposeOptionsBag()
    {
        Assert.Null(typeof(DeployConfig).GetProperty("Options"));
    }

    [Fact]
    public void ThemeConfig_DoesNotExposeRemoteSourceOrSiteLevelExtends()
    {
        Assert.Null(typeof(ThemeConfig).GetProperty("Source"));
        Assert.Null(typeof(ThemeConfig).GetProperty("Extends"));
    }

    [Fact]
    public void CoreEngine_DoesNotContainRemoteThemeSourceTooling()
    {
        var engineDir = Path.Combine(FindRepoRoot(), "src", "Bukit.Engine");
        var sourceText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(engineDir, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("ThemeSourceManager", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessGitRunner", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("IGitRunner", sourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void TestSources_DoNotHardcodeAbsoluteCurrentDirectories()
    {
        var testsDir = Path.Combine(FindRepoRoot(), "tests");
        var offenders = Directory
            .EnumerateFiles(testsDir, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new { Path = path, Line = index + 1, Text = line }))
            .Where(x => x.Text.Contains("Directory.SetCurrentDirectory(\"/", StringComparison.Ordinal) ||
                x.Text.Contains("Environment.CurrentDirectory = \"/", StringComparison.Ordinal))
            .Select(x => $"{Path.GetRelativePath(testsDir, x.Path)}:{x.Line}: {x.Text.Trim()}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
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
