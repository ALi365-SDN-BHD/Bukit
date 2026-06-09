using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Engine.Abstractions.Tests;

public sealed class CanonicalContentTests
{
    private static ContentRecord CreateRecord(string id = "p1", string slug = "post-1",
        string status = "published", string author = "Alice", string? summary = null)
    {
        return new ContentRecord(
            new ContentIdentity(id, slug, slug, "post", status),
            new ContentPresentation("Test Post", summary, null, "en", Array.Empty<string>()),
            new ContentClassification("post", "posts", Array.Empty<string>(), Array.Empty<string>()),
            new ContentOwnership(author, null, null, null),
            new ContentLifecycle(DateTimeOffset.UtcNow, null, null, null),
            new ProvenanceRecord("https://src.test", null, Array.Empty<string>(), Array.Empty<string>(), "synced"),
            new TrustMetadata(0.9, "approved", Array.Empty<string>()),
            new[] { new EntityRecord("Person", "Alice", Id: "alice-1") },
            Array.Empty<ContentRelation>(),
            Array.Empty<MediaAsset>());
    }

    [Fact]
    public void Empty_Graph_HasNoRecords()
    {
        Assert.Empty(CanonicalContentGraph.Empty.Records);
        Assert.Empty(CanonicalContentGraph.Empty.Entities);
    }

    [Fact]
    public void Graph_Constructor_ExtractsRelationsFromRecords()
    {
        var record = CreateRecord();
        var graph = new CanonicalContentGraph(
            new[] { record },
            new[] { new EntityRecord("Org", "Bukit") });
        Assert.Single(graph.Records);
        Assert.Single(graph.Entities);
    }

    [Fact]
    public void ContentIdentity_StoresAllFields()
    {
        var identity = new ContentIdentity("x", "slug-x", "slug-x", "page", "published");
        Assert.Equal("x", identity.Id);
        Assert.Equal("slug-x", identity.Slug);
        Assert.Equal("published", identity.Status);
    }

    [Fact]
    public void ContentLifecycle_NullableDates_DefaultToNull()
    {
        var lifecycle = new ContentLifecycle(DateTimeOffset.UtcNow, null, null, null);
        Assert.Null(lifecycle.UpdatedAt);
        Assert.Null(lifecycle.ExpiresAt);
    }

    [Fact]
    public void EntityRecord_HasOptionalFields()
    {
        var entity = new EntityRecord("Company", "Bukit",
            Description: "Static site generator",
            Url: "https://bukit.dev");
        Assert.Equal("Company", entity.Type);
        Assert.Equal("https://bukit.dev", entity.Url);
    }

    [Fact]
    public void MediaAsset_StoresCaptionAndAlt()
    {
        var asset = new MediaAsset("/img/hero.png", "image/png",
            Alt: "Hero image", Caption: "Main hero");
        Assert.Equal("Hero image", asset.Alt);
        Assert.Equal("Main hero", asset.Caption);
    }
}
