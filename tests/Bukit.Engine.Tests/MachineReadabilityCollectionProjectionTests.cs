using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class MachineReadabilityCollectionProjectionTests : IDisposable
{
    private readonly string _outputDir = Path.Combine(
        Path.GetTempPath(),
        "bukit-machine-collection-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Build_FeedExpectationsUseCollectionAndExcludeNullOrDerivedEntries()
    {
        Directory.CreateDirectory(_outputDir);
        var entries = new[]
        {
            Entry("eligible", "article", "news"),
            Entry("disabled", "news", "disabled"),
            Entry("missing", "news", null),
            Entry("derived", "taxonomy", "news", isDerived: true)
        };
        foreach (var entry in entries)
        {
            var path = Path.Combine(_outputDir, entry.Route.OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "<html><head><title>Item</title></head><body><main>Item</main></body></html>");
        }

        var result = MachineReadabilityTrustAuditBuilder.Build(
            Config(),
            _outputDir,
            entries.ToDictionary(entry => entry.Route.OutputPath, StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase),
            CanonicalContentGraph.Empty);

        var documents = result.PublishReport.Documents.ToDictionary(document => document.RouteUrl, StringComparer.OrdinalIgnoreCase);
        AssertFeedKinds(documents["/eligible/"], rssExpected: false, otherFormatsExpected: true);
        AssertFeedKinds(documents["/disabled/"], rssExpected: false, otherFormatsExpected: false);
        AssertFeedKinds(documents["/missing/"], rssExpected: false, otherFormatsExpected: false);
        AssertFeedKinds(documents["/derived/"], rssExpected: false, otherFormatsExpected: false);
    }

    private static void AssertFeedKinds(PublishAuditDocument document, bool rssExpected, bool otherFormatsExpected)
    {
        Assert.Equal(rssExpected, document.RepresentationKinds.Contains("feed", StringComparer.OrdinalIgnoreCase));
        Assert.Equal(otherFormatsExpected, document.RepresentationKinds.Contains("atom", StringComparer.OrdinalIgnoreCase));
        Assert.Equal(otherFormatsExpected, document.RepresentationKinds.Contains("jsonfeed", StringComparer.OrdinalIgnoreCase));
    }

    private static SeoIndexEntry Entry(string id, string contentType, string? collection, bool isDerived = false)
    {
        var route = new RouteInfo($"/{id}/", $"{id}/index.html", "page.html");
        return new SeoIndexEntry(
            route,
            $"https://example.com/{id}/",
            null,
            true,
            DateTimeOffset.UnixEpoch,
            id,
            contentType,
            IsDerived: isDerived,
            Collection: collection);
    }

    private static AppConfig Config()
        => new()
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Url = "https://example.com",
                Feed = new FeedConfig { Formats = ["atom", "json"] },
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["news"] = new() { Permalink = "/news/{slug}/", Output = new() { Rss = true } },
                    ["disabled"] = new() { Permalink = "/disabled/{slug}/", Output = new() { Rss = false } }
                }
            },
            Content = TestContent.Markdown()
        };

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
        {
            Directory.Delete(_outputDir, recursive: true);
        }
    }
}
