using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Engine.Abstractions.Tests;

public sealed class RawContentDocumentTests
{
    [Fact]
    public void Constructor_ShouldPreserveProviderInput_WhenRawPropertiesAndSourceAreProvided()
    {
        var source = new ContentSourceInfo(
            Provider: "markdown",
            SourceKey: "posts",
            SourcePath: "content/posts/hello.md",
            ExternalId: null,
            ExternalUrl: new Uri("https://example.test/source"),
            SyncedAt: new DateTimeOffset(2026, 6, 6, 0, 0, 0, TimeSpan.Zero),
            SyncStatus: "synced");
        var raw = new RawContentDocument(
            SourceId: "posts/hello",
            SourceKind: "markdown",
            Title: "Hello",
            Slug: "hello",
            PublishedAt: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            Body: new RawBody(InlineHtml: "<p>Hello</p>", BodyKey: "body-1", Markdown: "# Hello", PlainText: "Hello"),
            Properties: new Dictionary<string, RawContentValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", "post"),
                ["tags"] = new("list", new[] { "ai", "infra" })
            },
            Source: source,
            CustomFields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["featured"] = new("bool", true)
            });

        Assert.Equal("posts/hello", raw.SourceId);
        Assert.Equal("markdown", raw.Source.Provider);
        Assert.Equal("post", raw.Properties["type"].Value);
        Assert.True((bool)raw.CustomFields["featured"].Value!);
    }
}
