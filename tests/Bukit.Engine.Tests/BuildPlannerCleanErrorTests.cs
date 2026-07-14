using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class BuildPlannerCleanErrorTests
{
    [Fact]
    public void PlanRejectsNamedThemeManifestMissingEngine()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-theme-contract-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var themeRoot = Path.Combine(root, "themes", "site");
            Directory.CreateDirectory(themeRoot);
            File.WriteAllText(Path.Combine(themeRoot, "theme.yaml"), "name: site\nversion: 1.0.0\n");
            var outputDir = Path.Combine(root, "dist");
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, ".bukit-output-marker"), "bukit-output\n");
            var sentinel = Path.Combine(outputDir, "sentinel.txt");
            File.WriteAllText(sentinel, "keep");

            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "T", Language = "en", BaseUrl = "/" },
                Content = TestContent.Markdown() with { Media = new MediaConfig { DownloadToLocal = false } },
                Build = new BuildConfig { Output = "dist", Clean = true },
                Theme = new ThemeConfig { Name = "site" }
            };

            var ex = Assert.Throws<ConfigException>(() =>
                BuildPlanner.Plan(config, root, new ConfigOverrides(), new NoOpLogger()));

            Assert.Equal(DiagnosticCode.ThemeManifestInvalid, ex.Code);
            Assert.Contains("'engine' is missing", ex.Message, StringComparison.Ordinal);
            Assert.True(File.Exists(sentinel));
        }
        finally
        {
            TestCleanup.DeleteDirectory(root, recursive: true);
        }
    }

    [Fact]
    public async Task CleanRefusesDirectoryWithoutMarker_MessageIncludesHowToFix()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-clean-hint-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
            Directory.CreateDirectory(Path.Combine(root, "dist"));
            var sentinel = Path.Combine(root, "dist", "user-file.txt");
            File.WriteAllText(sentinel, "keep");
            File.WriteAllText(Path.Combine(root, "content", "a.md"), """
                ---
                type: page
                title: A
                slug: a
                ---
                # A
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{{ page.title }}");

            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "T", Language = "en", BaseUrl = "/" },
                Content = TestContent.Markdown() with { Media = new MediaConfig { DownloadToLocal = false } },
                Build = new BuildConfig { Output = "dist", Clean = true },
                Theme = new ThemeConfig { Layouts = "layouts" }
            };

            var ex = await Assert.ThrowsAsync<ConfigException>(() =>
                new SiteEngine(new NoOpLogger()).BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None));

            Assert.Equal(DiagnosticCode.BuildOutputNoMarker, ex.Code);
            Assert.Contains("marker", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("How to fix", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("dedicated empty output directory", ex.Message, StringComparison.Ordinal);
            Assert.Contains("successful build creates .bukit-output-marker", ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("--init-marker", ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("build.clean: false", ex.Message, StringComparison.Ordinal);
            Assert.True(File.Exists(sentinel));
        }
        finally
        {
            TestCleanup.DeleteDirectory(root, recursive: true);
        }
    }

    [Fact]
    public async Task CleanRefusesUnsafeDirectory_MessageIncludesHowToFix()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-unsafe-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
            File.WriteAllText(Path.Combine(root, "content", "a.md"), """
                ---
                type: page
                title: A
                slug: a
                ---
                # A
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{{ page.title }}");

            var unsafeDir = Path.Combine(root, ".git");
            Directory.CreateDirectory(unsafeDir);

            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "T", Language = "en", BaseUrl = "/" },
                Content = TestContent.Markdown() with { Media = new MediaConfig { DownloadToLocal = false } },
                Build = new BuildConfig { Output = ".git", Clean = true },
                Theme = new ThemeConfig { Layouts = "layouts" }
            };

            var ex = await Assert.ThrowsAsync<ConfigException>(() =>
                new SiteEngine(new NoOpLogger()).BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None));

            Assert.Contains("unsafe", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("How to fix", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestCleanup.DeleteDirectory(root, recursive: true);
        }
    }

    [Fact]
    public void RecoveryAutoCleanRefusesDirectoryWithoutMarker_MessageIncludesHowToFix()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-recovery-marker-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
            var outputDir = Path.Combine(root, "dist");
            Directory.CreateDirectory(outputDir);
            var sentinel = Path.Combine(outputDir, "user-file.txt");
            File.WriteAllText(sentinel, "keep");
            BuildRecoveryTracker.MarkStarted(outputDir);
            Assert.True(BuildRecoveryTracker.HasIncompleteBuild(outputDir));
            File.WriteAllText(Path.Combine(root, "content", "a.md"), """
                ---
                type: page
                title: A
                slug: a
                ---
                # A
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{{ page.title }}");

            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "T", Language = "en", BaseUrl = "/" },
                Content = TestContent.Markdown() with { Media = new MediaConfig { DownloadToLocal = false } },
                Build = new BuildConfig { Output = "dist", Clean = false },
                Theme = new ThemeConfig { Layouts = "layouts" }
            };

            var logger = new CapturingLogger();
            var exception = Record.Exception(() =>
                BuildPlanner.Plan(config, root, new ConfigOverrides { Clean = false }, logger));
            if (exception is null)
            {
                Assert.Fail(
                    "Expected recovery auto-clean to refuse unmarked output. " +
                    $"warnings={string.Join("|", logger.Warnings)} " +
                    $"entries={string.Join(",", Directory.EnumerateFileSystemEntries(outputDir).Select(Path.GetFileName))}");
            }

            var ex = Assert.IsType<ConfigException>(exception);

            Assert.Equal(DiagnosticCode.BuildOutputNoMarker, ex.Code);
            Assert.Contains("marker", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("How to fix", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(sentinel));
        }
        finally
        {
            TestCleanup.DeleteDirectory(root, recursive: true);
        }
    }

    [Fact]
    public void RecoveryAutoCleanRefusesUnsafeDirectory_MessageIncludesHowToFix()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-recovery-unsafe-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
            var unsafeDir = Path.Combine(root, ".git");
            Directory.CreateDirectory(unsafeDir);
            BuildRecoveryTracker.MarkStarted(unsafeDir);
            var statePath = Path.Combine(unsafeDir, ".bukit-build-state.json");
            Assert.True(BuildRecoveryTracker.HasIncompleteBuild(unsafeDir));
            File.WriteAllText(Path.Combine(root, "content", "a.md"), """
                ---
                type: page
                title: A
                slug: a
                ---
                # A
                """);
            File.WriteAllText(Path.Combine(root, "layouts", "pages", "page.html"), "{{ page.title }}");

            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "T", Language = "en", BaseUrl = "/" },
                Content = TestContent.Markdown() with { Media = new MediaConfig { DownloadToLocal = false } },
                Build = new BuildConfig { Output = ".git", Clean = false },
                Theme = new ThemeConfig { Layouts = "layouts" }
            };

            var logger = new CapturingLogger();
            var exception = Record.Exception(() =>
                BuildPlanner.Plan(config, root, new ConfigOverrides { Clean = false }, logger));
            if (exception is null)
            {
                Assert.Fail(
                    "Expected recovery auto-clean to refuse unsafe output. " +
                    $"warnings={string.Join("|", logger.Warnings)} " +
                    $"entries={string.Join(",", Directory.EnumerateFileSystemEntries(unsafeDir).Select(Path.GetFileName))}");
            }

            var ex = Assert.IsType<ConfigException>(exception);

            Assert.Equal(DiagnosticCode.BuildOutputUnsafe, ex.Code);
            Assert.Contains("unsafe", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("How to fix", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(statePath));
        }
        finally
        {
            TestCleanup.DeleteDirectory(root, recursive: true);
        }
    }

    private sealed class NoOpLogger : Bukit.Shared.ILogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }

    private sealed class CapturingLogger : Bukit.Shared.ILogger
    {
        public List<string> Warnings { get; } = [];
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message) { }
    }
}
