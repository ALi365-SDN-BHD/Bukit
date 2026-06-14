using Bukit.Cli.Commands;
using Bukit.Cli.Commands.Dev;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class DevCommandTests
{
    [Fact]
    public void DevPathGuard_OnNonWindows_RejectsCaseDifferentSiblingEscape()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "bukit-dev-path-" + Guid.NewGuid().ToString("N"), "site");
        Directory.CreateDirectory(root);
        try
        {
            var result = DevPathGuard.TryResolveWithinRoot(root, "../SITE/index.html");
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(root)!, recursive: true);
        }
    }

    [Fact]
    public void PathUtils_IsSubPathOf_DoesNotMatchPrefixSibling()
    {
        var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "repo", "themes", "foo"));
        var sibling = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "repo", "themes", "foo-extra", "layout.html"));

        Assert.False(PathUtils.IsSubPathOf(sibling, parent));
    }

    [Fact]
    public void ResolveWatchDirs_DoesNotTreatPrefixSiblingAsThemeChild()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-dev-watch-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "themes", "foo"));
            Directory.CreateDirectory(Path.Combine(root, "themes", "foo-extra"));

            var config = MinimalConfig() with
            {
                Theme = new ThemeConfig
                {
                    Name = "foo",
                    Layouts = Path.Combine("themes", "foo-extra"),
                    Assets = "missing-assets",
                    Static = "missing-static"
                }
            };

            var dirs = DevCommand.ResolveWatchDirs(root, config);

            Assert.Contains(Path.GetFullPath(Path.Combine(root, "themes", "foo")), dirs);
            Assert.Contains(Path.GetFullPath(Path.Combine(root, "themes", "foo-extra")), dirs);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void DevFileWatcher_ShouldIgnore_DynamicOutputCacheAndCommonGeneratedDirs()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-dev-ignore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var output = Path.Combine(root, "public");
            var cache = Path.Combine(root, ".cache");
            var excluded = DevCommand.ResolveExcludedWatchDirs(root, output, cache);
            using var watcher = new DevFileWatcher(
                Array.Empty<string>(),
                root,
                new TestLogger(),
                static (_, _) => Task.CompletedTask,
                excluded);

            Assert.True(watcher.ShouldIgnore(Path.Combine(output, "index.html"), "index.html"));
            Assert.True(watcher.ShouldIgnore(Path.Combine(cache, "manifest.json"), "manifest.json"));
            Assert.True(watcher.ShouldIgnore(Path.Combine(root, ".git", "HEAD"), "HEAD"));
            Assert.True(watcher.ShouldIgnore(Path.Combine(root, "node_modules", "pkg", "index.js"), "index.js"));
            Assert.True(watcher.ShouldIgnore(Path.Combine(root, ".bukit", "state.json"), "state.json"));
            Assert.True(watcher.ShouldIgnore(Path.Combine(root, "bin", "Debug", "file.dll"), "file.dll"));
            Assert.True(watcher.ShouldIgnore(Path.Combine(root, "obj", "project.assets.json"), "project.assets.json"));
            Assert.True(watcher.ShouldIgnore(Path.Combine(root, "content", ".draft.md"), ".draft.md"));

            Assert.False(watcher.ShouldIgnore(Path.Combine(root, "content", "page.md"), "page.md"));
            Assert.False(watcher.ShouldIgnore(Path.Combine(root, "dist", "index.html"), "index.html"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void WebSocketPolicy_RequiresHostAndOriginSameOrigin()
    {
        var policy = new DevWebSocketAccessPolicy("localhost", 35729, allowLan: false);

        Assert.True(policy.IsAllowed("localhost:35729", "http://localhost:35729", out _));
        Assert.False(policy.IsAllowed("localhost:35729", null, out var missingOrigin));
        Assert.Contains("Origin", missingOrigin, StringComparison.Ordinal);
        Assert.False(policy.IsAllowed("localhost:35729", "http://example.com:35729", out var crossOrigin));
        Assert.Contains("Origin host", crossOrigin, StringComparison.Ordinal);
        Assert.False(policy.IsAllowed("localhost:3000", "http://localhost:3000", out var wrongPort));
        Assert.Contains("Host port", wrongPort, StringComparison.Ordinal);
    }

    [Fact]
    public void WebSocketPolicy_RejectsLanHostUnlessAllowLan()
    {
        var loopbackOnly = new DevWebSocketAccessPolicy("0.0.0.0", 35729, allowLan: false);
        Assert.False(loopbackOnly.IsAllowed("192.168.1.10:35729", "http://192.168.1.10:35729", out var reason));
        Assert.Contains("loopback", reason, StringComparison.Ordinal);

        var allowLan = new DevWebSocketAccessPolicy("0.0.0.0", 35729, allowLan: true);
        Assert.True(allowLan.IsAllowed("192.168.1.10:35729", "http://192.168.1.10:35729", out _));
        Assert.False(allowLan.IsAllowed("192.168.1.10:35729", "http://192.168.1.11:35729", out _));
    }

    [Fact]
    public void ExtractOptions_RecognizesAllowLanAndPublicAliases()
    {
        var allowLan = DevCommand.ExtractOptions(new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--allow-lan"] = "true"
            },
            Array.Empty<string>()));

        var publicAlias = DevCommand.ExtractOptions(new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--public"] = "true"
            },
            Array.Empty<string>()));

        Assert.True(allowLan.allowLan);
        Assert.True(publicAlias.allowLan);
    }

    [Fact]
    public void ExtractOptions_RecognizesLivereloadPort()
    {
        var options = DevCommand.ExtractOptions(new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--livereload-port"] = "42000"
            },
            Array.Empty<string>()));

        Assert.Equal(42000, options.livereloadPort);
    }

    [Fact]
    public void CliRegistry_DevCommandIncludesLanExposureFlags()
    {
        var registry = BukitCliSpecs.CreateRegistry();
        var dev = registry.Commands.Single(c => c.Name == "dev");
        var optionNames = (dev.Options ?? Array.Empty<Cli.Shared.Cli.Metadata.CliOptionSpec>())
            .Select(o => o.Name)
            .ToArray();

        Assert.Contains("--allow-lan", optionNames);
        Assert.Contains("--public", optionNames);
        Assert.Contains("--livereload-port", optionNames);
    }

    [Fact]
    public void ResolveDisableAnalytics_UsesLoadedConfigAnalytics()
    {
        Assert.True(DevCommand.ResolveDisableAnalytics(new AnalyticsConfig
        {
            DisableInPreview = true,
            GoogleAnalyticsId = "G-ABCDE123"
        }));

        Assert.False(DevCommand.ResolveDisableAnalytics(new AnalyticsConfig
        {
            DisableInPreview = true
        }));
    }

    [Fact]
    public void InjectLivereload_UsesLocationProtocolAndHostnameForWebSocketUrl()
    {
        var html = DevRequestHandler.InjectLivereload("<html><head></head><body></body></html>");

        Assert.Contains("location.protocol === 'https:' ? 'wss://' : 'ws://'", html, StringComparison.Ordinal);
        Assert.Contains("location.hostname", html, StringComparison.Ordinal);
        Assert.DoesNotContain(".split(':')", html, StringComparison.Ordinal);
        Assert.DoesNotContain("'ws://'+", html, StringComparison.Ordinal);
    }

    [Fact]
    public void InjectLivereload_UsesConfiguredPortWhenProvided()
    {
        var html = DevRequestHandler.InjectLivereload("<html><head></head><body></body></html>", 42000);

        Assert.Contains("const configuredPort = \"42000\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".ico", "image/x-icon")]
    [InlineData(".avif", "image/avif")]
    [InlineData(".webmanifest", "application/manifest+json; charset=utf-8")]
    [InlineData(".woff", "font/woff")]
    [InlineData(".woff2", "font/woff2")]
    [InlineData(".map", "application/json; charset=utf-8")]
    [InlineData(".pdf", "application/pdf")]
    public void ResolveMimeType_CoversCommonDevAssets(string extension, string expected)
    {
        Assert.Equal(expected, ResolveMimeType(extension));
    }

    private static AppConfig MinimalConfig()
        => new()
        {
            Site = new SiteConfig { Name = "test", Title = "Test" },
            Content = new ContentConfig
            {
                Sources =
                [
                    new ContentSourceConfig
                    {
                        Type = "markdown",
                        Name = "page",
                        Collection = "page",
                        Markdown = new MarkdownConfig()
                    }
                ]
            }
        };

    private static string ResolveMimeType(string extension)
    {
        var method = typeof(DevRequestHandler).GetMethod(
            "ResolveMimeType",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (string)method.Invoke(null, [extension])!;
    }

    private sealed class TestLogger : ILogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
