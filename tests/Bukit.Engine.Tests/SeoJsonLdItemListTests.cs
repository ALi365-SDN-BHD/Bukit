using System.Linq;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Tests.Infrastructure;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SeoJsonLdItemListTests
{
    [Fact]
    public void BuildItemList_LongList_CapsElementsAndReportsTotal()
    {
        var items = Enumerable.Range(1, 250)
            .Select(i => (object)new Dictionary<string, object> { ["title"] = $"Post {i}", ["url"] = $"/p/{i}/" })
            .ToList();
        var fields = new Dictionary<string, ContentField> { ["items"] = new ContentField("list", items) };
        var config = new AppConfig { Site = new SiteConfig { Name = "t", Title = "T", Url = "https://example.com" }, Content = TestContent.Markdown() };

        var list = SeoJsonLdBuilder.BuildItemList(config, "/", fields)!;

        var elements = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(list["itemListElement"]);
        Assert.Equal(SeoJsonLdBuilder.MaxItemListElements, elements.Count);
        Assert.Equal(250, list["numberOfItems"]);
        Assert.Equal(100, elements[^1]["position"]);
    }
}
