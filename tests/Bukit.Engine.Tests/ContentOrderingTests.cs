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
        var byUrl = CollectionRouteIndex.Create(routed, tieBreak: null);

        Assert.Equal(["6170", "6169", "6168", "6167", "6166"], byId.AllOrdered.Select(x => x.Document.Id));
        Assert.Equal(["6170", "6169", "6168", "6167", "6166"], byId.GetByCollection("post").Select(x => x.Document.Id));
        Assert.Equal(["6167", "6168", "6169", "6170", "6166"], byUrl.AllOrdered.Select(x => x.Document.Id));
    }

    [Fact]
    public void ListRouteGraph_HomeItems_UseTieBreak()
    {
        var routed = new[] { "6167", "6170", "6168" }.Select(id => Routed(id, SameDay)).ToList();

        var graph = ListRouteGraphBuilder.Build(routed, collections: null, "none", new ContentTieBreak("ID", Descending: true));

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
        var own = inherits with { Taxonomy = new TaxonomyConfig { TieBreakField = "url" } };
        var neither = new AppConfig { Site = new SiteConfig { Name = "t", Title = "t" }, Content = new ContentConfig() };

        var inherited = TaxonomySortHelper.ResolveTieBreak(TaxonomySortHelper.WithContentOrdering(inherits));
        Assert.Equal(("ID", true), (inherited!.Field, inherited.Descending));
        var ownUrl = TaxonomySortHelper.ResolveTieBreak(TaxonomySortHelper.WithContentOrdering(own));
        Assert.Equal(("url", false), (ownUrl!.Field, ownUrl.Descending));

        var titleDesc = inherits with { Content = inherits.Content with { OrderingTieBreakField = "title" } };
        var inheritedTitle = TaxonomySortHelper.ResolveTieBreak(TaxonomySortHelper.WithContentOrdering(titleDesc));
        Assert.Equal(("title", true), (inheritedTitle!.Field, inheritedTitle.Descending));
        Assert.Same(neither.Taxonomy, TaxonomySortHelper.WithContentOrdering(neither));
    }

    [Fact]
    public void WithContentOrdering_TaxonomyOrderAloneIsNotIgnored()
    {
        var site = new SiteConfig { Name = "t", Title = "t" };
        var orderOnly = new AppConfig { Site = site, Content = new ContentConfig(), Taxonomy = new TaxonomyConfig { TieBreakOrder = "desc" } };
        var overridesSite = orderOnly with { Content = new ContentConfig { OrderingTieBreakField = "ID", OrderingTieBreakOrder = "asc" } };

        var titleDesc = TaxonomySortHelper.ResolveTieBreak(TaxonomySortHelper.WithContentOrdering(orderOnly));
        Assert.Equal(("title", true), (titleDesc!.Field, titleDesc.Descending));
        var idDesc = TaxonomySortHelper.ResolveTieBreak(TaxonomySortHelper.WithContentOrdering(overridesSite));
        Assert.Equal(("ID", true), (idDesc!.Field, idDesc.Descending));
    }

    [Fact]
    public void ContentTitleOrdering_TermPagesMatchCollectionList()
    {
        var titles = new[] { ("/x/", "Issue 9"), ("/y/", "Issue 10"), ("/z/", "2024") };
        var routed = titles.Select(t => new RoutedContentDocument(
            ContentDocument.Create(
                id: t.Item1,
                title: t.Item2,
                slug: t.Item1.Trim('/'),
                publishAt: SameDay,
                contentHtml: "<p></p>",
                fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["collection"] = "post" })),
            new RouteInfo(t.Item1, $"{t.Item1.Trim('/')}/index.html", "pages/post.html"))).ToList();

        foreach (var order in new[] { "asc", "desc" })
        {
            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "t" },
                Content = new ContentConfig { OrderingTieBreakField = "title", OrderingTieBreakOrder = order }
            };
            var list = CollectionRouteIndex.Create(routed, ContentOrdering.Resolve(config)).AllOrdered.Select(x => x.Route.Url).ToList();
            var termTieBreak = TaxonomySortHelper.ResolveTieBreak(TaxonomySortHelper.WithContentOrdering(config));
            var term = routed
                .Select(x => new TaxonomyPage(x.Document.Id, x.Document.Title, x.Route.Url, x.Document.PublishAt, null, null, false, null))
                .Order(Comparer<TaxonomyPage>.Create((a, b) => TaxonomySortHelper.ComparePages(a, b, termTieBreak)))
                .Select(x => x.Url)
                .ToList();

            Assert.Equal(list, term);
        }
    }

    [Theory]
    [InlineData("1234567890123456789", "1234567890123456788", 1)]
    [InlineData("-5", "3", -1)]
    [InlineData("007", "7", 0)]
    [InlineData("-0", "0", 0)]
    [InlineData("1.5", "2", -1)]
    public void CompareValues_IntegersCompareExactly(string a, string b, int expectedSign)
    {
        Assert.Equal(expectedSign, Math.Sign(ContentOrdering.CompareValues(a, b, 1)));
        Assert.Equal(-expectedSign, Math.Sign(ContentOrdering.CompareValues(a, b, -1)));
    }

    [Fact]
    public void FeedWindow_SameDayPostsFillWindowByTieBreak()
    {
        var tieBreak = new ContentTieBreak("ID", Descending: true);
        var posts = new[] { "6167", "6170", "6168", "6169" }
            .Select(id => RssGenerator.ToPost(Post(id, SameDay), $"https://example.com/{id}/", NullContentBodyStore.Instance, tieBreak, $"/{id}/"))
            .ToList();

        var window = RssGenerator.SelectWindow(posts, maxItems: 2);
        var unordered = RssGenerator.SelectWindow(posts.Select(p => p with { TieBreak = null }), maxItems: 2);

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
