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

public sealed class PageRenderDispatcherMetricsTests
{
    [Fact]
    public async Task RenderPagesAsync_CollectsContentHashBodyLoadAndRenderMetrics()
    {
        var item = new ContentItem(
            Id: "id-1",
            Title: "Hello",
            Slug: "hello",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Fields: null,
            BodyKey: "body-1");

        var route = new RouteInfo("/pages/hello/", "pages/hello/index.html", "pages/page.html");
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);

        var result = await PageRenderDispatcher.RenderPagesAsync(
            new List<(ContentItem Item, RouteInfo Route)> { (item, route) },
            new DictionaryContentBodyStore(new Dictionary<string, ContentBody>(StringComparer.OrdinalIgnoreCase)
            {
                ["body-1"] = new("<p>lazy body</p>")
            }),
            new CaptureRenderer(),
            new SiteModel
            {
                Name = "site",
                Title = "site",
                BaseUrl = "/",
                Language = "zh-CN"
            },
            outputDir,
            templateHash: "template-hash",
            renderDependencyHash: string.Empty,
            incrementalEnabled: false,
            manifest: new BuildManifest(),
            manifestEntries: null,
            currentKeys: new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase),
            maxDegreeOfParallelism: 1,
            logger: new ConsoleLogger(LogLevel.Error),
            cancellationToken: CancellationToken.None);

        Assert.Equal(1, result.StageMetrics.Counts["metadataHash"]);
        Assert.Equal(1, result.StageMetrics.Counts["bodyLoad"]);
        Assert.Equal(1, result.StageMetrics.Counts["pageRender"]);
        Assert.True(result.StageMetrics.DurationsMs["metadataHash"] >= 0);
        Assert.True(result.StageMetrics.DurationsMs["bodyLoad"] >= 0);
        Assert.True(result.StageMetrics.DurationsMs["pageRender"] >= 0);
    }

    private sealed class CaptureRenderer : ITemplateRenderer
    {
        public string RenderPage(string templatePath, PageModel model) => model.Page.Content;

        public string RenderList(string templatePath, ListPageModel model) => string.Join('\n', model.Pages.Select(x => x.Title));
    }
}
