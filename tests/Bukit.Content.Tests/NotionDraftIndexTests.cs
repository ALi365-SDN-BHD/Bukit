using Bukit.Content.Notion;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionDraftIndexTests
{
    [Fact]
    public void From_CreatesCaseInsensitiveLookup()
    {
        var first = new Draft("Page-1", "first");
        var second = new Draft("page-2", "second");

        var index = NotionDraftIndex<Draft>.From(new[] { first, second }, x => x.PageId);

        Assert.Same(first, index.GetRequired("page-1"));
        Assert.Same(second, index.GetRequired("PAGE-2"));
    }

    [Fact]
    public void From_SkipsEmptyOrWhitespacePageId()
    {
        var drafts = new[]
        {
            new Draft("", "empty"),
            new Draft("  ", "whitespace"),
            new Draft("valid-id", "valid"),
            new Draft(null!, "null")
        };

        var index = NotionDraftIndex<Draft>.From(drafts, x => x.PageId);

        var result = index.GetRequired("valid-id");
        Assert.Equal("valid", result.Name);

        Assert.Throws<InvalidOperationException>(() => index.GetRequired(""));
        Assert.Throws<InvalidOperationException>(() => index.GetRequired("  "));
    }

    private sealed record Draft(string PageId, string Name);
}
