using System.Text.Json;
using Bukit.Content.Notion;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionPropertyParserTests
{
    [Fact]
    public void ExtractAllFields_ParsesCommonPropertyTypes()
    {
        var json = """
                   {
                     "properties": {
                       "SEO Title": {
                         "type": "rich_text",
                         "rich_text": [{"plain_text":"Hello"}]
                       },
                       "Categories": {
                         "type": "multi_select",
                         "multi_select": [{"name":"Visa"}]
                       },
                       "Cover": {
                         "type": "url",
                         "url": "https://img.example/1.jpg"
                       },
                       "Published": {
                         "type": "checkbox",
                         "checkbox": true
                       },
                       "PublishAt": {
                         "type": "date",
                         "date": { "start": "2026-02-08" }
                       }
                     }
                   }
                   """;

        using var doc = JsonDocument.Parse(json);
        var props = doc.RootElement.GetProperty("properties");
        var fields = NotionPropertyParser.ExtractAllFields(props);

        Assert.Equal("text", fields["seo_title"].Type);
        Assert.Equal("Hello", fields["seo_title"].Value);

        Assert.Equal("list", fields["categories"].Type);
        var cats = Assert.IsAssignableFrom<IEnumerable<object>>(fields["categories"].Value!);
        Assert.Contains("Visa", cats.Select(x => x.ToString()));

        Assert.Equal("text", fields["cover"].Type);
        Assert.Equal("https://img.example/1.jpg", fields["cover"].Value);

        Assert.Equal("bool", fields["published"].Type);
        Assert.True((bool)fields["published"].Value!);

        Assert.Equal("date", fields["publishat"].Type);
        Assert.NotNull(fields["publishat"].Value);
    }
}

