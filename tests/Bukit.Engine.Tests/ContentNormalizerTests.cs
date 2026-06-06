using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Normalization;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ContentNormalizerTests
{
    [Fact]
    public void Normalize_ShouldCreateTypedDocument_WhenRawMarkdownPropertiesUseCanonicalKeys()
    {
        var raw = new RawContentDocument(
            SourceId: "posts/hello",
            SourceKind: "markdown",
            Title: "Hello",
            Slug: "hello",
            PublishedAt: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            Body: new RawBody("<p>Hello</p>", "body-1", "# Hello", "Hello"),
            Properties: new Dictionary<string, RawContentValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", "article"),
                ["collection"] = new("text", "posts"),
                ["language"] = new("text", "en"),
                ["summary"] = new("text", "A typed document"),
                ["tags"] = new("list", new[] { "ai", "infra" }),
                ["author"] = new("text", "Ada"),
                ["route.url"] = new("text", "/posts/hello/"),
                ["route.outputPath"] = new("text", "posts/hello/index.html"),
                ["route.template"] = new("text", "pages/post.html")
            },
            Source: new ContentSourceInfo("markdown", "posts", "content/posts/hello.md", null, null, null, "synced"),
            CustomFields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["featured"] = new("bool", true)
            });
        IContentNormalizer normalizer = new ContentNormalizer();

        var document = normalizer.Normalize(raw, ContentModelSchema.Default);

        Assert.Equal("posts/hello", document.Record.Identity.Id);
        Assert.Equal("article", document.Record.Classification.Type);
        Assert.Equal("posts", document.Record.Classification.Collection);
        Assert.Equal("en", document.Record.Presentation.Language);
        Assert.Equal("A typed document", document.Record.Presentation.Summary);
        Assert.Equal(["ai", "infra"], document.Record.Classification.Tags);
        Assert.Equal("Ada", document.Record.Ownership.Author);
        Assert.Equal("/posts/hello/", document.Route.Url);
        Assert.Equal("posts/hello/index.html", document.Route.OutputPath);
        Assert.Equal("pages/post.html", document.Route.Template);
        Assert.False(document.Publish.Draft);
        Assert.True((bool)document.CustomFields["featured"].Value!);
    }

    [Fact]
    public void Normalize_ShouldReportUnknownRawKey_WhenPropertyIsNotMappedOrDeclared()
    {
        var raw = new RawContentDocument(
            SourceId: "pages/about",
            SourceKind: "markdown",
            Title: "About",
            Slug: "about",
            PublishedAt: null,
            Body: new RawBody("<p>About</p>", null, null, "About"),
            Properties: new Dictionary<string, RawContentValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", "page"),
                ["legacySummary"] = new("text", "Old field")
            },
            Source: new ContentSourceInfo("markdown", "pages", "content/about.md", null, null, null, "synced"),
            CustomFields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase));
        IContentNormalizer normalizer = new ContentNormalizer();

        var document = normalizer.Normalize(raw, ContentModelSchema.Default);

        var diagnostic = Assert.Single(document.Diagnostics);
        Assert.Equal("content.unknown_raw_key", diagnostic.Code);
        Assert.Equal("error", diagnostic.Severity);
        Assert.Equal("legacySummary", diagnostic.Field);
        Assert.Equal("pages/about", diagnostic.SourceId);
    }

    [Fact]
    public void Normalize_ShouldMapEntitiesRelationsAndMedia_WhenRawPropertiesContainCanonicalGraphFields()
    {
        var raw = new RawContentDocument(
            SourceId: "posts/entity-rich",
            SourceKind: "markdown",
            Title: "Entity Rich",
            Slug: "entity-rich",
            PublishedAt: null,
            Body: new RawBody("<p>Body</p>", null, null, "Body"),
            Properties: new Dictionary<string, RawContentValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", "post"),
                ["companies"] = new("list", new[] { "Bukit" }),
                ["people"] = new("list", new[] { "Ada" }),
                ["relations.translationOf"] = new("text", "post-original"),
                ["relations.relatedTo"] = new("list", new[] { "post-2", "post-3" }),
                ["image"] = new("text", "/img/cover.png"),
                ["image_alt"] = new("text", "Cover image"),
                ["image_caption"] = new("text", "Launch cover"),
                ["image_license"] = new("text", "CC-BY")
            },
            Source: new ContentSourceInfo("markdown", "posts", "content/posts/entity-rich.md", null, null, null, "synced"),
            CustomFields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase));
        IContentNormalizer normalizer = new ContentNormalizer();

        var document = normalizer.Normalize(raw, ContentModelSchema.Default);

        Assert.Contains(document.Record.Entities, entity => entity.Type == "company" && entity.Name == "Bukit");
        Assert.Contains(document.Record.Entities, entity => entity.Type == "person" && entity.Name == "Ada");
        Assert.Contains(document.Record.Relations, relation => relation.Type == "translation-of" && relation.Target == "post-original");
        Assert.Contains(document.Record.Relations, relation => relation.Type == "related-to" && relation.Target == "post-2");
        var media = Assert.Single(document.Record.Media);
        Assert.Equal("image", media.Kind);
        Assert.Equal("/img/cover.png", media.Url);
        Assert.Equal("Cover image", media.Alt);
        Assert.Equal("Launch cover", media.Caption);
        Assert.Equal("CC-BY", media.License);
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void Normalize_ShouldHonorLegacyCanonicalAliasesAndSchemaMappings()
    {
        var raw = new RawContentDocument(
            SourceId: "posts/alias",
            SourceKind: "markdown",
            Title: "Alias",
            Slug: "alias",
            PublishedAt: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            Body: new RawBody("<p>Alias</p>", null, null, "Alias"),
            Properties: new Dictionary<string, RawContentValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["description"] = new("text", "Alias summary"),
                ["categories"] = new("list", new[] { "guides" }),
                ["original_url"] = new("text", "https://example.com/original"),
                ["review_status"] = new("text", "approved"),
                ["last_edited_time"] = new("date", "2026-06-05T12:00:00Z"),
                ["notionOwner"] = new("text", "Ali")
            },
            Source: new ContentSourceInfo("markdown", "posts", "content/posts/alias.md", null, null, null, "synced"),
            CustomFields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase));
        var schema = new ContentModelSchema(
            new Dictionary<string, CanonicalFieldMapping>(StringComparer.OrdinalIgnoreCase)
            {
                ["owner"] = new("notionOwner", "owner", "text")
            },
            new Dictionary<string, CustomFieldDefinition>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, EntityMapping>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, RelationMapping>(StringComparer.OrdinalIgnoreCase));
        IContentNormalizer normalizer = new ContentNormalizer();

        var document = normalizer.Normalize(raw, schema);

        Assert.Equal("Alias summary", document.Record.Presentation.Summary);
        Assert.Equal(["guides"], document.Record.Classification.Sections);
        Assert.Equal("https://example.com/original", document.Record.Provenance.OriginalSource);
        Assert.Equal("approved", document.Record.Trust.ReviewStatus);
        Assert.Equal("Ali", document.Record.Ownership.Owner);
        Assert.Equal(DateTimeOffset.Parse("2026-06-05T12:00:00Z"), document.Record.Lifecycle.UpdatedAt);
        Assert.Empty(document.Diagnostics);
    }
}
