using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class I18nRootProjectionWriterRegistryTests
{
    [Fact]
    public void GenerateRootOutputs_UsesDeterministicProductionWriterPlanForEveryRootRepresentation()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-i18n-root-writers-" + Guid.NewGuid().ToString("N"));

        try
        {
            var registry = I18nRootProjectionWriterRegistry.CreateDefault();
            Assert.Equal(
                [
                    ("feed", typeof(I18nRootFeedWriter)),
                    ("atom", typeof(I18nRootFeedWriter)),
                    ("jsonfeed", typeof(I18nRootFeedWriter)),
                    ("sitemap", typeof(I18nRootSitemapWriter)),
                    ("search", typeof(I18nRootSearchWriter)),
                    ("llms", typeof(I18nRootLlmsWriter)),
                    ("llms-full", typeof(I18nRootLlmsWriter)),
                    ("robots", typeof(I18nRootRobotsWriter)),
                    ("agent-manifest", typeof(I18nRootAgentManifestWriter))
                ],
                registry.BuildPlan(PublishRepresentationRegistry.AggregateRepresentations())
                    .Select(entry => (entry.Representation.Kind, entry.Writer.GetType())));

            var projections = I18nOutputMerger.GenerateRootOutputs(
                CreateEnabledConfig(),
                outputDir,
                "/",
                Array.Empty<BuildVariantResult>(),
                new ConsoleLogger(LogLevel.Error),
                new DefaultSearchIndexBuilder());

            Assert.Equal(
                PublishRepresentationRegistry.AggregateRepresentations().Select(x => x.Kind),
                projections.Select(x => x.Representation.Kind));
            Assert.All(projections, projection => Assert.True(Assert.Single(projection.Outputs).Exists));
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("SITEMAP", "mixed-sitemap.xml")]
    [InlineData("FEED", "mixed-feed.xml")]
    [InlineData("SEARCH", "mixed-search.json")]
    [InlineData("ROBOTS", "mixed-robots.txt")]
    [InlineData("AGENT-MANIFEST", "mixed-agent-manifest.json")]
    [InlineData("completely-unknown", "unknown.txt")]
    public void ProjectRootAggregate_MixedCaseOrUnknownKind_IsNoOpWithInventoryResult(
        string kind,
        string path)
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-i18n-root-noop-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(outputDir);
            var representation = new PublishRepresentation(kind, path, IsAggregate: true);
            var context = new PublishProjectionContext(
                Config: CreateEnabledConfig(),
                OutputDir: outputDir,
                ContentGraph: CanonicalContentGraph.Empty,
                SeoIndex: new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase),
                SeoModels: new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase),
                RoutedDocuments: Array.Empty<RoutedContentDocument>(),
                BaseUrl: "/",
                Logger: new ConsoleLogger(LogLevel.Error),
                VariantResults: Array.Empty<BuildVariantResult>());

            var projection = I18nOutputMerger.ProjectRootAggregate(context, representation);

            Assert.Empty(Directory.EnumerateFiles(outputDir, "*", SearchOption.AllDirectories));
            Assert.Same(representation, projection.Representation);
            var output = Assert.Single(projection.Outputs);
            Assert.Equal(kind, output.Kind);
            Assert.Equal("/" + path, output.Url);
            Assert.Equal(path, output.Path);
            Assert.False(output.Exists);
            Assert.False(output.Indexable);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    private static AppConfig CreateEnabledConfig()
        => new()
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Url = "https://example.com",
                SitemapMode = "merged",
                Feed = new FeedConfig { Mode = "merged", Formats = ["rss", "atom", "json"] },
                Search = new SearchDetailConfig { Mode = "merged" },
                Seo = new SeoConfig
                {
                    Geo = new SeoGeoConfig { Enabled = true, LlmsTxt = true, LlmsFullTxt = true },
                    RobotsTxt = new SeoRobotsTxtConfig { Enabled = true }
                }
            },
            Content = TestContent.Markdown()
        };
}
