using System.Collections.Concurrent;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class HtmlTransformPipelineTests
{
    [Fact]
    public void Transform_AppliesCoreBeforePluginTransforms()
    {
        var pipeline = new HtmlTransformPipeline([
            new AppendingTransform("seo"),
            new AppendingTransform("analytics")
        ]);
        var context = new HtmlTransformContext(
            "/post/", "post/index.html", HtmlDocumentKind.Content,
            Bukit.Config.BuildExecutionMode.Production, new ConsoleLogger(LogLevel.Error));

        var result = pipeline.Transform(context, "html");

        Assert.Equal("html|seo|analytics", result);
    }

    [Fact]
    public async Task DispatchAsync_AppliesPipelineToContentListAndStaticWithAccurateContext()
    {
        var outputDir = CreateOutputDir();
        var staticDir = Path.Combine(outputDir, "input");
        Directory.CreateDirectory(staticDir);
        await File.WriteAllTextAsync(Path.Combine(staticDir, "about.html"), "static");
        var item = ContentDocument.Create("post", "Post", "post", DateTimeOffset.UnixEpoch, "content");
        var pageRoute = new RouteInfo("/post/", "post/index.html", "page.html");
        var listRoute = new RouteInfo("/", "index.html", "list.html");
        var entries = new List<RenderEntry>
        {
            RenderEntry.ForPage(item, pageRoute),
            RenderEntry.ForList(listRoute, [new RoutedContentDocument(item, pageRoute)], false)
        };
        entries.AddRange(RenderEntry.ForStaticDir(staticDir, "static.html", _ => { }, false));
        var transform = new CapturingTransform();

        await PageRenderDispatcher.DispatchAsync(
            entries,
            EmptyContentBodyStore.Instance,
            new HtmlRenderer(),
            new SiteModel { Name = "s", Title = "s", BaseUrl = "/", Language = "en" },
            outputDir,
            string.Empty,
            string.Empty,
            false,
            new BuildManifest(),
            null,
            new ConcurrentDictionary<string, byte>(),
            1,
            new ConsoleLogger(LogLevel.Error),
            CancellationToken.None,
            htmlTransformPipeline: new HtmlTransformPipeline([transform]));

        Assert.Contains(transform.Contexts, x =>
            x.DocumentKind == HtmlDocumentKind.Content && x.RouteUrl == "/post/" && x.OutputPath == "post/index.html");
        Assert.Contains(transform.Contexts, x =>
            x.DocumentKind == HtmlDocumentKind.List && x.RouteUrl == "/" && x.OutputPath == "index.html");
        Assert.Contains(transform.Contexts, x =>
            x.DocumentKind == HtmlDocumentKind.Static && x.RouteUrl == "/about/" && x.OutputPath == "about/index.html");
        Assert.Contains("|content", await File.ReadAllTextAsync(Path.Combine(outputDir, "post", "index.html")));
        Assert.Contains("|list", await File.ReadAllTextAsync(Path.Combine(outputDir, "index.html")));
        Assert.Contains("|static", await File.ReadAllTextAsync(Path.Combine(outputDir, "about", "index.html")));
    }

    [Fact]
    public async Task SpecialListRenderer_AppliesTheSameListTransformBeforeWrite()
    {
        var outputDir = CreateOutputDir();
        var route = new RouteInfo("/archive/", "archive/index.html", "list.html");
        var item = ContentDocument.Create("post", "Post", "post", DateTimeOffset.UnixEpoch, "content");
        var itemRoute = new RouteInfo("/post/", "post/index.html", "page.html");
        var transform = new CapturingTransform();
        var logger = new ConsoleLogger(LogLevel.Error);

        await SpecialListRenderer.RenderSpecialListAlwaysAsync(
            route,
            [new RoutedContentDocument(item, itemRoute)],
            EmptyContentBodyStore.Instance,
            new HtmlRenderer(),
            new SiteModel { Name = "s", Title = "s", BaseUrl = "/", Language = "en" },
            outputDir,
            new ConcurrentDictionary<string, SemaphoreSlim>(),
            1,
            1,
            false,
            null,
            null,
            CancellationToken.None,
            logger,
            null,
            null,
            new HtmlTransformPipeline([transform]));

        var context = Assert.Single(transform.Contexts);
        Assert.Equal(HtmlDocumentKind.List, context.DocumentKind);
        Assert.Equal("/archive/", context.RouteUrl);
        Assert.Equal("archive/index.html", context.OutputPath);
        Assert.Contains("|list", await File.ReadAllTextAsync(Path.Combine(outputDir, "archive", "index.html")));
    }

    private static string CreateOutputDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "bukit-html-pipeline-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class AppendingTransform(string name) : IHtmlTransform
    {
        public string Name => name;
        public string Transform(HtmlTransformContext context, string html) => $"{html}|{name}";
    }

    private sealed class CapturingTransform : IHtmlTransform
    {
        public string Name => "capture";
        public ConcurrentBag<HtmlTransformContext> Contexts { get; } = [];

        public string Transform(HtmlTransformContext context, string html)
        {
            Contexts.Add(context);
            return $"{html}|{context.DocumentKind.ToString().ToLowerInvariant()}";
        }
    }

    private sealed class HtmlRenderer : ITemplateRenderer
    {
        public string RenderPage(string templateRelativePath, PageModel model)
            => $"<html><head></head><body>{model.Page.Content}</body></html>";

        public string RenderList(string templateRelativePath, ListPageModel model)
            => "<html><head></head><body>list</body></html>";
    }
}
