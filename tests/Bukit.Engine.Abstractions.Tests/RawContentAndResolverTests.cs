using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Xunit;

namespace Bukit.Engine.Abstractions.Tests;

public sealed class RawContentAndResolverTests
{
    [Fact]
    public void RawContentDocument_Constructor_NormalizesSourceKind()
    {
        var raw = new RawContentDocument("p1", "post", "Test", "test-post",
            DateTimeOffset.UtcNow, new RawBody("<p>Hello</p>"));
        Assert.Equal("post", raw.SourceKind);
        Assert.Equal("Test", raw.Title);
        Assert.Equal("test-post", raw.Slug);
    }

    [Fact]
    public void RawContentDocument_SourceKindDefaultsToUnknown()
    {
        var raw = new RawContentDocument("p1", "", "Test", "slug",
            DateTimeOffset.UtcNow, new RawBody("<p>X</p>"));
        Assert.Equal("unknown", raw.SourceKind);
    }

    [Fact]
    public void RawContentDocument_WithCustomFields()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["seoTitle"] = new("text", "SEO Title"),
            ["priority"] = new("number", 5.0)
        };
        var raw = new RawContentDocument("p1", "post", "Test", "slug",
            DateTimeOffset.UtcNow, new RawBody("<p>X</p>"),
            CustomFields: fields);
        Assert.Equal(2, raw.CustomFields!.Count);
        Assert.Equal("SEO Title", raw.CustomFields!["seoTitle"].Value);
    }

    [Fact]
    public async Task ContentBodyResolver_ReturnsInlineHtml()
    {
        var doc = ContentDocument.Create("x", "X", "x",
            DateTimeOffset.UtcNow, "inline-html");
        var html = await ContentBodyResolver.GetHtmlAsync(doc, NullContentBodyStore.Instance);
        Assert.Equal("inline-html", html);
    }

    [Fact]
    public async Task ContentBodyResolver_CancellationThrows()
    {
        var doc = ContentDocument.Create("x", "X", "x",
            DateTimeOffset.UtcNow, null, bodyKey: "body-key");
        var store = NullContentBodyStore.Instance;
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => ContentBodyResolver.GetHtmlAsync(doc, store, cts.Token));
    }
}
