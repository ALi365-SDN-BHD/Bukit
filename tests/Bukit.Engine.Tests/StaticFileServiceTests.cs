using System.Collections.Concurrent;
using Bukit.Rendering;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class StaticFileServiceTests : IDisposable
{
    private readonly List<string> _tempRoots = new();

    [Theory]
    [InlineData("index.html", "/")]
    [InlineData("about.html", "/about/")]
    [InlineData("about/team.html", "/about/team/")]
    [InlineData("docs/index.html", "/docs/")]
    public void RenderStaticFiles_StaticHtml_GeneratesExpectedUrl(string relativePath, string expectedUrl)
    {
        var root = CreateTempRoot();
        var staticDir = Path.Combine(root, "static");
        var outputDir = Path.Combine(root, "dist");
        var sourcePath = Path.Combine(staticDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "<main>Static</main>");
        var renderer = new CaptureRenderer();
        var currentKeys = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        StaticFileService.RenderStaticFiles(
            staticDir,
            outputDir,
            renderer,
            CreateSiteModel(),
            "pages/static.html",
            "/",
            currentKeys,
            CancellationToken.None);

        Assert.Equal(expectedUrl, renderer.PageUrls.Single());
    }

    [Fact]
    public void RenderStaticFiles_EmptyHtmlFileName_SkipsFileAndDoesNotPolluteCurrentKeys()
    {
        var root = CreateTempRoot();
        var staticDir = Path.Combine(root, "static");
        var outputDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(staticDir);
        File.WriteAllText(Path.Combine(staticDir, ".html"), "<main>Invalid</main>");
        var renderer = new CaptureRenderer();
        var currentKeys = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        StaticFileService.RenderStaticFiles(
            staticDir,
            outputDir,
            renderer,
            CreateSiteModel(),
            "pages/static.html",
            "/",
            currentKeys,
            CancellationToken.None,
            warnings.Add);

        Assert.Empty(renderer.PageUrls);
        Assert.DoesNotContain(".html", currentKeys.Keys);
        Assert.Contains(warnings, warning => warning.Contains(".html", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RenderStaticFiles_PublishDotFilesFalse_SkipsDotPrefixedFiles()
    {
        var root = CreateTempRoot();
        var staticDir = Path.Combine(root, "static");
        var outputDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(staticDir);
        File.WriteAllText(Path.Combine(staticDir, ".hidden.html"), "<main>Hidden</main>");
        File.WriteAllText(Path.Combine(staticDir, "visible.html"), "<main>Visible</main>");
        File.WriteAllText(Path.Combine(staticDir, ".secret.txt"), "secret-txt");
        File.WriteAllText(Path.Combine(staticDir, "public.txt"), "public-txt");
        var renderer = new CaptureRenderer();
        var currentKeys = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        StaticFileService.RenderStaticFiles(
            staticDir, outputDir, renderer, CreateSiteModel(),
            "pages/static.html", "/", currentKeys, CancellationToken.None,
            publishDotFiles: false);

        Assert.Single(renderer.PageUrls);
        Assert.Equal("/visible/", renderer.PageUrls[0]);
        Assert.False(File.Exists(Path.Combine(outputDir, ".secret.txt")));
        Assert.True(File.Exists(Path.Combine(outputDir, "public.txt")));
    }

    [Fact]
    public void RenderStaticFiles_PublishDotFilesTrue_SensitiveDotfilesStillDenied()
    {
        var root = CreateTempRoot();
        var staticDir = Path.Combine(root, "static");
        var nestedDir = Path.Combine(staticDir, ".git");
        var outputDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(nestedDir);
        File.WriteAllText(Path.Combine(staticDir, ".env"), "secret");
        File.WriteAllText(Path.Combine(staticDir, ".env.local"), "local-secret");
        File.WriteAllText(Path.Combine(staticDir, "server.pem"), "private-key");
        File.WriteAllText(Path.Combine(staticDir, ".npmrc"), "npmrc-data");
        File.WriteAllText(Path.Combine(staticDir, ".htaccess"), "htaccess-data");
        File.WriteAllText(Path.Combine(staticDir, "regular.txt"), "regular");
        File.WriteAllText(Path.Combine(nestedDir, "config"), "git-config");
        var renderer = new CaptureRenderer();
        var currentKeys = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        StaticFileService.RenderStaticFiles(
            staticDir, outputDir, renderer, CreateSiteModel(),
            "pages/static.html", "/", currentKeys, CancellationToken.None,
            warnings.Add, publishDotFiles: true);

        Assert.False(File.Exists(Path.Combine(outputDir, ".env")));
        Assert.False(File.Exists(Path.Combine(outputDir, ".env.local")));
        Assert.False(File.Exists(Path.Combine(outputDir, "server.pem")));
        Assert.False(File.Exists(Path.Combine(outputDir, ".npmrc")));
        Assert.False(File.Exists(Path.Combine(outputDir, ".git")));
        Assert.False(File.Exists(Path.Combine(outputDir, ".git", "config")));
        Assert.True(File.Exists(Path.Combine(outputDir, ".htaccess")));
        Assert.Equal("htaccess-data", File.ReadAllText(Path.Combine(outputDir, ".htaccess")));
        Assert.True(File.Exists(Path.Combine(outputDir, "regular.txt")));
    }

    [Fact]
    public void RenderStaticFiles_PublishDotFilesTrue_WellKnownAllowed()
    {
        var root = CreateTempRoot();
        var staticDir = Path.Combine(root, "static");
        var wellKnownDir = Path.Combine(staticDir, ".well-known");
        var outputDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(wellKnownDir);
        File.WriteAllText(Path.Combine(wellKnownDir, "security.txt"), "security-content");
        File.WriteAllText(Path.Combine(staticDir, "index.html"), "<main>Home</main>");
        var renderer = new CaptureRenderer();
        var currentKeys = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        StaticFileService.RenderStaticFiles(
            staticDir, outputDir, renderer, CreateSiteModel(),
            "pages/static.html", "/", currentKeys, CancellationToken.None,
            publishDotFiles: true);

        Assert.True(File.Exists(Path.Combine(outputDir, ".well-known", "security.txt")));
        Assert.Equal("security-content", File.ReadAllText(Path.Combine(outputDir, ".well-known", "security.txt")));
    }

    public void Dispose()
    {
        foreach (var root in _tempRoots)
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        _tempRoots.Add(root);
        return root;
    }

    private static SiteModel CreateSiteModel() => new()
    {
        Name = "site",
        Title = "Site",
        BaseUrl = "/",
        Language = "en"
    };

    private sealed class CaptureRenderer : ITemplateRenderer
    {
        public List<string> PageUrls { get; } = new();

        public string RenderPage(string templateRelativePath, PageModel model)
        {
            PageUrls.Add(model.Page.Url);
            return model.Page.Content;
        }

        public string RenderList(string templateRelativePath, ListPageModel model) => string.Empty;
    }
}
