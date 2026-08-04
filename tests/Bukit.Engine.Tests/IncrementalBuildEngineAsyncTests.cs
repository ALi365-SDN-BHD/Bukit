using System.Security.Cryptography;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Content.Media;
using Bukit.Engine.Incremental;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class IncrementalBuildEngineAsyncTests
{
    private static readonly DateTimeOffset s_testPublishAt = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

    private static ContentDocument CreateItem(
        string id = "test-id",
        string title = "Test Title",
        string slug = "test-slug",
        DateTimeOffset? publishAt = null,
        string? contentHtml = null,
        IReadOnlyDictionary<string, object>? fieldValues = null,
        IReadOnlyDictionary<string, ContentField>? fields = null)
    {
        return ContentDocument.Create(
            id,
            title,
            slug,
            publishAt ?? s_testPublishAt,
            contentHtml,
            ContentFieldReader.WithValues(fields, fieldValues ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase))
        );
    }

    private static RouteInfo CreateRoute(
        string url = "/pages/test/",
        string outputPath = "pages/test/index.html",
        string template = "pages/page.html")
    {
        return new RouteInfo(url, outputPath, template);
    }

    [Fact]
    public async Task ComputeListContentHashAsync_PageContentChange_InvalidatesImmediately()
    {
        var bodyStoreV1 = new StubBodyStore("<p>version-1</p>");
        var bodyStoreV2 = new StubBodyStore("<p>version-2</p>");
        var item = CreateItem(id: "post", slug: "post", contentHtml: "<p>version-1</p>");
        var route = CreateRoute(url: "/post/", outputPath: "post/index.html");
        var source = new[] { new RoutedContentDocument(item, route) };

        // Round 1: initial hash
        var hash1 = await IncrementalBuildEngine.ComputeListContentHashAsync(
            "tpl", "list.html", source, new BuildManifest(), bodyStoreV1,
            includeContent: true, CancellationToken.None);

        // Round 2: page content changes, list hash must change immediately
        var updatedItem = CreateItem(id: "post", slug: "post", contentHtml: "<p>version-2</p>");
        var updatedSource = new[] { new RoutedContentDocument(updatedItem, route) };
        var hash2 = await IncrementalBuildEngine.ComputeListContentHashAsync(
            "tpl", "list.html", updatedSource, new BuildManifest(), bodyStoreV2,
            includeContent: true, CancellationToken.None);

        Assert.NotEqual(hash1, hash2);

        // Round 3: no changes, list hash stays the same
        var hash3 = await IncrementalBuildEngine.ComputeListContentHashAsync(
            "tpl", "list.html", updatedSource, new BuildManifest(), bodyStoreV2,
            includeContent: true, CancellationToken.None);

        Assert.Equal(hash2, hash3);
    }

    [Fact]
    public async Task ComputeListContentHashAsync_PageMetadataChange_InvalidatesImmediately()
    {
        var item = CreateItem(id: "post", title: "Old Title", slug: "post");
        var route = CreateRoute(url: "/post/", outputPath: "post/index.html");
        var source = new[] { new RoutedContentDocument(item, route) };
        var bodyStore = NullContentBodyStore.Instance;

        var hash1 = await IncrementalBuildEngine.ComputeListContentHashAsync(
            "tpl", "list.html", source, new BuildManifest(), bodyStore,
            includeContent: false, CancellationToken.None);

        // Change title only
        var updatedItem = CreateItem(id: "post", title: "New Title", slug: "post");
        var updatedSource = new[] { new RoutedContentDocument(updatedItem, route) };
        var hash2 = await IncrementalBuildEngine.ComputeListContentHashAsync(
            "tpl", "list.html", updatedSource, new BuildManifest(), bodyStore,
            includeContent: false, CancellationToken.None);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public async Task ComputeListContentHashAsync_PublicContentRecordChanges_InvalidateWithoutCustomFieldChanges()
    {
        var item = CreateItem(id: "post", slug: "post");
        var route = CreateRoute(url: "/post/", outputPath: "post/index.html");
        var baseline = await ComputeMetadataOnlyListHashAsync(item, route);
        var record = item.Record;
        var variants = new[]
        {
            item with { Record = record with { Lifecycle = record.Lifecycle with { UpdatedAt = s_testPublishAt.AddHours(1) } } },
            item with { Record = record with { Provenance = record.Provenance with { Source = "changed-source" } } },
            item with { Record = record with { Trust = record.Trust with { ReviewStatus = "reviewed" } } },
            item with { Record = record with { Entities = [new EntityRecord("company", "Bukit")] } },
            item with { Record = record with { Ownership = record.Ownership with { Reviewer = "reviewer" } } }
        };

        foreach (var variant in variants)
        {
            Assert.NotEqual(baseline, await ComputeMetadataOnlyListHashAsync(variant, route));
        }
    }

    [Fact]
    public async Task ComputeListContentHashAsync_ComplexStructuralFields_AreDeterministicAndMatchSyncVersion()
    {
        var item = CreateItem(
            id: "structural",
            slug: "structural",
            fields: new Dictionary<string, ContentField>(StringComparer.Ordinal)
            {
                ["custom"] = new("object", new Dictionary<string, object?>
                {
                    ["values"] = new object?[] { null, string.Empty, 1, true, "a,b" },
                    ["nested"] = new Dictionary<string, object?> { ["line\nbreak"] = "value\nline" }
                }),
                ["tableOfContents"] = new("list", new[] { new TableOfContentsEntry(2, "Heading", "heading") })
            });
        var route = CreateRoute(url: "/structural/", outputPath: "structural/index.html");
        var source = new[] { new RoutedContentDocument(item, route) };

#pragma warning disable CS0618
        var syncHash = IncrementalBuildEngine.ComputeListContentHash(
            "th", "pages/list.html", source, new BuildManifest(), NullContentBodyStore.Instance, includeContent: false);
#pragma warning restore CS0618
        var asyncHash1 = await IncrementalBuildEngine.ComputeListContentHashAsync(
            "th", "pages/list.html", source, new BuildManifest(), NullContentBodyStore.Instance,
            includeContent: false, CancellationToken.None);
        var asyncHash2 = await IncrementalBuildEngine.ComputeListContentHashAsync(
            "th", "pages/list.html", source, new BuildManifest(), NullContentBodyStore.Instance,
            includeContent: false, CancellationToken.None);

        Assert.Equal(syncHash, asyncHash1);
        Assert.Equal(asyncHash1, asyncHash2);
    }

    private static Task<string> ComputeMetadataOnlyListHashAsync(ContentDocument item, RouteInfo route)
        => IncrementalBuildEngine.ComputeListContentHashAsync(
            "template-hash",
            "pages/list.html",
            [new RoutedContentDocument(item, route)],
            new BuildManifest(),
            NullContentBodyStore.Instance,
            includeContent: false,
            CancellationToken.None);

    private sealed class StubBodyStore : IContentBodyStore
    {
        private readonly string _html;

        public StubBodyStore(string html) => _html = html;

        public Task<ContentBody> GetAsync(ContentDocument item, CancellationToken cancellationToken = default)
            => Task.FromResult(new ContentBody(_html));
    }

    [Fact]
    public async Task ComputeListContentHashAsync_ComputesDeterministicHash()
    {
        var item = CreateItem(id: "a", slug: "a");
        var route = CreateRoute(url: "/a/", outputPath: "a/index.html");
        var source = new[] { new RoutedContentDocument(item, route) };
        var manifest = new BuildManifest();
        var bodyStore = new StubBodyStore("<p>hello</p>");

        var hash1 = await IncrementalBuildEngine.ComputeListContentHashAsync(
            "tpl-hash", "pages/list.html", source, manifest, bodyStore, includeContent: true, CancellationToken.None);
        var hash2 = await IncrementalBuildEngine.ComputeListContentHashAsync(
            "tpl-hash", "pages/list.html", source, manifest, bodyStore, includeContent: true, CancellationToken.None);

        Assert.Equal(hash1, hash2);
        Assert.NotEmpty(hash1);
        Assert.Equal(64, hash1.Length);
    }

    [Fact]
    public async Task ComputeListContentHashAsync_MatchesSyncVersion_WithIncludeContent()
    {
        var html = "<p>Body content</p>";
        var item = CreateItem(id: "x", slug: "x", contentHtml: html);
        var route = CreateRoute(url: "/x/", outputPath: "x/index.html");
        var source = new[] { new RoutedContentDocument(item, route) };
        var manifest = new BuildManifest();
        var bodyStore = NullContentBodyStore.Instance;

#pragma warning disable CS0618
        var syncHash = IncrementalBuildEngine.ComputeListContentHash(
            "th", "pages/list.html", source, manifest, bodyStore, includeContent: true);
#pragma warning restore CS0618
        var asyncHash = await IncrementalBuildEngine.ComputeListContentHashAsync(
            "th", "pages/list.html", source, manifest, bodyStore, includeContent: true, CancellationToken.None);

        Assert.Equal(syncHash, asyncHash);
    }

    [Fact]
    public async Task ComputeListContentHashAsync_MatchesSyncVersion_WithoutIncludeContent()
    {
        var item = CreateItem(id: "y", slug: "y");
        var route = CreateRoute(url: "/y/", outputPath: "y/index.html");
        var source = new[] { new RoutedContentDocument(item, route) };
        var manifest = new BuildManifest();
        var bodyStore = NullContentBodyStore.Instance;

#pragma warning disable CS0618
        var syncHash = IncrementalBuildEngine.ComputeListContentHash(
            "th", "pages/list.html", source, manifest, bodyStore, includeContent: false);
#pragma warning restore CS0618
        var asyncHash = await IncrementalBuildEngine.ComputeListContentHashAsync(
            "th", "pages/list.html", source, manifest, bodyStore, includeContent: false, CancellationToken.None);

        Assert.Equal(syncHash, asyncHash);
    }

    [Fact]
    public async Task ComputeListContentHashAsync_MatchesSyncVersion_WithManifestEntries()
    {
        var item = CreateItem(id: "z", title: "Post Z");
        var route = CreateRoute(url: "/z/", outputPath: "z/index.html");
        var source = new[] { new RoutedContentDocument(item, route) };

        var manifest = new BuildManifest
        {
            Entries = new Dictionary<string, BuildManifestEntry>(StringComparer.Ordinal)
            {
                ["z/index.html"] = new()
                {
                    OutputPath = "z/index.html",
                    ContentHash = "manifest-content-hash",
                    RouteHash = "manifest-route-hash"
                }
            }
        };
        var bodyStore = NullContentBodyStore.Instance;

#pragma warning disable CS0618
        var syncHash = IncrementalBuildEngine.ComputeListContentHash(
            "th", "pages/list.html", source, manifest, bodyStore, includeContent: false);
#pragma warning restore CS0618
        var asyncHash = await IncrementalBuildEngine.ComputeListContentHashAsync(
            "th", "pages/list.html", source, manifest, bodyStore, includeContent: false, CancellationToken.None);

        Assert.Equal(syncHash, asyncHash);
    }

    [Fact]
    public async Task ComputeListContentHashAsync_DifferentTemplateHash_ProducesDifferentHash()
    {
        var source = Array.Empty<RoutedContentDocument>();
        var manifest = new BuildManifest();
        var bodyStore = NullContentBodyStore.Instance;

        var hash1 = await IncrementalBuildEngine.ComputeListContentHashAsync(
            "hash-a", "pages/list.html", source, manifest, bodyStore, includeContent: false, CancellationToken.None);
        var hash2 = await IncrementalBuildEngine.ComputeListContentHashAsync(
            "hash-b", "pages/list.html", source, manifest, bodyStore, includeContent: false, CancellationToken.None);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public async Task ComputeListContentHashAsync_UsesBodyStoreAsync()
    {
        var callCount = 0;
        var trackingStore = new TrackingBodyStore("<p>async body</p>", () => callCount++);

        var item = CreateItem(id: "track", slug: "track");
        var route = CreateRoute(url: "/track/", outputPath: "track/index.html");
        var source = new[] { new RoutedContentDocument(item, route) };
        var manifest = new BuildManifest();

        await IncrementalBuildEngine.ComputeListContentHashAsync(
            "th", "pages/list.html", source, manifest, trackingStore, includeContent: true, CancellationToken.None);

        Assert.Equal(1, callCount);
    }

    private sealed class TrackingBodyStore : IContentBodyStore
    {
        private readonly string _html;
        private readonly Action _onGet;

        public TrackingBodyStore(string html, Action onGet)
        {
            _html = html;
            _onGet = onGet;
        }

        public Task<ContentBody> GetAsync(ContentDocument item, CancellationToken cancellationToken = default)
        {
            _onGet();
            return Task.FromResult(new ContentBody(_html));
        }
    }
}
