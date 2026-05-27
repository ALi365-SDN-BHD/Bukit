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

public sealed class RenderEntryDispatchTests
{
    [Fact]
    public async Task DispatchAsync_RendersPageEntry()
    {
        var item = Item("hello", "hello", null);
        var route = new RouteInfo("/hello/", "hello/index.html", "pages/page.html");
        var entries = new[] { RenderEntry.ForPage(item, route) };
        var outputDir = CreateOutputDir();
        var renderer = new CaptureRenderer();
        var manifest = new BuildManifest();

        var result = await PageRenderDispatcher.DispatchAsync(
            entries,
            EmptyContentBodyStore.Instance,
            renderer,
            new SiteModel { Name = "s", Title = "s", BaseUrl = "/", Language = "en" },
            outputDir,
            string.Empty,
            string.Empty,
            false,
            manifest,
            null,
            new ConcurrentDictionary<string, byte>(),
            1,
            new ConsoleLogger(LogLevel.Error),
            CancellationToken.None);

        Assert.Equal(1, result.RenderedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Contains("pages/page.html", renderer.PageRenderedTemplates.Keys);
        Assert.True(File.Exists(Path.Combine(outputDir, "hello", "index.html")));
    }

    [Fact]
    public async Task DispatchAsync_RendersListEntry()
    {
        var item = Item("a", "a", null);
        var pageRoute = new RouteInfo("/a/", "a/index.html", "pages/page.html");
        var source = new[] { (item, pageRoute) };
        var listRoute = new RouteInfo("/", "index.html", "pages/index.html");
        var entries = new[] { RenderEntry.ForList(listRoute, source, includeContent: false) };
        var outputDir = CreateOutputDir();
        var renderer = new CaptureRenderer();
        var manifest = new BuildManifest();

        var result = await PageRenderDispatcher.DispatchAsync(
            entries,
            EmptyContentBodyStore.Instance,
            renderer,
            new SiteModel { Name = "s", Title = "s", BaseUrl = "/", Language = "en" },
            outputDir,
            string.Empty,
            string.Empty,
            false,
            manifest,
            null,
            new ConcurrentDictionary<string, byte>(),
            1,
            new ConsoleLogger(LogLevel.Error),
            CancellationToken.None);

        Assert.Equal(1, result.RenderedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Contains("pages/index.html", renderer.ListRenderedTemplates.Keys);
        Assert.True(File.Exists(Path.Combine(outputDir, "index.html")));
    }

    [Fact]
    public async Task DispatchAsync_RendersStaticEntry()
    {
        var staticDir = CreateStaticDirWithHtml("about.html", "<main>About</main>");
        var entries = RenderEntry.ForStaticDir(staticDir, "pages/static.html", _ => { }, false);
        var outputDir = CreateOutputDir();
        var renderer = new CaptureRenderer();
        var manifest = new BuildManifest();

        var result = await PageRenderDispatcher.DispatchAsync(
            entries,
            EmptyContentBodyStore.Instance,
            renderer,
            new SiteModel { Name = "s", Title = "Test", BaseUrl = "/", Language = "en" },
            outputDir,
            string.Empty,
            string.Empty,
            false,
            manifest,
            null,
            new ConcurrentDictionary<string, byte>(),
            1,
            new ConsoleLogger(LogLevel.Error),
            CancellationToken.None);

        Assert.Equal(1, result.RenderedCount);
        Assert.True(File.Exists(Path.Combine(outputDir, "about", "index.html")));
    }

    [Fact]
    public async Task DispatchAsync_MixedPageAndList_AllRendered()
    {
        var item = Item("p", "p", null);
        var pageRoute = new RouteInfo("/p/", "p/index.html", "pages/page.html");
        var source = new[] { (item, pageRoute) };
        var listRoute = new RouteInfo("/", "index.html", "pages/index.html");
        var entries = new[]
        {
            RenderEntry.ForPage(item, pageRoute),
            RenderEntry.ForList(listRoute, source, includeContent: false)
        };
        var outputDir = CreateOutputDir();
        var renderer = new CaptureRenderer();
        var manifest = new BuildManifest();

        var result = await PageRenderDispatcher.DispatchAsync(
            entries,
            EmptyContentBodyStore.Instance,
            renderer,
            new SiteModel { Name = "s", Title = "s", BaseUrl = "/", Language = "en" },
            outputDir,
            string.Empty,
            string.Empty,
            false,
            manifest,
            null,
            new ConcurrentDictionary<string, byte>(),
            1,
            new ConsoleLogger(LogLevel.Error),
            CancellationToken.None);

        Assert.Equal(2, result.RenderedCount);
        Assert.True(File.Exists(Path.Combine(outputDir, "p", "index.html")));
        Assert.True(File.Exists(Path.Combine(outputDir, "index.html")));
    }

    [Fact]
    public async Task DispatchAsync_AllEntries_CollectStageMetrics()
    {
        var item = Item("m", "m", null);
        var pageRoute = new RouteInfo("/m/", "m/index.html", "pages/page.html");
        var entries = new[] { RenderEntry.ForPage(item, pageRoute) };
        var outputDir = CreateOutputDir();
        var renderer = new CaptureRenderer();
        var manifest = new BuildManifest();

        var result = await PageRenderDispatcher.DispatchAsync(
            entries,
            EmptyContentBodyStore.Instance,
            renderer,
            new SiteModel { Name = "s", Title = "s", BaseUrl = "/", Language = "en" },
            outputDir,
            string.Empty,
            string.Empty,
            false,
            manifest,
            null,
            new ConcurrentDictionary<string, byte>(),
            1,
            new ConsoleLogger(LogLevel.Error),
            CancellationToken.None);

        Assert.True(result.StageMetrics.Counts.ContainsKey("pageRender"));
    }

    [Fact]
    public async Task DispatchAsync_RenderEntryKind_AllThreeDefined()
    {
        Assert.True(Enum.IsDefined(typeof(RenderEntryKind), RenderEntryKind.Page));
        Assert.True(Enum.IsDefined(typeof(RenderEntryKind), RenderEntryKind.List));
        Assert.True(Enum.IsDefined(typeof(RenderEntryKind), RenderEntryKind.Static));
    }

    [Fact]
    public async Task DispatchAsync_ForPage_BuildsCorrectEntry()
    {
        var item = Item("x", "x", null);
        var route = new RouteInfo("/x/", "x/index.html", "pages/page.html");
        var entry = RenderEntry.ForPage(item, route);

        Assert.Equal(RenderEntryKind.Page, entry.Kind);
        Assert.Same(item, entry.Item);
        Assert.Same(route, entry.Route);
        Assert.Null(entry.RawContent);
        Assert.Null(entry.SourceItems);
    }

    [Fact]
    public async Task DispatchAsync_ForList_BuildsCorrectEntry()
    {
        var item = Item("x", "x", null);
        var route = new RouteInfo("/x/", "x/index.html", "pages/page.html");
        var source = new[] { (item, route) };
        var listRoute = new RouteInfo("/", "index.html", "pages/index.html");
        var entry = RenderEntry.ForList(listRoute, source, includeContent: true);

        Assert.Equal(RenderEntryKind.List, entry.Kind);
        Assert.Equal(listRoute, entry.Route);
        Assert.True(entry.IncludeContent);
        Assert.NotNull(entry.SourceItems);
        Assert.NotEmpty(entry.SourceItems);
    }

    private static ContentItem Item(string id, string slug, IReadOnlyDictionary<string, object>? meta) =>
        new(id, id, slug, DateTimeOffset.UnixEpoch, $"<p>{id}</p>", meta ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase));

    private static string CreateOutputDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-render-entry-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string CreateStaticDirWithHtml(string relativePath, string content)
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-static-entry-tests", Guid.NewGuid().ToString("N"));
        var staticDir = Path.Combine(root, "static");
        var filePath = Path.Combine(staticDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, content);
        return staticDir;
    }

    private sealed class CaptureRenderer : ITemplateRenderer
    {
        public Dictionary<string, string> PageRenderedTemplates { get; } = new();
        public Dictionary<string, string> ListRenderedTemplates { get; } = new();

        public string RenderPage(string templateRelativePath, PageModel model)
        {
            PageRenderedTemplates[templateRelativePath] = model.Page.Content;
            return model.Page.Content;
        }

        public string RenderList(string templateRelativePath, ListPageModel model)
        {
            ListRenderedTemplates[templateRelativePath] = string.Join('\n', model.Pages.Select(p => p.Title));
            return ListRenderedTemplates[templateRelativePath];
        }
    }
}
