using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Xunit;

namespace Bukit.Engine.Abstractions.Tests;

public sealed class RouteAndIndexTests
{
    [Fact]
    public void RouteInfo_StoresAllFields()
    {
        var route = new RouteInfo("/post/", "post/index.html", "pages/post.html");
        Assert.Equal("/post/", route.Url);
        Assert.Equal("post/index.html", route.OutputPath);
        Assert.Equal("pages/post.html", route.Template);
    }

    [Fact]
    public void SeoIndexEntry_DefaultIsDerivedFalse()
    {
        var entry = new SeoIndexEntry(
            new RouteInfo("/post/", "p.html", "t.html"),
            "https://example.com/post/", null, true,
            DateTimeOffset.UtcNow, "p1", "post");
        Assert.False(entry.IsDerived);
        Assert.True(entry.Indexable);
        Assert.Equal("post", entry.ContentType);
    }

    [Fact]
    public void SeoIndexEntry_ExplicitIsDerived()
    {
        var entry = new SeoIndexEntry(
            new RouteInfo("/tags/", "tags.html", "pages/tags.html"),
            "https://example.com/tags/", "noindex", false,
            DateTimeOffset.UtcNow, null, "tags-index", IsDerived: true);
        Assert.True(entry.IsDerived);
        Assert.False(entry.Indexable);
        Assert.Equal("noindex", entry.Robots);
    }

    [Fact]
    public void TableOfContentsEntry_HasLevelTextAndId()
    {
        var toc = new TableOfContentsEntry(2, "Introduction", "intro");
        Assert.Equal(2, toc.Level);
        Assert.Equal("Introduction", toc.Text);
        Assert.Equal("intro", toc.Id);
    }

    [Fact]
    public void ContentField_SimpleText()
    {
        var field = new ContentField("text", "hello");
        Assert.Equal("text", field.Type);
        Assert.Equal("hello", field.Value);
    }

    [Fact]
    public void ContentField_NumericValue()
    {
        var field = new ContentField("number", 42.0);
        Assert.Equal("number", field.Type);
        Assert.Equal(42.0, field.Value);
    }

    [Fact]
    public void ContentBody_StoresHtml()
    {
        var body = new ContentBody("<p>Hello</p>");
        Assert.Equal("<p>Hello</p>", body.Html);
    }
}
