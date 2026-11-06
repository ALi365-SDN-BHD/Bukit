using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ContentFieldReservationsTests
{
    [Fact]
    public void StrictSchema_AllowsEngineManagedFieldsWithoutDeclaringThem()
    {
        var schema = new ContentModelSchema(RejectUnknownRawKeys: true);
        var raw = RawDocument(new Dictionary<string, RawContentValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = new("text", "Page"),
            ["slug"] = new("text", "page"),
            ["type"] = new("text", "page"),
            ["collection"] = new("text", "page"),
            ["source"] = new("text", "markdown"),
            ["sourcePath"] = new("text", "content/page.md"),
            ["sourceKey"] = new("text", "pages"),
            ["sourceMode"] = new("text", "content"),
            ["sourceId"] = new("text", "page"),
            ["bodyFingerprint"] = new("text", "abc"),
            ["tableOfContents"] = new("list", Array.Empty<object>()),
            ["publishAt"] = new("date", DateTimeOffset.UnixEpoch)
        });

        var document = ContentDocumentNormalizer.ToDocument(raw, schema);

        Assert.DoesNotContain(document.Diagnostics, diagnostic =>
            string.Equals(diagnostic.Code, "content.unknown_raw_key", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StrictSchema_StillRejectsUndeclaredUserFields()
    {
        var schema = new ContentModelSchema(RejectUnknownRawKeys: true);
        var raw = RawDocument(new Dictionary<string, RawContentValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = new("text", "page"),
            ["collection"] = new("text", "page"),
            ["sourceKey"] = new("text", "pages"),
            ["articleOnly"] = new("text", "not declared")
        });

        var exception = Assert.Throws<ConfigException>(() => ContentDocumentNormalizer.ToDocument(raw, schema));

        Assert.Contains("articleOnly", exception.Message, StringComparison.Ordinal);
    }

    private static RawContentDocument RawDocument(IReadOnlyDictionary<string, RawContentValue> properties)
        => new(
            SourceId: "page",
            SourceKind: "markdown",
            Title: "Page",
            Slug: "page",
            PublishedAt: DateTimeOffset.UnixEpoch,
            Body: new RawBody(InlineHtml: "<p>Page</p>"),
            Properties: properties,
            Source: new ContentSourceInfo("markdown", SourcePath: "content/page.md"));
}
