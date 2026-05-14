using System.Text.Json;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SearchSnippetCapabilityTests : IDisposable
{
    private readonly string _rootDir;
    private readonly string _layoutsDir;
    private readonly string _outputDir;

    public SearchSnippetCapabilityTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-search-capability-" + Guid.NewGuid().ToString("N"));
        _layoutsDir = Path.Combine(_rootDir, "layouts");
        _outputDir = Path.Combine(_rootDir, "dist");
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "pages"));
        Directory.CreateDirectory(_outputDir);
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "search.html"), "{{ page.title }}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public void AfterBuild_WritesSnippet_WhenCapabilityDeclared()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "bukit.templates.yaml"), """
                                                                        templates:
                                                                          pages/search.html:
                                                                            capabilities:
                                                                              supports_search_snippets: true
                                                                        """);

        var context = CreateContext();
        new SearchIndexPlugin().AfterBuild(context);

        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(_outputDir, "search.json")));
        var item = doc.RootElement[0];
        Assert.True(item.TryGetProperty("snippet", out var snippet));
        Assert.Equal("Summary text", snippet.GetString());
    }

    private BuildContext CreateContext()
    {
        var item = new ContentItem(
            Id: "post-1",
            Title: "Post",
            Slug: "post",
            PublishAt: new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero),
            ContentHtml: "<p>Body text</p>",
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "post",
                ["summary"] = "Summary text"
            },
            Fields: null);

        var route = new RouteInfo("/blog/post/", Path.Combine("blog", "post", "index.html"), "pages/post.html");
        return new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "test" },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = _rootDir,
            OutputDir = _outputDir,
            BaseUrl = "/",
            LayoutsDir = _layoutsDir,
            Routed = new List<(ContentItem Item, RouteInfo Route)>
            {
                (item, route)
            },
            SeoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
            {
                [BuildPathUtils.NormalizeRelPath(route.OutputPath)] = new(
                    route,
                    "https://example.com/blog/post/",
                    Robots: null,
                    Indexable: true,
                    item.PublishAt,
                    item.Id,
                    "post")
            },
            Logger = new ConsoleLogger(LogLevel.Error)
        };
    }
}
