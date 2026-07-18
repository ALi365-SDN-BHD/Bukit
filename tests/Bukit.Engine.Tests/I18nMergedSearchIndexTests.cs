using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Routing;
using Bukit.Shared;
using System.Text.Json;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class I18nMergedSearchIndexTests
{
    [Fact]
    public void GenerateRootOutputs_MergedSearchUsesRootConfiguredMaxContentLengthForEveryLanguage()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-i18n-merged-search-" + Guid.NewGuid().ToString("N"));
        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "test",
                    Title = "Test",
                    Search = new SearchDetailConfig { Mode = "merged", MaxContentLength = 6 }
                },
                Content = TestContent.Markdown()
            };
            var variants = new[]
            {
                CreateVariant("en", "/en", "en-post", "abcdefghij"),
                CreateVariant("zh", "/zh", "zh-post", "uvwxyz1234")
            };

            I18nOutputMerger.GenerateRootOutputs(
                config,
                outputDir,
                "/",
                variants,
                new ConsoleLogger(LogLevel.Error),
                new DefaultSearchIndexBuilder());

            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputDir, "search.json")));
            var items = doc.RootElement.EnumerateArray()
                .ToDictionary(item => item.GetProperty("id").GetString()!, item => item.GetProperty("content").GetString());
            Assert.Equal("abcdef", items["en-post"]);
            Assert.Equal("uvwxyz", items["zh-post"]);
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
    [InlineData("split", false)]
    [InlineData("index", true)]
    public void GenerateRootOutputs_SplitAndIndexModesKeepCappedLanguageSearchFiles(string mode, bool expectRootIndex)
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-i18n-search-mode-" + Guid.NewGuid().ToString("N"));
        try
        {
            var config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "test",
                    Title = "Test",
                    Search = new SearchDetailConfig { Mode = mode, MaxContentLength = 4 }
                },
                Content = TestContent.Markdown()
            };
            var variants = new[]
            {
                CreateVariant("en", "/en", "en-post", "abcdefghij", Path.Combine(outputDir, "en")),
                CreateVariant("zh", "/zh", "zh-post", "uvwxyz1234", Path.Combine(outputDir, "zh"))
            };
            foreach (var variant in variants)
            {
                SearchProjectionWriter.WriteSearchIndex(new PublishProjectionContext(
                    Config: config,
                    OutputDir: variant.OutputDir,
                    ContentGraph: variant.ContentGraph ?? CanonicalContentGraph.Empty,
                    SeoIndex: variant.SeoIndex,
                    SeoModels: variant.SeoModels,
                    RoutedDocuments: variant.RoutedDocuments,
                    BodyStore: variant.BodyStore,
                    BaseUrl: variant.BaseUrl,
                    SearchSnippetsEnabled: variant.SearchSnippetsEnabled,
                    ListRouteGraph: variant.ListRouteGraph,
                    DerivedDocuments: variant.DerivedDocuments));
            }

            I18nOutputMerger.GenerateRootOutputs(
                config,
                outputDir,
                "/",
                variants,
                new ConsoleLogger(LogLevel.Error),
                new DefaultSearchIndexBuilder());

            Assert.Equal("abcd", ReadOnlyContent(Path.Combine(outputDir, "en", "search.json")));
            Assert.Equal("uvwx", ReadOnlyContent(Path.Combine(outputDir, "zh", "search.json")));
            var indexPath = Path.Combine(outputDir, "search.index.json");
            Assert.Equal(expectRootIndex, File.Exists(indexPath));
            if (expectRootIndex)
            {
                var index = File.ReadAllText(indexPath);
                Assert.Contains("/en/search.json", index, StringComparison.Ordinal);
                Assert.Contains("/zh/search.json", index, StringComparison.Ordinal);
            }
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    private static BuildVariantResult CreateVariant(
        string language,
        string baseUrl,
        string id,
        string body,
        string outputDir = "")
    {
        var document = ContentDocument.Create(
            id,
            id,
            id,
            DateTimeOffset.Parse("2026-06-05T00:00:00Z"),
            body,
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "post",
                ["collection"] = "post",
                ["language"] = language
            }));
        var route = new RouteInfo($"/{id}/", $"{id}/index.html", "pages/post.html");
        return new BuildVariantResult(
            Language: language,
            OutputDir: outputDir,
            BaseUrl: baseUrl,
            SearchSnippetsEnabled: false,
            BodyStore: new DictionaryContentBodyStore(new Dictionary<string, ContentBody>
            {
                [id] = new(body)
            }),
            DerivedRoutes: Array.Empty<(RouteInfo Route, DateTimeOffset LastModified)>(),
            SeoIndex: new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase)
            {
                [route.OutputPath] = new(route, baseUrl + route.Url, null, true, document.PublishAt, document.Id, "post")
            },
            SeoModels: new Dictionary<string, Bukit.Rendering.SeoModel>(StringComparer.OrdinalIgnoreCase),
            PluginExecutions: Array.Empty<PluginExecutionInfo>(),
            RenderedCount: 1,
            SkippedCount: 0,
            RenderReasons: new Dictionary<string, int>(),
            StageMetrics: BuildStageMetrics.Empty,
            RoutedDocuments: new[] { new RoutedContentDocument(document, route) },
            ContentGraph: CanonicalContentGraph.Empty,
            ListRouteGraph: ListRouteGraph.Empty);
    }

    private static string? ReadOnlyContent(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return Assert.Single(doc.RootElement.EnumerateArray()).GetProperty("content").GetString();
    }
}
