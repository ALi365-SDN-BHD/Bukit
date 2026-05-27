using Xunit;
using Bukit.Theme;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
namespace Bukit.Theme.Tests;

public sealed class SectionDataResolverTests
{
    private static ContentItem MakeItem(string id, string title, string type, DateTimeOffset publishAt, List<string>? collections = null, IReadOnlyDictionary<string, ContentField>? fields = null)
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = type
        };
        if (collections is not null)
        {
            meta["collections"] = collections;
        }

        return new ContentItem(id, title, id, publishAt, null, meta, fields);
    }

    private static RouteInfo MakeRoute(string url)
    {
        return new RouteInfo(url, url.TrimStart('/') + "index.html", "pages/page.html");
    }

    private static IReadOnlyList<(ContentItem Item, RouteInfo? Route)> MakePages(params (ContentItem, string)[] items)
    {
        return items.Select(i => ((ContentItem, RouteInfo?))(i.Item1, MakeRoute(i.Item2))).ToList();
    }

    [Fact]
    public void Resolve_MatchesSourceByCollection()
    {
        var items = MakePages(
            (MakeItem("post1", "First Post", "post", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)), "/blog/first-post/"),
            (MakeItem("page1", "About", "page", new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero)), "/about/"),
            (MakeItem("post2", "Second Post", "post", new DateTimeOffset(2024, 1, 3, 0, 0, 0, TimeSpan.Zero)), "/blog/second-post/")
        );

        var sectionDef = new PageSectionDefinition
        {
            Type = "hero",
            Source = "post"
        };

        var result = SectionDataResolver.Resolve(sectionDef, items);
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.EndsWith("/", r.Url));
    }

    [Fact]
    public void Resolve_LimitApplied()
    {
        var items = MakePages(
            (MakeItem("a", "A", "post", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)), "/a/"),
            (MakeItem("b", "B", "post", new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero)), "/b/"),
            (MakeItem("c", "C", "post", new DateTimeOffset(2024, 1, 3, 0, 0, 0, TimeSpan.Zero)), "/c/")
        );

        var sectionDef = new PageSectionDefinition
        {
            Type = "hero",
            Source = "post",
            Limit = 2
        };

        var result = SectionDataResolver.Resolve(sectionDef, items);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Resolve_SortByTitleAscending()
    {
        var items = MakePages(
            (MakeItem("c", "Charlie", "post", new DateTimeOffset(2024, 1, 3, 0, 0, 0, TimeSpan.Zero)), "/c/"),
            (MakeItem("a", "Alpha", "post", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)), "/a/"),
            (MakeItem("b", "Bravo", "post", new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero)), "/b/")
        );

        var sectionDef = new PageSectionDefinition
        {
            Type = "hero",
            Source = "post",
            Sort = "title"
        };

        var result = SectionDataResolver.Resolve(sectionDef, items);
        Assert.Equal(3, result.Count);
        Assert.Equal("Alpha", result[0].Item.Title);
        Assert.Equal("Bravo", result[1].Item.Title);
        Assert.Equal("Charlie", result[2].Item.Title);
    }

    [Fact]
    public void Resolve_FiltersWork()
    {
        var items = MakePages(
            (MakeItem("a", "Featured A", "post", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                {
                    ["featured"] = new("bool", true)
                }), "/a/"),
            (MakeItem("b", "Regular B", "post", new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero),
                fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                {
                    ["featured"] = new("bool", false)
                }), "/b/"),
            (MakeItem("c", "Featured C", "post", new DateTimeOffset(2024, 1, 3, 0, 0, 0, TimeSpan.Zero),
                fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                {
                    ["featured"] = new("bool", true)
                }), "/c/")
        );

        var sectionDef = new PageSectionDefinition
        {
            Type = "hero",
            Source = "post",
            Filter = new Dictionary<string, object?> { ["featured"] = true }
        };

        var result = SectionDataResolver.Resolve(sectionDef, items);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Item.Title == "Featured A");
        Assert.Contains(result, r => r.Item.Title == "Featured C");
    }

    [Fact]
    public void Resolve_EmptySourceReturnsEmpty()
    {
        var items = MakePages(
            (MakeItem("a", "A", "post", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)), "/a/")
        );

        var sectionDef = new PageSectionDefinition
        {
            Type = "hero",
            Source = ""
        };

        var result = SectionDataResolver.Resolve(sectionDef, items);
        Assert.Empty(result);
    }

    [Fact]
    public void Resolve_WildcardSourceMatchesAll()
    {
        var items = MakePages(
            (MakeItem("a", "A", "post", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)), "/a/"),
            (MakeItem("b", "B", "page", new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero)), "/b/")
        );

        var sectionDef = new PageSectionDefinition
        {
            Type = "hero",
            Source = "*"
        };

        var result = SectionDataResolver.Resolve(sectionDef, items);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Resolve_TypePrefixWorks()
    {
        var items = MakePages(
            (MakeItem("a", "A", "post", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)), "/a/"),
            (MakeItem("b", "B", "page", new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero)), "/b/"),
            (MakeItem("c", "C", "post", new DateTimeOffset(2024, 1, 3, 0, 0, 0, TimeSpan.Zero)), "/c/")
        );

        var sectionDef = new PageSectionDefinition
        {
            Type = "hero",
            Source = "type:post"
        };

        var result = SectionDataResolver.Resolve(sectionDef, items);
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal("post", r.Item.Meta["type"]));
    }

    [Fact]
    public void Resolve_InvalidSourceReturnsEmpty()
    {
        var items = MakePages(
            (MakeItem("a", "A", "post", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)), "/a/")
        );

        var sectionDef = new PageSectionDefinition
        {
            Type = "hero",
            Source = "nonexistent"
        };

        var result = SectionDataResolver.Resolve(sectionDef, items);
        Assert.Empty(result);
    }
}
