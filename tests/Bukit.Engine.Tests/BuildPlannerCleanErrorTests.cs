using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class BuildPlannerCleanErrorTests
{
    [Fact]
    public async Task CleanRefusesDirectoryWithoutMarker_MessageIncludesHowToFix()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-clean-hint-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "content"));
            Directory.CreateDirectory(Path.Combine(root, "layouts", "pages"));
            Directory.CreateDirectory(Path.Combine(root, "dist"));
            File.WriteAllText(Path.Combine(root, "dist", "user-file.txt"), "keep");
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
                Content = new ContentConfig { Provider = "markdown", Markdown = new MarkdownConfig { Dir = "content" }, Media = new MediaConfig { DownloadToLocal = false } },
                Build = new BuildConfig { Output = "dist", Clean = true },
                Theme = new ThemeConfig { Layouts = "layouts" }
            };

            var ex = await Assert.ThrowsAsync<ConfigException>(() =>
                new SiteEngine(new NoOpLogger()).BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None));

            Assert.Contains("marker", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("How to fix", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
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
                Content = new ContentConfig { Provider = "markdown", Markdown = new MarkdownConfig { Dir = "content" }, Media = new MediaConfig { DownloadToLocal = false } },
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
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private sealed class NoOpLogger : Bukit.Shared.ILogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
