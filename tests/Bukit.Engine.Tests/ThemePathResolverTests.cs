using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ThemePathResolverTests : IDisposable
{
    private readonly string _rootDir;
    private readonly TestLogger _logger;

    public ThemePathResolverTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-theme-resolver-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
        _logger = new TestLogger();
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
    }

    [Fact]
    public void Resolve_NoThemeName_ReturnsMinimalPaths()
    {
        var theme = new ThemeConfig { Name = null };
        var result = ThemePathResolver.Resolve(_rootDir, theme, _logger);

        Assert.NotNull(result);
        Assert.Equal("default", result.ThemeName);
        Assert.Equal(_rootDir, result.ThemeRoot);
        Assert.Equal(Path.GetFullPath(Path.Combine(_rootDir, "layouts")), result.LayoutsDir);
        Assert.Equal(Path.GetFullPath(Path.Combine(_rootDir, "assets")), result.AssetsDir);
        Assert.Equal(Path.GetFullPath(Path.Combine(_rootDir, "static")), result.StaticDir);
        Assert.Null(result.ParentThemeRoot);
        Assert.Null(result.ParentLayoutsDir);
        Assert.Null(result.ParentAssetsDir);
        Assert.Null(result.ParentStaticDir);
    }

    [Fact]
    public void Resolve_LocalTheme_CreatesDirectoryStructureUnderRoot()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "my-theme");
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts"));
        Directory.CreateDirectory(Path.Combine(themeRoot, "assets"));
        Directory.CreateDirectory(Path.Combine(themeRoot, "static"));

        var theme = new ThemeConfig { Name = "my-theme" };
        var result = ThemePathResolver.Resolve(_rootDir, theme, _logger);

        Assert.Equal("my-theme", result.ThemeName);
        Assert.EndsWith(Path.Combine("themes", "my-theme"), result.ThemeRoot);
        Assert.EndsWith(Path.Combine("themes", "my-theme", "layouts"), result.LayoutsDir);
        Assert.EndsWith(Path.Combine("themes", "my-theme", "assets"), result.AssetsDir);
        Assert.EndsWith(Path.Combine("themes", "my-theme", "static"), result.StaticDir);
        Assert.Null(result.ParentThemeRoot);
        Assert.Null(result.ParentLayoutsDir);
        Assert.Null(result.ParentAssetsDir);
        Assert.Null(result.ParentStaticDir);
    }

    [Fact]
    public void Resolve_LocalTheme_DoesNotResolveManifestParentPaths()
    {
        var childRoot = Path.Combine(_rootDir, "themes", "child");
        var parentRoot = Path.Combine(_rootDir, "themes", "parent");
        Directory.CreateDirectory(Path.Combine(childRoot, "layouts"));
        Directory.CreateDirectory(Path.Combine(parentRoot, "layouts"));
        Directory.CreateDirectory(Path.Combine(parentRoot, "assets"));
        Directory.CreateDirectory(Path.Combine(parentRoot, "static"));

        var theme = new ThemeConfig { Name = "child" };
        var result = ThemePathResolver.Resolve(_rootDir, theme, _logger);

        Assert.Equal("child", result.ThemeName);
        Assert.EndsWith(Path.Combine("themes", "child"), result.ThemeRoot);
        Assert.Null(result.ParentThemeRoot);
        Assert.Null(result.ParentLayoutsDir);
        Assert.Null(result.ParentAssetsDir);
        Assert.Null(result.ParentStaticDir);
    }

    [Fact]
    public void Resolve_LocalTheme_ReturnsNoParent()
    {
        var childRoot = Path.Combine(_rootDir, "themes", "standalone");
        Directory.CreateDirectory(Path.Combine(childRoot, "layouts"));

        var theme = new ThemeConfig { Name = "standalone" };
        var result = ThemePathResolver.Resolve(_rootDir, theme, _logger);

        Assert.Null(result.ParentThemeRoot);
        Assert.Null(result.ParentLayoutsDir);
        Assert.Null(result.ParentAssetsDir);
        Assert.Null(result.ParentStaticDir);
    }

    [Fact]
    public void Resolve_CustomLayoutAssetsStaticPaths_RespectsCustomValues()
    {
        var customTheme = new ThemeConfig
        {
            Name = "custom-theme",
            Layouts = "my-layouts",
            Assets = "my-assets",
            Static = "my-static"
        };

        var result = ThemePathResolver.Resolve(_rootDir, customTheme, _logger);

        var expectedLayouts = Path.GetFullPath(Path.Combine(_rootDir, "my-layouts"));
        var expectedAssets = Path.GetFullPath(Path.Combine(_rootDir, "my-assets"));
        var expectedStatic = Path.GetFullPath(Path.Combine(_rootDir, "my-static"));

        Assert.Equal(expectedLayouts, result.LayoutsDir);
        Assert.Equal(expectedAssets, result.AssetsDir);
        Assert.Equal(expectedStatic, result.StaticDir);
    }

    [Fact]
    public void Resolve_UserLayoutsExists_ReturnsUserLayoutsDir()
    {
        var userLayouts = Path.Combine(_rootDir, "layouts");
        Directory.CreateDirectory(userLayouts);
        File.WriteAllText(Path.Combine(userLayouts, "override.html"), "test");

        var themeRoot = Path.Combine(_rootDir, "themes", "base");
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts"));

        var theme = new ThemeConfig { Name = "base" };
        var result = ThemePathResolver.Resolve(_rootDir, theme, _logger);

        Assert.NotNull(result.UserLayoutsDir);
        Assert.EndsWith("layouts", result.UserLayoutsDir);
    }

    [Fact]
    public void Resolve_UserLayoutsDoesNotExist_ReturnsNoUserLayoutsDir()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "base");
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts"));

        var theme = new ThemeConfig { Name = "base" };
        var result = ThemePathResolver.Resolve(_rootDir, theme, _logger);

        Assert.Null(result.UserLayoutsDir);
    }

    [Fact]
    public void Resolve_AbsoluteLayoutsPath_IsRespected()
    {
        var absoluteLayouts = Path.GetFullPath(Path.Combine(_rootDir, "custom-layouts"));
        Directory.CreateDirectory(absoluteLayouts);

        var theme = new ThemeConfig
        {
            Name = "base",
            Layouts = absoluteLayouts
        };

        var result = ThemePathResolver.Resolve(_rootDir, theme, _logger);

        Assert.Equal(absoluteLayouts, result.LayoutsDir);
    }

    [Fact]
    public void Resolve_AbsoluteLayoutsPath_OutsideRoot_ThrowsConfigException()
    {
        var absoluteLayouts = Path.Combine(Path.GetTempPath(), "bukit-test-abs-layouts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(absoluteLayouts);
        try
        {
            var theme = new ThemeConfig
            {
                Name = "base",
                Layouts = absoluteLayouts
            };

            Assert.Throws<ConfigException>(() => ThemePathResolver.Resolve(_rootDir, theme, _logger));
        }
        finally
        {
            TestCleanup.DeleteDirectory(absoluteLayouts, recursive: true);
        }
    }

    [Fact]
    public void Resolve_BuildPlanner_UsesSamePathsAsThemeBootstrapper()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "my-theme");
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts"));
        Directory.CreateDirectory(Path.Combine(themeRoot, "assets"));
        Directory.CreateDirectory(Path.Combine(themeRoot, "static"));

        var theme = new ThemeConfig { Name = "my-theme" };
        var result = ThemePathResolver.Resolve(_rootDir, theme, _logger);

        Assert.EndsWith(Path.Combine("themes", "my-theme"), result.ThemeRoot);
        Assert.EndsWith(Path.Combine("themes", "my-theme", "layouts"), result.LayoutsDir);
        Assert.EndsWith(Path.Combine("themes", "my-theme", "assets"), result.AssetsDir);
        Assert.EndsWith(Path.Combine("themes", "my-theme", "static"), result.StaticDir);
    }

    private sealed class TestLogger : ILogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }

}
