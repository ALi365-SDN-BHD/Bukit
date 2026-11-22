using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ContentMediaOutputNestedFieldTests
{
    // A localized image inside an object field is rewritten and copied, as a top-level one is.
    [Fact]
    public void NormalizeDocument_RewritesAndCollectsLocalizedMediaInsideObjectFields()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bukit-media-nested-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "media"));
        File.WriteAllText(Path.Combine(root, "media", "day.jpg"), "image");
        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "t", BaseUrl = "/", Url = "https://example.com" },
                Content = new ContentConfig { Media = new MediaConfig { DownloadToLocal = true } }
            };
            var context = new BuildVariantContext(
                config,
                root,
                new ConfigOverrides(),
                [],
                CanonicalContentGraph.Empty,
                NullContentBodyStore.Instance,
                Path.Combine(root, "dist"),
                "/",
                root,
                root,
                root,
                Path.Combine(root, "media"),
                new Dictionary<string, IReadOnlyList<Bukit.Rendering.SeoAlternateModel>>(),
                "/",
                "en",
                "en",
                DateTimeOffset.UnixEpoch);
            var output = new ContentMediaOutput(context);
            var day = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["title"] = "D1",
                ["photos"] = new List<object?> { "/assets/uploads/day.jpg", null }
            };
            var document = ContentDocument.Create(
                "page",
                "Page",
                "page",
                DateTimeOffset.UnixEpoch,
                contentHtml: null,
                fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                {
                    ["days"] = new ContentField("list", new List<object?> { day })
                });

            var normalized = output.NormalizeDocument(document, collect: true);

            var days = Assert.IsType<List<object?>>(normalized.CustomFields!["days"].Value);
            var normalizedDay = Assert.IsType<Dictionary<string, object?>>(days[0]);
            Assert.Equal("D1", normalizedDay["TITLE"]);
            Assert.Equal(new object?[] { "/assets/uploads/day.jpg", null }, Assert.IsType<List<object?>>(normalizedDay["photos"]));
            var planned = Assert.Single(output.Plan(new ConsoleLogger(LogLevel.Error), CancellationToken.None));
            Assert.Equal("assets/uploads/day.jpg", planned.Destination);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
