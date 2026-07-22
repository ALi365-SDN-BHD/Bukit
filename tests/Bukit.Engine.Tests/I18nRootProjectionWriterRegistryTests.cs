using Bukit.Config;
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
                ["sitemap", "feeds", "search", "llms", "robots", "agent-manifest"],
                registry.Writers.Select(writer => writer.Name));
            Assert.Equal(
                [
                    ("feed", "feeds"),
                    ("atom", "feeds"),
                    ("jsonfeed", "feeds"),
                    ("sitemap", "sitemap"),
                    ("search", "search"),
                    ("llms", "llms"),
                    ("llms-full", "llms"),
                    ("robots", "robots"),
                    ("agent-manifest", "agent-manifest")
                ],
                registry.BuildPlan(PublishRepresentationRegistry.AggregateRepresentations())
                    .Select(entry => (entry.Representation.Kind, entry.Writer.Name)));

            var config = new AppConfig
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

            var projections = I18nOutputMerger.GenerateRootOutputs(
                config,
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
}
