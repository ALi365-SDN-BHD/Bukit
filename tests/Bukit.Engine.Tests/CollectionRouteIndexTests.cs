using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class CollectionRouteIndexTests
{
    [Fact]
    public void GetOrBuild_CachesSortedRoutesPerBuildContext()
    {
        var routed = new List<(ContentDocument Item, RouteInfo Route)>
        {
            (CreateItem("page-1", "page", 1), new RouteInfo("/pages/page-1/", "pages/page-1/index.html", "pages/page.html")),
            (CreateItem("post-1", "post", 3), new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html")),
            (CreateItem("post-2", "post", 2), new RouteInfo("/blog/post-2/", "blog/post-2/index.html", "pages/post.html"))
        };

        var context = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "test" },
                Content = TestContent.Markdown()
            },
            RootDir = Path.GetTempPath(),
            OutputDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N")),
            BaseUrl = "/",
            LayoutsDir = Path.Combine(Path.GetTempPath(), "bukit-layouts"),
            RoutedDocuments = routed.ToRoutedDocuments(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var first = CollectionRouteIndex.GetOrBuild(context);
        var second = CollectionRouteIndex.GetOrBuild(context);

        Assert.Same(first, second);
        Assert.Equal(new[] { "post-1", "post-2", "page-1" }, first.AllOrdered.Select(x => x.Document.Id).ToArray());
        Assert.Equal(new[] { "post-1", "post-2" }, first.GetByCollection("post").Select(x => x.Document.Id).ToArray());
        Assert.Same(first.GetByCollection("post"), second.GetByCollection("post"));
    }

    [Fact]
    public void Create_GroupsOnlyByExplicitCollectionAndExcludesCollectionlessDocuments()
    {
        var news = CreateItem("news-1", "article", "news", 2);
        var module = CreateItem("module-1", "module", string.Empty, 3);
        var articleCollection = CreateItem("article-1", "article", "article", 1);
        var routed = new[]
        {
            new RoutedContentDocument(news, new RouteInfo("/news/news-1/", "news/news-1/index.html", "news.html")),
            new RoutedContentDocument(module, new RouteInfo("/modules/module-1/", "modules/module-1/index.html", "module.html")),
            new RoutedContentDocument(articleCollection, new RouteInfo("/articles/article-1/", "articles/article-1/index.html", "article.html"))
        };

        var index = CollectionRouteIndex.Create(routed);

        Assert.Equal(new[] { "news-1", "article-1" }, index.AllOrdered.Select(item => item.Document.Id));
        Assert.Equal(new[] { "news-1" }, index.GetByCollection("news").Select(item => item.Document.Id));
        Assert.Equal(new[] { "article-1" }, index.GetByCollection("article").Select(item => item.Document.Id));
        Assert.Empty(index.GetByCollection("module"));
    }

    private static ContentDocument CreateItem(string id, string collection, int day)
        => CreateItem(id, collection, collection, day);

    private static ContentDocument CreateItem(string id, string type, string collection, int day)
    {
        return ContentDocument.Create(
            id: id,
            title: id,
            slug: id,
            publishAt: new DateTimeOffset(2024, 1, day, 0, 0, 0, TimeSpan.Zero),
            contentHtml: $"<p>{id}</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = type,
                ["collection"] = collection
            }));
    }
}
