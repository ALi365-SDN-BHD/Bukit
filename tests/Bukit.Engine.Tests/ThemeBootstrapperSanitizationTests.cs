using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ThemeBootstrapperSanitizationTests : IDisposable
{
    private readonly string _rootDir;
    private readonly CapturingLogger _logger;

    public ThemeBootstrapperSanitizationTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-theme-bootstrap-sanitize-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
        _logger = new CapturingLogger();
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
    }

    [Fact]
    public void Resolve_Should_ThrowConfigException_When_ThemeNameIsPathTraversal()
    {
        var theme = new ThemeConfig { Name = "../../../etc" };

        Assert.Throws<ConfigException>(() => ThemePathResolver.Resolve(_rootDir, theme, _logger));
    }

    [Fact]
    public void Resolve_Should_ThrowConfigException_When_ThemeNameContainsPathSeparator()
    {
        var theme = new ThemeConfig { Name = "foo/bar" };

        Assert.Throws<ConfigException>(() => ThemePathResolver.Resolve(_rootDir, theme, _logger));
    }

    [Fact]
    public void Resolve_Should_WarnAndSkipParent_When_ExtendsIsMalicious()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "child");
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts"));

        var theme = new ThemeConfig { Name = "child", Extends = "../../etc" };

        var result = ThemePathResolver.Resolve(_rootDir, theme, _logger);

        Assert.Null(result.ParentThemeRoot);
        Assert.Null(result.ParentLayoutsDir);
        Assert.Contains(_logger.Warnings, w => w.Contains("theme.extends", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Resolve_Should_WarnAndSkipParent_When_ExtendsContainsPathSeparator()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "child");
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts"));

        var theme = new ThemeConfig { Name = "child", Extends = "foo/bar" };

        var result = ThemePathResolver.Resolve(_rootDir, theme, _logger);

        Assert.Null(result.ParentThemeRoot);
        Assert.Contains(_logger.Warnings, w => w.Contains("theme.extends", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Resolve_Should_AcceptValidExtends()
    {
        var childRoot = Path.Combine(_rootDir, "themes", "child");
        var parentRoot = Path.Combine(_rootDir, "themes", "parent");
        Directory.CreateDirectory(Path.Combine(childRoot, "layouts"));
        Directory.CreateDirectory(Path.Combine(parentRoot, "layouts"));

        var theme = new ThemeConfig { Name = "child", Extends = "parent" };
        var result = ThemePathResolver.Resolve(_rootDir, theme, _logger);

        Assert.NotNull(result.ParentThemeRoot);
        Assert.EndsWith(Path.Combine("themes", "parent"), result.ParentThemeRoot);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = new();
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { Warnings.Add(message); }
        public void Error(string message) { }
    }
}
