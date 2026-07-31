using Xunit;
using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Engine.Tests;

/// <summary>
/// Extended tests for BuildPathUtils theme directory resolution and Windows compatibility checks.
/// </summary>
public sealed class BuildPathUtilsExtendedTests : IDisposable
{
    private readonly string _testDir;

    public BuildPathUtilsExtendedTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "bukit-buildpath-ext-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_testDir, recursive: true);
    }

    // ── NormalizeBaseUrl: edge cases ────────────────────────────────

    [Fact]
    public void NormalizeBaseUrl_WhitespaceOnly_ReturnsSlash()
    {
        var result = BuildPathUtils.NormalizeBaseUrl("   ");
        Assert.Equal("/", result);
    }

    [Fact]
    public void NormalizeBaseUrl_SingleSlash_ReturnsSlash()
    {
        var result = BuildPathUtils.NormalizeBaseUrl("/");
        Assert.Equal("/", result);
    }

    [Fact]
    public void NormalizeBaseUrl_NoLeadingSlash_AddsSlash()
    {
        var result = BuildPathUtils.NormalizeBaseUrl("blog");
        Assert.Equal("/blog", result);
    }

    [Fact]
    public void NormalizeBaseUrl_TrailingSlashOnly_RemovesTrailing()
    {
        var result = BuildPathUtils.NormalizeBaseUrl("/docs/");
        Assert.Equal("/docs", result);
    }

    // ── NormalizeRelPath ────────────────────────────────────────────

    [Fact]
    public void NormalizeRelPath_BackslashToForwardSlash()
    {
        var result = BuildPathUtils.NormalizeRelPath("path\\to\\file.txt");
        Assert.Equal("path/to/file.txt", result);
    }

    [Fact]
    public void NormalizeRelPath_AlreadyForwardSlash_Unchanged()
    {
        var result = BuildPathUtils.NormalizeRelPath("path/to/file.txt");
        Assert.Equal("path/to/file.txt", result);
    }

    // ── IsWindowsDeviceName ─────────────────────────────────────────

    [Theory]
    [InlineData("CON", true)]
    [InlineData("con", true)]
    [InlineData("PRN", true)]
    [InlineData("AUX", true)]
    [InlineData("NUL", true)]
    [InlineData("COM1", true)]
    [InlineData("COM9", true)]
    [InlineData("LPT1", true)]
    [InlineData("LPT9", true)]
    [InlineData("hello", false)]
    [InlineData("COM", false)] // too short
    [InlineData("COM10", false)] // too long
    [InlineData("LPT", false)]
    [InlineData("LPT10", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsWindowsDeviceName_VariousInputs(string name, bool expected)
    {
        var result = BuildPathUtils.IsWindowsDeviceName(name);
        Assert.Equal(expected, result);
    }

    // ── TryGetWindowsPathIssue: edge cases ──────────────────────────

    [Fact]
    public void TryGetWindowsPathIssue_EmptyPath_ReturnsEmpty()
    {
        var result = BuildPathUtils.TryGetWindowsPathIssue("", out var issue);
        Assert.True(result);
        Assert.Contains("empty", issue);
    }

    [Fact]
    public void TryGetWindowsPathIssue_TrailingDot_ReturnsIssue()
    {
        var result = BuildPathUtils.TryGetWindowsPathIssue("file.", out var issue);
        Assert.True(result);
        Assert.Contains("space or dot", issue);
    }

    [Fact]
    public void TryGetWindowsPathIssue_TrailingSpace_ReturnsIssue()
    {
        var result = BuildPathUtils.TryGetWindowsPathIssue("file ", out var issue);
        Assert.True(result);
        Assert.Contains("space or dot", issue);
    }

    [Fact]
    public void TryGetWindowsPathIssue_InvalidChar_ReturnsIssue()
    {
        var result = BuildPathUtils.TryGetWindowsPathIssue("file<name", out var issue);
        Assert.True(result);
        Assert.Contains("invalid Windows character", issue);
    }

    [Fact]
    public void TryGetWindowsPathIssue_ValidPath_NoIssue()
    {
        var result = BuildPathUtils.TryGetWindowsPathIssue("valid/path/name.txt", out _);
        Assert.False(result);
    }

    // ── WarnIfWindowsIncompatible ───────────────────────────────────

    [Fact]
    public void WarnIfWindowsIncompatible_EmptyPath_NoWarn()
    {
        var warned = new HashSet<string>();
        var logger = new TestLogger();
        BuildPathUtils.WarnIfWindowsIncompatible("", warned, logger);
        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public void WarnIfWindowsIncompatible_ValidPath_NoWarn()
    {
        var warned = new HashSet<string>();
        var logger = new TestLogger();
        BuildPathUtils.WarnIfWindowsIncompatible("valid/path/file.txt", warned, logger);
        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public void WarnIfWindowsIncompatible_DuplicatePath_NoDoubleWarn()
    {
        var warned = new HashSet<string>();
        var logger = new TestLogger();
        BuildPathUtils.WarnIfWindowsIncompatible("file.", warned, logger);
        BuildPathUtils.WarnIfWindowsIncompatible("file.", warned, logger);
        Assert.Single(logger.Warnings);
    }

    // ── ResolveThemeDirectories ─────────────────────────────────────

    [Fact]
    public void ResolveThemeDirectories_NoThemeName_UsesRelativePaths()
    {
        var layoutsDir = Path.Combine(_testDir, "layouts");
        var assetsDir = Path.Combine(_testDir, "assets");
        var staticDir = Path.Combine(_testDir, "static");
        Directory.CreateDirectory(layoutsDir);
        Directory.CreateDirectory(assetsDir);
        Directory.CreateDirectory(staticDir);

        var theme = new ThemeConfig { Layouts = "layouts", Assets = "assets", Static = "static" };
        var (layouts, assets, stat, parentLayouts, parentAssets, parentStatic, userLayouts) =
            BuildPathUtils.ResolveThemeDirectories(_testDir, theme);

        Assert.Equal(layoutsDir, layouts);
        Assert.Equal(assetsDir, assets);
        Assert.Equal(staticDir, stat);
        Assert.Null(parentLayouts);
        Assert.Null(parentAssets);
        Assert.Null(parentStatic);
    }

    [Fact]
    public void ResolveThemeDirectories_WithThemeName_ResolvesFromThemesDir()
    {
        var themeRoot = Path.Combine(_testDir, "themes", "mytheme");
        var layoutsDir = Path.Combine(themeRoot, "layouts");
        var assetsDir = Path.Combine(themeRoot, "assets");
        var staticDir = Path.Combine(themeRoot, "static");
        Directory.CreateDirectory(layoutsDir);
        Directory.CreateDirectory(assetsDir);
        Directory.CreateDirectory(staticDir);

        var theme = new ThemeConfig { Name = "mytheme", Layouts = "layouts", Assets = "assets", Static = "static" };
        var (layouts, assets, stat, _, _, _, _) =
            BuildPathUtils.ResolveThemeDirectories(_testDir, theme);

        Assert.Equal(layoutsDir, layouts);
        Assert.Equal(assetsDir, assets);
        Assert.Equal(staticDir, stat);
    }

    [Fact]
    public void ResolveThemeDirectories_UserLayoutsDir_ExistsWhenPresent()
    {
        var userLayouts = Path.Combine(_testDir, "layouts");
        Directory.CreateDirectory(userLayouts);

        var theme = new ThemeConfig { Layouts = "layouts", Assets = "assets", Static = "static" };
        var (_, _, _, _, _, _, resolvedUserLayouts) =
            BuildPathUtils.ResolveThemeDirectories(_testDir, theme);

        Assert.Equal(userLayouts, resolvedUserLayouts);
    }

    [Fact]
    public void ResolveThemeDirectories_UserLayoutsDir_NullWhenAbsent()
    {
        var theme = new ThemeConfig { Layouts = "layouts", Assets = "assets", Static = "static" };
        var (_, _, _, _, _, _, resolvedUserLayouts) =
            BuildPathUtils.ResolveThemeDirectories(_testDir, theme);

        Assert.Null(resolvedUserLayouts);
    }

    [Fact]
    public void ResolveThemeDirectories_CustomLayoutsPath_ResolvesAbsolute()
    {
        var customLayouts = Path.Combine(_testDir, "custom-layouts");
        Directory.CreateDirectory(customLayouts);

        var theme = new ThemeConfig
        {
            Name = "mytheme",
            Layouts = "custom-layouts",
            Assets = "assets",
            Static = "static"
        };
        var (layouts, _, _, _, _, _, _) =
            BuildPathUtils.ResolveThemeDirectories(_testDir, theme);

        Assert.Equal(customLayouts, layouts);
    }

    // ── RenderSimplePage: edge cases ────────────────────────────────

    [Fact]
    public void RenderSimplePage_WithBaseUrl_IncludesCanonical()
    {
        var result = BuildPathUtils.RenderSimplePage("/blog", "Test", "/post/", "<p>content</p>");
        Assert.Contains("/blog/post/", result);
        Assert.Contains("/blog/assets/style.css", result);
    }

    [Fact]
    public void RenderSimplePage_RootBaseUrl_UsesSlashPrefix()
    {
        var result = BuildPathUtils.RenderSimplePage("/", "Test", "/post/", "<p>content</p>");
        Assert.Contains("/post/", result);
        Assert.Contains("/assets/style.css", result);
    }

    // ── RenderSimpleIndex: edge cases ───────────────────────────────

    [Fact]
    public void RenderSimpleIndex_EmptyList_ReturnsEmptyUl()
    {
        var result = BuildPathUtils.RenderSimpleIndex("/", Array.Empty<Bukit.Engine.Abstractions.Content.RoutedContentDocument>());
        Assert.Contains("<ul>", result);
        Assert.Contains("</ul>", result);
        Assert.DoesNotContain("<li>", result);
    }

    // ── TestLogger ──────────────────────────────────────────────────

    private sealed class TestLogger : ILogger
    {
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();
        public void Info(string message) { }
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message) => Errors.Add(message);
        public void Debug(string message) { }
    }
}
