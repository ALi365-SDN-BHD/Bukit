using Bukit.Content.Notion;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionBodyStoreTests
{
    [Fact]
    public async Task GetAsync_RendersOnDemand_AndCachesByBodyKey()
    {
        var renderCount = 0;
        var store = new NotionBodyStore(async (item, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            renderCount++;
            await Task.Yield();
            return $"<p>{item.Id}</p>";
        });

        var item = new ContentItem(
            Id: "page-1",
            Title: "Page",
            Slug: "page",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: null,
            Meta: new Dictionary<string, object>(),
            Fields: null,
            BodyKey: "page-1");

        Assert.Equal(0, renderCount);

        var first = await store.GetAsync(item);
        var second = await store.GetAsync(item);

        Assert.Equal("<p>page-1</p>", first.Html);
        Assert.Equal("<p>page-1</p>", second.Html);
        Assert.Equal(1, renderCount);
    }
}
