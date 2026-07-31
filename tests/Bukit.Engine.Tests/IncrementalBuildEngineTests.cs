using System.Security.Cryptography;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Content.Media;
using Bukit.Engine.Incremental;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Xunit;

namespace Bukit.Engine.Tests;

#pragma warning disable CS0618 // Sync document hashing is intentionally tested for deterministic behavior.
public sealed class IncrementalBuildEngineTests
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
    public void ComputeMetadataHash_SameItem_ProducesSameHash()
    {
        var item = CreateItem();
        var hash1 = IncrementalBuildEngine.ComputeMetadataHash(item);
        var hash2 = IncrementalBuildEngine.ComputeMetadataHash(item);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeMetadataHash_IdenticalItems_ProduceSameHash()
    {
        var item1 = CreateItem();
        var item2 = CreateItem();

        Assert.Equal(
            IncrementalBuildEngine.ComputeMetadataHash(item1),
            IncrementalBuildEngine.ComputeMetadataHash(item2));
    }

    [Fact]
    public void ComputeMetadataHash_DifferentId_ProducesDifferentHash()
    {
        var item1 = CreateItem(id: "id-1");
        var item2 = CreateItem(id: "id-2");

        Assert.NotEqual(
            IncrementalBuildEngine.ComputeMetadataHash(item1),
            IncrementalBuildEngine.ComputeMetadataHash(item2));
    }

    [Fact]
    public void ComputeMetadataHash_DifferentTitle_ProducesDifferentHash()
    {
        var item1 = CreateItem(title: "Title A");
        var item2 = CreateItem(title: "Title B");

        Assert.NotEqual(
            IncrementalBuildEngine.ComputeMetadataHash(item1),
            IncrementalBuildEngine.ComputeMetadataHash(item2));
    }

    [Fact]
    public void ComputeMetadataHash_DifferentSlug_ProducesDifferentHash()
    {
        var item1 = CreateItem(slug: "slug-a");
        var item2 = CreateItem(slug: "slug-b");

        Assert.NotEqual(
            IncrementalBuildEngine.ComputeMetadataHash(item1),
            IncrementalBuildEngine.ComputeMetadataHash(item2));
    }

    [Fact]
    public void ComputeMetadataHash_DifferentPublishAt_ProducesDifferentHash()
    {
        var item1 = CreateItem(publishAt: new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var item2 = CreateItem(publishAt: new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero));

        Assert.NotEqual(
            IncrementalBuildEngine.ComputeMetadataHash(item1),
            IncrementalBuildEngine.ComputeMetadataHash(item2));
    }

    [Fact]
    public void ComputeMetadataHash_DifferentTypeMeta_ProducesDifferentHash()
    {
        var item1 = CreateItem(fieldValues: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post"
        });
        var item2 = CreateItem(fieldValues: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "page"
        });

        Assert.NotEqual(
            IncrementalBuildEngine.ComputeMetadataHash(item1),
            IncrementalBuildEngine.ComputeMetadataHash(item2));
    }

    [Fact]
    public void ComputeMetadataHash_DifferentSummary_ProducesDifferentHash()
    {
        var item1 = CreateItem(fieldValues: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["summary"] = "Summary A"
        });
        var item2 = CreateItem(fieldValues: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["summary"] = "Summary B"
        });

        Assert.NotEqual(
            IncrementalBuildEngine.ComputeMetadataHash(item1),
            IncrementalBuildEngine.ComputeMetadataHash(item2));
    }

    [Fact]
    public void ComputeMetadataHash_DifferentStructuredSummary_ProducesDifferentHash()
    {
        var item1 = CreateItem(fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["summary"] = new("text", "Summary A")
        });
        var item2 = CreateItem(fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["summary"] = new("text", "Summary B")
        });

        Assert.NotEqual(
            IncrementalBuildEngine.ComputeMetadataHash(item1),
            IncrementalBuildEngine.ComputeMetadataHash(item2));
    }

    [Fact]
    public void ComputeMetadataHash_FieldsParticipateInHash()
    {
        var item1 = CreateItem();
        var item2 = CreateItem(fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["custom"] = new("text", "value")
        });

        Assert.NotEqual(
            IncrementalBuildEngine.ComputeMetadataHash(item1),
            IncrementalBuildEngine.ComputeMetadataHash(item2));
    }

    [Fact]
    public void ComputeMetadataHash_Deterministic_AcrossMultipleCalls()
    {
        var item = CreateItem();
        var results = new HashSet<string>();
        for (var i = 0; i < 100; i++)
        {
            results.Add(IncrementalBuildEngine.ComputeMetadataHash(item));
        }

        Assert.Single(results);
    }

    [Fact]
    public void ComputeMetadataHash_WithNullFields_ProducesHash()
    {
        var item = CreateItem(fields: null);
        var hash = IncrementalBuildEngine.ComputeMetadataHash(item);

        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public async Task ComputeContentHash_WithBodyFingerprint_UsesStableHash()
    {
        var item = CreateItem(fieldValues: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["bodyFingerprint"] = "abc123def456"
        });
        var bodyStore = NullContentBodyStore.Instance;

        var hash = await IncrementalBuildEngine.ComputeContentHashAsync(item, bodyStore);

        var metadataHash = IncrementalBuildEngine.ComputeMetadataHash(item);
        var expected = HashUtil.Sha256Hex(string.Join("\n", metadataHash, "abc123def456"));

        Assert.Equal(expected, hash);
    }

    [Fact]
    public async Task ComputeContentHash_WithContentHtml_NoBodyFingerprint_FallsBackToContentHash()
    {
        var html = "<p>Hello World</p>";
        var item = CreateItem(contentHtml: html);
        var bodyStore = NullContentBodyStore.Instance;

        var hash = await IncrementalBuildEngine.ComputeContentHashAsync(item, bodyStore);

        var metadataHash = IncrementalBuildEngine.ComputeMetadataHash(item);
        var bodyFingerprint = HashUtil.Sha256Hex(html);
        var expected = HashUtil.Sha256Hex(string.Join("\n", metadataHash, bodyFingerprint));

        Assert.Equal(expected, hash);
    }

    [Fact]
    public async Task ComputeContentHash_WithBothBodyFingerprintAndContentHtml_UsesBodyFingerprint()
    {
        var item = CreateItem(
            contentHtml: "<p>Should not be used</p>",
            fieldValues: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["bodyFingerprint"] = "fingerprint-from-fieldValues"
            });
        var bodyStore = NullContentBodyStore.Instance;

        var hash = await IncrementalBuildEngine.ComputeContentHashAsync(item, bodyStore);

        var metadataHash = IncrementalBuildEngine.ComputeMetadataHash(item);
        var expected = HashUtil.Sha256Hex(string.Join("\n", metadataHash, "fingerprint-from-fieldValues"));

        Assert.Equal(expected, hash);
    }

    [Fact]
    public void ComputeContentHash_3Args_BasicHash()
    {
        var item = CreateItem();
        var metadataHash = IncrementalBuildEngine.ComputeMetadataHash(item);
        var contentHtml = "<p>Some content</p>";

        var hash = IncrementalBuildEngine.ComputeContentHash(item, metadataHash, contentHtml);

        var expected = HashUtil.Sha256Hex(string.Join("\n", metadataHash, contentHtml));
        Assert.Equal(expected, hash);
    }

    [Fact]
    public void ComputeContentHash_3Args_EmptyContentHtml()
    {
        var item = CreateItem();
        var metadataHash = IncrementalBuildEngine.ComputeMetadataHash(item);

        var hash = IncrementalBuildEngine.ComputeContentHash(item, metadataHash, string.Empty);

        var expected = HashUtil.Sha256Hex(string.Join("\n", metadataHash, string.Empty));
        Assert.Equal(expected, hash);
    }

    [Fact]
    public void ComputeContentHash_3Args_Deterministic()
    {
        var item = CreateItem();
        var metadataHash = IncrementalBuildEngine.ComputeMetadataHash(item);
        var contentHtml = "<p>Content</p>";

        var hash1 = IncrementalBuildEngine.ComputeContentHash(item, metadataHash, contentHtml);
        var hash2 = IncrementalBuildEngine.ComputeContentHash(item, metadataHash, contentHtml);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeStableContentHash_WithBodyFingerprint_ProducesHash()
    {
        var item = CreateItem(fieldValues: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["bodyFingerprint"] = "abc123"
        });
        var metadataHash = IncrementalBuildEngine.ComputeMetadataHash(item);

        var hash = IncrementalBuildEngine.ComputeStableContentHash(item, metadataHash);

        var expected = HashUtil.Sha256Hex(string.Join("\n", metadataHash, "abc123"));
        Assert.Equal(expected, hash);
    }

    [Fact]
    public void ComputeStableContentHash_WithContentHtml_FallsBackToHashOfContentHtml()
    {
        var html = "<p>Hello</p>";
        var item = CreateItem(contentHtml: html);
        var metadataHash = IncrementalBuildEngine.ComputeMetadataHash(item);

        var hash = IncrementalBuildEngine.ComputeStableContentHash(item, metadataHash);

        var expectedBodyFingerprint = HashUtil.Sha256Hex(html);
        var expected = HashUtil.Sha256Hex(string.Join("\n", metadataHash, expectedBodyFingerprint));
        Assert.Equal(expected, hash);
    }

    [Fact]
    public void ComputeStableContentHash_NoBodyFingerprint_Throws()
    {
        var item = CreateItem(contentHtml: null);
        var metadataHash = IncrementalBuildEngine.ComputeMetadataHash(item);

        var ex = Assert.Throws<InvalidOperationException>(
            () => IncrementalBuildEngine.ComputeStableContentHash(item, metadataHash));

        Assert.Contains("stable body fingerprint", ex.Message);
        Assert.Contains(item.Id, ex.Message);
    }

    [Fact]
    public void ComputeStableContentHash_EmptyContentHtml_Throws()
    {
        var item = CreateItem(contentHtml: string.Empty);
        var metadataHash = IncrementalBuildEngine.ComputeMetadataHash(item);

        Assert.Throws<InvalidOperationException>(
            () => IncrementalBuildEngine.ComputeStableContentHash(item, metadataHash));
    }

    [Fact]
    public void TryComputeStableContentHash_LocalizedBodyStore_ReturnsFalse()
    {
        var item = CreateItem(fieldValues: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["bodyFingerprint"] = "abc"
        });
        var metadataHash = IncrementalBuildEngine.ComputeMetadataHash(item);
        var bodyStore = new LocalizedContentBodyStore(NullContentBodyStore.Instance, null!);

        var result = IncrementalBuildEngine.TryComputeStableContentHash(
            item, bodyStore, metadataHash, out var contentHash);

        Assert.False(result);
        Assert.Equal(string.Empty, contentHash);
    }

    [Fact]
    public void TryComputeStableContentHash_NoBodyFingerprint_ReturnsFalse()
    {
        var item = CreateItem(contentHtml: null);
        var metadataHash = IncrementalBuildEngine.ComputeMetadataHash(item);
        var bodyStore = NullContentBodyStore.Instance;

        var result = IncrementalBuildEngine.TryComputeStableContentHash(
            item, bodyStore, metadataHash, out var contentHash);

        Assert.False(result);
        Assert.Equal(string.Empty, contentHash);
    }

    [Fact]
    public void TryComputeStableContentHash_WithBodyFingerprintMeta_ReturnsTrue()
    {
        var item = CreateItem(fieldValues: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["bodyFingerprint"] = "xyz789"
        });
        var metadataHash = IncrementalBuildEngine.ComputeMetadataHash(item);
        var bodyStore = NullContentBodyStore.Instance;

        var result = IncrementalBuildEngine.TryComputeStableContentHash(
            item, bodyStore, metadataHash, out var contentHash);

        Assert.True(result);
        var expected = HashUtil.Sha256Hex(string.Join("\n", metadataHash, "xyz789"));
        Assert.Equal(expected, contentHash);
    }

    [Fact]
    public void TryComputeStableContentHash_WithContentHtml_ReturnsTrue()
    {
        var html = "<p>Fallback content</p>";
        var item = CreateItem(contentHtml: html);
        var metadataHash = IncrementalBuildEngine.ComputeMetadataHash(item);
        var bodyStore = NullContentBodyStore.Instance;

        var result = IncrementalBuildEngine.TryComputeStableContentHash(
            item, bodyStore, metadataHash, out var contentHash);

        Assert.True(result);
        var expectedBodyFingerprint = HashUtil.Sha256Hex(html);
        var expected = HashUtil.Sha256Hex(string.Join("\n", metadataHash, expectedBodyFingerprint));
        Assert.Equal(expected, contentHash);
    }

    [Fact]
    public void TryComputeStableContentHash_WithWhitespaceBodyFingerprint_ReturnsFalse()
    {
        var item = CreateItem(fieldValues: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["bodyFingerprint"] = "   "
        });
        var metadataHash = IncrementalBuildEngine.ComputeMetadataHash(item);
        var bodyStore = NullContentBodyStore.Instance;

        var result = IncrementalBuildEngine.TryComputeStableContentHash(
            item, bodyStore, metadataHash, out var contentHash);

        Assert.False(result);
    }

    [Fact]
    public void ComputeRouteHash_Deterministic()
    {
        var route = CreateRoute();

        var hash1 = IncrementalBuildEngine.ComputeRouteHash(route);
        var hash2 = IncrementalBuildEngine.ComputeRouteHash(route);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeRouteHash_DifferentUrl_ProducesDifferentHash()
    {
        var route1 = CreateRoute(url: "/pages/alpha/");
        var route2 = CreateRoute(url: "/pages/beta/");

        Assert.NotEqual(
            IncrementalBuildEngine.ComputeRouteHash(route1),
            IncrementalBuildEngine.ComputeRouteHash(route2));
    }

    [Fact]
    public void ComputeRouteHash_DifferentOutputPath_ProducesDifferentHash()
    {
        var route1 = CreateRoute(outputPath: "pages/a/index.html");
        var route2 = CreateRoute(outputPath: "pages/b/index.html");

        Assert.NotEqual(
            IncrementalBuildEngine.ComputeRouteHash(route1),
            IncrementalBuildEngine.ComputeRouteHash(route2));
    }

    [Fact]
    public void ComputeRouteHash_DifferentTemplate_ProducesDifferentHash()
    {
        var route1 = CreateRoute(template: "pages/post.html");
        var route2 = CreateRoute(template: "pages/page.html");

        Assert.NotEqual(
            IncrementalBuildEngine.ComputeRouteHash(route1),
            IncrementalBuildEngine.ComputeRouteHash(route2));
    }

    [Fact]
    public void ComputeListContentHash_EmptySource_ProducesDeterministicHash()
    {
        var source = Array.Empty<RoutedContentDocument>();
        var manifest = new BuildManifest();
        var bodyStore = NullContentBodyStore.Instance;

        var hash1 = IncrementalBuildEngine.ComputeListContentHash(
            "template-hash", "pages/list.html", source, manifest, bodyStore, includeContent: false);
        var hash2 = IncrementalBuildEngine.ComputeListContentHash(
            "template-hash", "pages/list.html", source, manifest, bodyStore, includeContent: false);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeListContentHash_WithManifestEntries_UsesEntryHashes()
    {
        var item = CreateItem(id: "post-1", title: "Post 1");
        var route = CreateRoute(url: "/blog/post-1/", outputPath: "blog/post-1/index.html");
        var source = new[] { new RoutedContentDocument(item, route) };

        var entryOutputPath = "blog/post-1/index.html";
        var manifest = new BuildManifest
        {
            Entries = new Dictionary<string, BuildManifestEntry>(StringComparer.Ordinal)
            {
                [entryOutputPath] = new()
                {
                    OutputPath = entryOutputPath,
                    ContentHash = "manifest-content-hash",
                    RouteHash = "manifest-route-hash"
                }
            }
        };
        var bodyStore = NullContentBodyStore.Instance;

        var hash1 = IncrementalBuildEngine.ComputeListContentHash(
            "th", "pages/list.html", source, manifest, bodyStore, includeContent: false);
        var hash2 = IncrementalBuildEngine.ComputeListContentHash(
            "th", "pages/list.html", source, manifest, bodyStore, includeContent: false);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeListContentHash_WithoutManifestEntries_FallsBackToComputedHashes()
    {
        var item = CreateItem(id: "post-2", title: "Post 2");
        var route = CreateRoute(url: "/blog/post-2/", outputPath: "blog/post-2/index.html");
        var source = new[] { new RoutedContentDocument(item, route) };

        var manifest = new BuildManifest();
        var bodyStore = NullContentBodyStore.Instance;

        var hash1 = IncrementalBuildEngine.ComputeListContentHash(
            "th", "pages/list.html", source, manifest, bodyStore, includeContent: false);

        Assert.NotNull(hash1);
        Assert.NotEmpty(hash1);
        Assert.Equal(64, hash1.Length);
    }

    [Fact]
    public void ComputeListContentHash_DifferentTemplateHash_ProducesDifferentHash()
    {
        var source = Array.Empty<RoutedContentDocument>();
        var manifest = new BuildManifest();
        var bodyStore = NullContentBodyStore.Instance;

        var hash1 = IncrementalBuildEngine.ComputeListContentHash(
            "hash-a", "pages/list.html", source, manifest, bodyStore, includeContent: false);
        var hash2 = IncrementalBuildEngine.ComputeListContentHash(
            "hash-b", "pages/list.html", source, manifest, bodyStore, includeContent: false);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeListContentHash_DifferentTemplate_ProducesDifferentHash()
    {
        var source = Array.Empty<RoutedContentDocument>();
        var manifest = new BuildManifest();
        var bodyStore = NullContentBodyStore.Instance;

        var hash1 = IncrementalBuildEngine.ComputeListContentHash(
            "th", "pages/list.html", source, manifest, bodyStore, includeContent: false);
        var hash2 = IncrementalBuildEngine.ComputeListContentHash(
            "th", "pages/index.html", source, manifest, bodyStore, includeContent: false);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeListContentHash_WithIncludeContent_DifferentContentProducesDifferentHash()
    {
        var item = CreateItem(id: "post-3", title: "Post 3", contentHtml: "<p>Hello</p>");
        var route = CreateRoute(url: "/blog/post-3/", outputPath: "blog/post-3/index.html");
        var source = new[] { new RoutedContentDocument(item, route) };
        var manifest = new BuildManifest();
        var bodyStore = NullContentBodyStore.Instance;

        var hash1 = IncrementalBuildEngine.ComputeListContentHash(
            "th", "pages/list.html", source, manifest, bodyStore, includeContent: true);
        var hash2 = IncrementalBuildEngine.ComputeListContentHash(
            "th", "pages/list.html", source, manifest, bodyStore, includeContent: false);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void AppendUtf8_Null_DoesNotAddData()
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        IncrementalBuildEngine.AppendUtf8(hasher, null);

        var digest = hasher.GetHashAndReset();
        var hash = HashUtil.ToHexLower(digest);

        Assert.NotNull(hash);
    }

    [Fact]
    public void AppendUtf8_EmptyString_DoesNotAddData()
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        IncrementalBuildEngine.AppendUtf8(hasher, string.Empty);

        var digest = hasher.GetHashAndReset();
        var hash = HashUtil.ToHexLower(digest);

        using var hasher2 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        IncrementalBuildEngine.AppendUtf8(hasher2, null);

        var digest2 = hasher2.GetHashAndReset();
        var hash2 = HashUtil.ToHexLower(digest2);

        Assert.Equal(hash, hash2);
    }

    [Fact]
    public void AppendUtf8_AsciiText_ProducesCorrectHash()
    {
        using var hasher1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        IncrementalBuildEngine.AppendUtf8(hasher1, "hello");
        var hash1 = HashUtil.ToHexLower(hasher1.GetHashAndReset());

        using var hasher2 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hasher2.AppendData(System.Text.Encoding.UTF8.GetBytes("hello"));
        var hash2 = HashUtil.ToHexLower(hasher2.GetHashAndReset());

        Assert.Equal(hash2, hash1);
    }

    [Fact]
    public void AppendUtf8_UnicodeText_ProducesCorrectHash()
    {
        var text = "你好世界 🌍 café";

        using var hasher1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        IncrementalBuildEngine.AppendUtf8(hasher1, text);
        var hash1 = HashUtil.ToHexLower(hasher1.GetHashAndReset());

        using var hasher2 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hasher2.AppendData(System.Text.Encoding.UTF8.GetBytes(text));
        var hash2 = HashUtil.ToHexLower(hasher2.GetHashAndReset());

        Assert.Equal(hash2, hash1);
    }

    [Fact]
    public void AppendUtf8_DifferentText_ProducesDifferentHash()
    {
        using var hasher1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        IncrementalBuildEngine.AppendUtf8(hasher1, "alpha");
        var hash1 = HashUtil.ToHexLower(hasher1.GetHashAndReset());

        using var hasher2 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        IncrementalBuildEngine.AppendUtf8(hasher2, "beta");
        var hash2 = HashUtil.ToHexLower(hasher2.GetHashAndReset());

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void AppendUtf8_MultipleCalls_AccumulatesData()
    {
        using var hasherCombined = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        IncrementalBuildEngine.AppendUtf8(hasherCombined, "hello");
        IncrementalBuildEngine.AppendUtf8(hasherCombined, "world");
        var combinedHash = HashUtil.ToHexLower(hasherCombined.GetHashAndReset());

        using var hasherSingle = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hasherSingle.AppendData(System.Text.Encoding.UTF8.GetBytes("helloworld"));
        var singleHash = HashUtil.ToHexLower(hasherSingle.GetHashAndReset());

        Assert.Equal(singleHash, combinedHash);
    }

    [Fact]
    public async Task ComputeContentHash_2Args_WithLocalizedBodyStore_FallsBackToContentHash()
    {
        var html = "<p>Fallback</p>";
        var item = CreateItem(
            contentHtml: html,
            fieldValues: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["bodyFingerprint"] = "should-ignore-because-localized"
            });
        var bodyStore = new LocalizedContentBodyStore(NullContentBodyStore.Instance, null!);

        var hash = await IncrementalBuildEngine.ComputeContentHashAsync(item, bodyStore);

        var metadataHash = IncrementalBuildEngine.ComputeMetadataHash(item);
        var expected = HashUtil.Sha256Hex(string.Join("\n", metadataHash, html));

        Assert.Equal(expected, hash);
    }
}
#pragma warning restore CS0618
