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

    private sealed record Draft(string PageId, string Name);
}
