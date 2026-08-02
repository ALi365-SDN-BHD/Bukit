using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SeoObservationIdentityTests
{
    [Fact]
    public void CreateRouteKey_ChangesAcrossRouteChanges()
    {
        var first = SeoObservationIdentity.CreateRouteKey("/old/", "/old/");
        var second = SeoObservationIdentity.CreateRouteKey("/new/", "/new/");

        Assert.NotEqual(first, second);
        Assert.Matches("^route:sha256:[0-9a-f]{64}$", first);
    }

    [Fact]
    public void CreateRouteKey_UsesRouteAndCanonicalDeterministically()
    {
        var first = SeoObservationIdentity.CreateRouteKey("/old/", "/old/");
        var repeat = SeoObservationIdentity.CreateRouteKey("/old/", "/old/");
        var changedCanonical = SeoObservationIdentity.CreateRouteKey("/old/", "https://example.com/old/");

        Assert.Equal("route:sha256:69f74d94ac45882e8fb7b0e08f150a6751ced104b3752132f957873ed4e89322", first);
        Assert.Equal(first, repeat);
        Assert.Equal("route:sha256:b3bbc6898ffc90f36fe6708c28842035cf89341d51d15db840d1cdca4be08bcb", changedCanonical);
    }

    [Theory]
    [InlineData("", "/canonical/")]
    [InlineData("   ", "/canonical/")]
    [InlineData("/route/", "")]
    [InlineData("/route/", "   ")]
    public void CreateRouteKey_RejectsBlankIdentityComponents(string route, string canonical)
    {
        Assert.Throws<ArgumentException>(() => SeoObservationIdentity.CreateRouteKey(route, canonical));
    }

    [Fact]
    public void CreateContentKey_IsStableAcrossRouteChangesAndDistinguishesLanguages()
    {
        var record = TestRecord("internal-id");
        var first = SeoObservationIdentity.CreateContentKey(record, "zh-CN");
        var second = SeoObservationIdentity.CreateContentKey(record, "zh-CN");

        Assert.Equal(first, second);
        Assert.Equal("content:sha256:ce279df7a710d9187c0bcf6817ffe77012cdf639506a5a99a032dc4300e7867e", first);
        Assert.Matches("^content:sha256:[0-9a-f]{64}$", first);
        Assert.DoesNotContain("internal-id", first, StringComparison.Ordinal);
        Assert.NotEqual(first, SeoObservationIdentity.CreateContentKey(record, "en"));
    }

    [Fact]
    public void CreateContentKey_ReturnsNullWithoutContentRecord()
    {
        Assert.Null(SeoObservationIdentity.CreateContentKey(null, "en"));
    }

    private static ContentRecord TestRecord(string id)
        => new(
            new ContentIdentity(id, "post", "post", "post", "published"),
            new ContentPresentation("Post", null, null, "zh-CN", []),
            new ContentClassification("post", "news", [], []),
            new ContentOwnership(null, null, null, null),
            new ContentLifecycle(DateTimeOffset.Parse("2026-08-03T00:00:00Z"), null, null, null),
            new ProvenanceRecord("notion", null, [], [], null),
            new TrustMetadata(null, "approved", []),
            [],
            [],
            []);
}
