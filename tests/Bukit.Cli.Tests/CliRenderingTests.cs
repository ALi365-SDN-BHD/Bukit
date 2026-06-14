using System;
using System.IO;
using System.Linq;
using Bukit.Cli.Shared.Cli.Metadata;
using Bukit.Cli.Shared.Cli.Parsing;
using Bukit.Cli.Shared.Cli.Rendering;
using Bukit.Cli;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CliRenderingTests
{
    [Fact]
    public void RenderHelp_IncludesUsage_Arguments_AndOptions()
    {
        var spec = new CliCommandSpec(
            Name: "preview",
            Description: "本地预览 dist",
            Arguments: new[] { new CliArgumentSpec("dir", "目录", Required: false) },
            Options: new[] { new CliOptionSpec("--port", "预览端口", CliOptionType.Integer, ValueName: "port") });

        var text = CliHelpRenderer.Render(spec, "bukit preview");

        Assert.Contains("Usage:", text);
        Assert.Contains("bukit preview", text);
        Assert.Contains("--port <port>", text);
    }

    [Fact]
    public void RenderError_PrefixesPrimaryMessage()
    {
        var text = CliErrorRenderer.Render(new CliDiagnostic("invalid-option-value", "Invalid value for --port: abc"));
        Assert.Contains("Error:", text);
        Assert.Contains("Invalid value for --port: abc", text);
    }

    [Fact]
    public void RenderDevHelp_UsesLiveReloadTerm()
    {
        var registry = BukitCliSpecs.CreateRegistry();
        var dev = registry.Commands.Single(c => string.Equals(c.Name, "dev", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("LiveReload 实时预览开发服务器", dev.Description);
        var text = CliHelpRenderer.Render(dev, "bukit dev");

        Assert.DoesNotContain("HMR", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Hot Module Replacement", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SourceFiles_DoNotContainUserVisibleHmrPhrasing()
    {
        var cliRoot = FindRepoRoot();
        var sourceFiles = Directory.EnumerateFiles(
            Path.Combine(cliRoot, "src", "Bukit.Cli"),
            "*.cs",
            SearchOption.AllDirectories);

        foreach (var file in sourceFiles)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("HMR", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Hot Module Replacement", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void DevCommandSource_UsesLiveReloadBanner()
    {
        var cliRoot = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(cliRoot, "src", "Bukit.Cli", "Commands", "DevCommand.cs"));

        Assert.Contains("bukit dev — LiveReload development server", text, StringComparison.Ordinal);
        Assert.DoesNotContain("bukit dev - HMR development server", text, StringComparison.Ordinal);
        Assert.DoesNotContain("bukit dev — HMR development server", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Hot Module Replacement", text, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "src", "Bukit.Cli"))
                && File.Exists(Path.Combine(current.FullName, "tests", "Bukit.Cli.Tests", "Bukit.Cli.Tests.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root for CLI source checks.");
    }
}
