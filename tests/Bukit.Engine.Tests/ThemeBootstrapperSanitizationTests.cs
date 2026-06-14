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
    public void Bootstrap_Should_ThrowConfigException_When_ManifestExtendsIsMalicious()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "child");
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts"));
        File.WriteAllText(Path.Combine(themeRoot, "theme.yaml"), """
            name: child
            extends: ../../etc
            """);

        var config = CreateConfig("child");

        Assert.Throws<ConfigException>(() => ThemeBootstrapper.Bootstrap(config, _rootDir, _logger));
    }

    [Fact]
    public void Bootstrap_Should_ThrowConfigException_When_ManifestExtendsContainsPathSeparator()
    {
        var themeRoot = Path.Combine(_rootDir, "themes", "child");
        Directory.CreateDirectory(Path.Combine(themeRoot, "layouts"));
        File.WriteAllText(Path.Combine(themeRoot, "theme.yaml"), """
            name: child
            extends: foo/bar
            """);

        var config = CreateConfig("child");

        Assert.Throws<ConfigException>(() => ThemeBootstrapper.Bootstrap(config, _rootDir, _logger));
    }

    [Fact]
    public void Bootstrap_Should_AcceptValidManifestExtends()
    {
        var childRoot = Path.Combine(_rootDir, "themes", "child");
        var parentRoot = Path.Combine(_rootDir, "themes", "parent");
        Directory.CreateDirectory(Path.Combine(childRoot, "layouts"));
        Directory.CreateDirectory(Path.Combine(parentRoot, "layouts"));
        File.WriteAllText(Path.Combine(childRoot, "theme.yaml"), """
            name: child
            extends: parent
            """);
        File.WriteAllText(Path.Combine(parentRoot, "theme.yaml"), """
            name: parent
            """);

        var result = ThemeBootstrapper.Bootstrap(CreateConfig("child"), _rootDir, _logger);

        Assert.NotNull(result.ParentThemeRoot);
        Assert.EndsWith(Path.Combine("themes", "parent"), result.ParentThemeRoot);
    }

    [Fact]
    public void Bootstrap_Should_ThrowConfigException_When_ManifestExtendsParentManifestMissing()
    {
        var childRoot = Path.Combine(_rootDir, "themes", "child");
        var parentRoot = Path.Combine(_rootDir, "themes", "parent");
        Directory.CreateDirectory(Path.Combine(childRoot, "layouts"));
        Directory.CreateDirectory(Path.Combine(parentRoot, "layouts"));
        File.WriteAllText(Path.Combine(childRoot, "theme.yaml"), """
            name: child
            extends: parent
            """);

        var ex = Assert.Throws<ConfigException>(() => ThemeBootstrapper.Bootstrap(CreateConfig("child"), _rootDir, _logger));

        Assert.Contains("parent theme manifest was not found", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Bootstrap_Should_ThrowConfigException_When_ManifestHasUnknownField()
    {
        var childRoot = Path.Combine(_rootDir, "themes", "child");
        Directory.CreateDirectory(Path.Combine(childRoot, "layouts"));
        File.WriteAllText(Path.Combine(childRoot, "theme.yaml"), """
            name: child
            unknown: should-fail
            """);

        var ex = Assert.Throws<ConfigException>(() => ThemeBootstrapper.Bootstrap(CreateConfig("child"), _rootDir, _logger));

        Assert.Equal(DiagnosticCode.ThemeManifestInvalid, ex.Code);
        Assert.Contains("theme.yaml", ex.Message, StringComparison.Ordinal);
    }

    private static AppConfig CreateConfig(string themeName) => new()
    {
        Site = new SiteConfig { Name = "test", Title = "Test" },
        Content = TestContent.Markdown(),
        Theme = new ThemeConfig { Name = themeName }
    };

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = new();
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { Warnings.Add(message); }
        public void Error(string message) { }
    }
}
