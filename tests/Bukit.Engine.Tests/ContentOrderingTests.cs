using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Plugins.BuiltIn;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ContentOrderingTests
{
    private static readonly DateTimeOffset SameDay = new(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null, "desc", false)]
    [InlineData("  ", "desc", false)]
    [InlineData("ID", "desc", true)]
    [InlineData("ID", null, true)]
    public void Resolve_MapsConfig(string? field, string? order, bool enabled)
    {
        var result = ContentOrdering.Resolve(new ContentConfig { OrderingTieBreakField = field, OrderingTieBreakOrder = order! });

        Assert.Equal(enabled, result is not null);
        if (result is not null)
        {
            Assert.Equal(order == "desc" ? -1 : 1, result.Direction);
        }
    }

    [Fact]
    public void ReadValue_ReadsUrlTitleAndFields()
    {
        var doc = Post("6170", SameDay);

        Assert.Equal("/6170/", ContentOrdering.ReadValue(doc, "/6170/", "url"));
        Assert.Equal("Post 6170", ContentOrdering.ReadValue(doc, "/6170/", "title"));
        Assert.Equal("6170", ContentOrdering.ReadValue(doc, "/6170/", "ID"));
        Assert.Null(ContentOrdering.ReadValue(doc, "/6170/", "missing"));
    }

    [Fact]
    public void CollectionRouteIndex_WithTieBreak_OrdersSameDayById()
    {
        var routed = new[] { "6167", "6170", "6168", "6169" }.Select(id => Routed(id, SameDay)).ToList();
        routed.Add(Routed("6166", SameDay.AddDays(-1)));

        var byId = CollectionRouteIndex.Create(routed, new ContentTieBreak("ID", Descending: true));
        var byUrl = CollectionRouteIndex.Create(routed);

        Assert.Equal(["6170", "6169", "6168", "6167", "6166"], byId.AllOrdered.Select(x => x.Document.Id));
        Assert.Equal(["6170", "6169", "6168", "6167", "6166"], byId.GetByCollection("post").Select(x => x.Document.Id));
        Assert.Equal(["6167", "6168", "6169", "6170", "6166"], byUrl.AllOrdered.Select(x => x.Document.Id));
    }

    [Fact]
    public void ListRouteGraph_HomeItems_UseTieBreak()
    {
        var routed = new[] { "6167", "6170", "6168" }.Select(id => Routed(id, SameDay)).ToList();

        var graph = ListRouteGraphBuilder.Build(routed, collections: null, "none", templateResolver: null, new ContentTieBreak("ID", Descending: true));

        var home = Assert.Single(graph.Routes, route => route.Kind == ListRouteKind.Home);
        Assert.Equal(["/6170/", "/6168/", "/6167/"], home.Items.Select(item => item.Url));
    }

    [Fact]
    public void WithContentOrdering_FillsOnlyUnsetTaxonomyTieBreak()
    {
        var inherits = new AppConfig
        {
            Site = new SiteConfig { Name = "t", Title = "t" },
            Content = new ContentConfig { OrderingTieBreakField = "ID", OrderingTieBreakOrder = "desc" }
        };
        var own = inherits with { Taxonomy = new TaxonomyConfig { TieBreakField = "title" } };
        var neither = new AppConfig { Site = new SiteConfig { Name = "t", Title = "t" }, Content = new ContentConfig() };

        var inherited = TaxonomySortHelper.ResolveTieBreak(TaxonomySortHelper.WithContentOrdering(inherits));
        Assert.Equal(("ID", true), (inherited!.Field, inherited.Descending));
        Assert.Null(TaxonomySortHelper.ResolveTieBreak(TaxonomySortHelper.WithContentOrdering(own)));
        Assert.Same(neither.Taxonomy, TaxonomySortHelper.WithContentOrdering(neither));
    }

    [Fact]
    public void FeedWindow_SameDayPostsFillWindowByTieBreak()
    {
        var tieBreak = new ContentTieBreak("ID", Descending: true);
        var posts = new[] { "6167", "6170", "6168", "6169" }
            .Select(id => RssGenerator.ToPost(Post(id, SameDay), $"https://example.com/{id}/", NullContentBodyStore.Instance, tieBreak, $"/{id}/"))
            .ToList();

        var window = RssGenerator.SelectWindow(posts, maxItems: 2);
        var unordered = RssGenerator.SelectWindow(posts.Select(p => p with { TieBreakDirection = 0 }), maxItems: 2);

        Assert.Equal(["https://example.com/6170/", "https://example.com/6169/"], window.Select(p => p.AbsoluteUrl));
        Assert.Equal(["https://example.com/6167/", "https://example.com/6168/"], unordered.Select(p => p.AbsoluteUrl));
    }

    private static RoutedContentDocument Routed(string id, DateTimeOffset publishAt)
        => new(Post(id, publishAt), new RouteInfo($"/{id}/", $"{id}/index.html", "pages/post.html"));

    private static ContentDocument Post(string id, DateTimeOffset publishAt)
        => ContentDocument.Create(
            id: id,
            title: "Post " + id,
            slug: id,
            publishAt: publishAt,
            contentHtml: $"<p>{id}</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "post",
                ["collection"] = "post",
                ["ID"] = int.Parse(id)
            }));
}
