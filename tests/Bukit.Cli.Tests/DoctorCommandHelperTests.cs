using System.Reflection;
using Bukit.Cli.Commands;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class DoctorCommandHelperTests : IDisposable
{
    private readonly string _rootDir;

    private static readonly MethodInfo s_checkOutputDirectorySafety = typeof(DoctorCommand)
        .GetMethod("CheckOutputDirectorySafety", BindingFlags.NonPublic | BindingFlags.Static)!
        ;

    private static readonly MethodInfo s_checkFollowSymlinksSafety = typeof(DoctorCommand)
        .GetMethod("CheckFollowSymlinksSafety", BindingFlags.NonPublic | BindingFlags.Static)!
        ;

    private static readonly MethodInfo s_printDataModuleSummary = typeof(DoctorCommand)
        .GetMethod("PrintDataModuleSummary", BindingFlags.NonPublic | BindingFlags.Static)!
        ;

    private static readonly MethodInfo s_hasNotionSource = typeof(DoctorCommand)
        .GetMethod("HasNotionSource", BindingFlags.NonPublic | BindingFlags.Static)!
        ;

    public DoctorCommandHelperTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-doctor-helper-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
    }

    [Fact]
    public void CheckOutputDirectorySafety_WhenOutputMissing_ReturnsTrue()
    {
        var config = CreateConfig(build: new BuildConfig { Output = "dist", Clean = true });

        var (result, output) = CaptureStdOut(() => (bool)s_checkOutputDirectorySafety.Invoke(null, [config, _rootDir])!);

        Assert.True(result);
        Assert.Contains("directory will be created", output, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckOutputDirectorySafety_WhenOutputEmpty_ReturnsTrue()
    {
        Directory.CreateDirectory(Path.Combine(_rootDir, "dist"));
        var config = CreateConfig(build: new BuildConfig { Output = "dist", Clean = true });

        var (result, output) = CaptureStdOut(() => (bool)s_checkOutputDirectorySafety.Invoke(null, [config, _rootDir])!);

        Assert.True(result);
        Assert.Contains("directory is empty", output, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckOutputDirectorySafety_WhenCleanDisabled_ReturnsTrue()
    {
        var outputDir = Path.Combine(_rootDir, "dist");
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, "index.html"), "<html></html>");
        var config = CreateConfig(build: new BuildConfig { Output = "dist", Clean = false });

        var (result, output) = CaptureStdOut(() => (bool)s_checkOutputDirectorySafety.Invoke(null, [config, _rootDir])!);

        Assert.True(result);
        Assert.Contains("existing files will be overwritten", output, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckOutputDirectorySafety_WhenMarkerExists_ReturnsTrue()
    {
        var outputDir = Path.Combine(_rootDir, "dist");
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, "index.html"), "<html></html>");
        File.WriteAllText(Path.Combine(outputDir, ".bukit-output-marker"), string.Empty);
        var config = CreateConfig(build: new BuildConfig { Output = "dist", Clean = true });

        var (result, output) = CaptureStdOut(() => (bool)s_checkOutputDirectorySafety.Invoke(null, [config, _rootDir])!);

        Assert.True(result);
        Assert.Contains("directory has Bukit marker", output, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckOutputDirectorySafety_WhenNonEmptyWithoutMarker_ReturnsFalse()
    {
        var outputDir = Path.Combine(_rootDir, "dist");
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, "index.html"), "<html></html>");
        var config = CreateConfig(build: new BuildConfig { Output = "dist", Clean = true });

        var (result, output) = CaptureStdOut(() => (bool)s_checkOutputDirectorySafety.Invoke(null, [config, _rootDir])!);

        Assert.False(result);
        Assert.Contains("clean would be blocked", output, StringComparison.Ordinal);
        Assert.Contains("bukit clean --init-marker", output, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckFollowSymlinksSafety_WhenEnabled_PrintsWarning()
    {
        var config = CreateConfig(build: new BuildConfig { FollowSymlinks = true });

        var (_, output) = CaptureStdOut(() =>
        {
            s_checkFollowSymlinksSafety.Invoke(null, [config]);
            return 0;
        });

        Assert.Contains("build.followSymlinks is enabled", output, StringComparison.Ordinal);
    }

    [Fact]
    public void PrintDataModuleSummary_WithMixedDataItems_PrintsGroupedSummary()
    {
        var documents = new[]
        {
            ContentDocument.Create("page-1", "Page", "page-1", DateTimeOffset.UtcNow, "<p>page</p>",
                new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                {
                    ["type"] = new("text", "page")
                }),
            ContentDocument.Create("faq-1", "FAQ One", "faq-1", DateTimeOffset.UtcNow, null,
                new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sourceMode"] = new("text", "data"),
                    ["sourceKey"] = new("text", "cms"),
                    ["type"] = new("text", "faq"),
                    ["language"] = new("text", "en"),
                    ["question"] = new("text", "What?")
                }),
            ContentDocument.Create("faq-2", "FAQ Two", "faq-2", DateTimeOffset.UtcNow, null,
                new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sourceMode"] = new("text", "data"),
                    ["sourceKey"] = new("text", "cms"),
                    ["type"] = new("text", "faq"),
                    ["language"] = new("text", "ms"),
                    ["answer"] = new("text", "Because")
                }),
            ContentDocument.Create("asset-1", "Asset", "asset-1", DateTimeOffset.UtcNow, null,
                new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sourceMode"] = new("text", "data"),
                    ["type"] = new("text", "asset")
                })
        };

        var (_, output) = CaptureStdOut(() =>
        {
            s_printDataModuleSummary.Invoke(null, [documents]);
            return 0;
        });

        Assert.Contains("Data modules:", output, StringComparison.Ordinal);
        Assert.Contains("faq", output, StringComparison.Ordinal);
        Assert.Contains("lang=mixed", output, StringComparison.Ordinal);
        Assert.Contains("[answer, language, question, sourceKey, sourceMode, type]", output, StringComparison.Ordinal);
        Assert.Contains("asset", output, StringComparison.Ordinal);
        Assert.Contains("lang=-", output, StringComparison.Ordinal);
    }

    [Fact]
    public void PrintDataModuleSummary_WithNoDataItems_PrintsNone()
    {
        var documents = new[]
        {
            ContentDocument.Create("page-1", "Page", "page-1", DateTimeOffset.UtcNow, "<p>page</p>",
                new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                {
                    ["type"] = new("text", "page")
                })
        };

        var (_, output) = CaptureStdOut(() =>
        {
            s_printDataModuleSummary.Invoke(null, [documents]);
            return 0;
        });

        Assert.Contains("Data modules: (none)", output, StringComparison.Ordinal);
    }

    [Fact]
    public void HasNotionSource_DetectsBothTypeAndNotionConfig()
    {
        var withoutSources = new ContentConfig();
        var markdownOnly = new ContentConfig
        {
            Sources =
            [
                new ContentSourceConfig { Type = "markdown", Markdown = new MarkdownConfig() }
            ]
        };
        var notionByType = new ContentConfig
        {
            Sources =
            [
                new ContentSourceConfig { Type = "notion", Notion = new NotionConfig { DatabaseId = "db" } }
            ]
        };
        var notionByConfig = new ContentConfig
        {
            Sources =
            [
                new ContentSourceConfig { Type = "markdown", Notion = new NotionConfig { DatabaseId = "db" } }
            ]
        };

        Assert.False((bool)s_hasNotionSource.Invoke(null, [withoutSources])!);
        Assert.False((bool)s_hasNotionSource.Invoke(null, [markdownOnly])!);
        Assert.True((bool)s_hasNotionSource.Invoke(null, [notionByType])!);
        Assert.True((bool)s_hasNotionSource.Invoke(null, [notionByConfig])!);
    }

    private static AppConfig CreateConfig(BuildConfig? build = null)
    {
        return new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "Test" },
            Content = new ContentConfig(),
            Build = build ?? new BuildConfig()
        };
    }

    private static (T Result, string Output) CaptureStdOut<T>(Func<T> action)
    {
        using var writer = new StringWriter();
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(writer);
            var result = action();
            return (result, writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
