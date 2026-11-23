using Bukit.Engine;
using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class CloudBaseProjectionPolicyTests
{
    [Fact]
    public void CloudBaseRecord_DropsCloudIdentifiersFromPublicProjections()
    {
        var record = new ContentRecord(
            new ContentIdentity(
                "id",
                "slug",
                "cloud://env-abc.media/posts/a.png",
                "page",
                "published"),
            new ContentPresentation("Title", null, null, "en", []),
            new ContentClassification("page", "page", [], []),
            new ContentOwnership(null, null, null, null),
            new ContentLifecycle(DateTimeOffset.UnixEpoch, null, null, null),
            new ProvenanceRecord("cloudbase", null, [], [], "loaded"),
            new TrustMetadata(null, "unreviewed", []),
            [
                new EntityRecord(
                    "organization",
                    "cloud://env-abc.media/a.png",
                    Id: "cloud://env-abc.media/a.png",
                    Url: "https://env-abc.api.tcloudbasegateway.com/v1/storages/object/sign/media/a.png?token=x",
                    SameAs: ["https://env-abc.storage.tcloudbase.com/media/a.png?token=x"])
            ],
            [new ContentRelation("related", "cloud://env-abc.media/b.png")],
            []);

        Assert.Equal("/route/", PublicContentProjectionPolicy.ResolvePublicId(record, "/route/"));
        Assert.Empty(PublicContentProjectionPolicy.SanitizeEntities(record));
        Assert.Empty(PublicContentProjectionPolicy.SanitizeRelations(record));
    }

    [Fact]
    public void NonCloudBaseRecord_IsUnchanged()
    {
        var record = new ContentRecord(
            new ContentIdentity("id", "slug", "stable-key", "page", "published"),
            new ContentPresentation("Title", null, null, "en", []),
            new ContentClassification("page", "page", [], []),
            new ContentOwnership(null, null, null, null),
            new ContentLifecycle(DateTimeOffset.UnixEpoch, null, null, null),
            new ProvenanceRecord("markdown", null, [], [], "loaded"),
            new TrustMetadata(null, "unreviewed", []),
            [new EntityRecord("organization", "Example")],
            [new ContentRelation("related", "other")],
            []);

        Assert.Equal("stable-key", PublicContentProjectionPolicy.ResolvePublicId(record, "/route/"));
        Assert.Single(PublicContentProjectionPolicy.SanitizeEntities(record));
        Assert.Single(PublicContentProjectionPolicy.SanitizeRelations(record));
    }
}
