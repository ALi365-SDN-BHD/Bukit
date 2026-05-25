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
