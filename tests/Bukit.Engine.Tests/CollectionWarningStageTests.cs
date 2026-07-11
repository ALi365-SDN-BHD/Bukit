using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Stages;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class CollectionWarningStageTests
{
    private sealed class TestLogger : ILogger
    {
        public List<string> Warnings { get; } = new();
        public List<string> Infos { get; } = new();

        public void Debug(string message) { }
        public void Info(string message) => Infos.Add(message);
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message) { }
    }

    private static ContentDocument CreateDocument(string id, IReadOnlyDictionary<string, object> fields)
    {
        var fieldMap = ContentFieldReader.ToFieldMap(fields);
        return ContentDocumentNormalizer.ToDocument(new RawContentDocument(
            Id: id,
            Title: $"Item {id}",
            Slug: $"item-{id}",
            PublishAt: DateTimeOffset.UtcNow,
            Body: new RawBody(InlineHtml: "<p>hi</p>"),
            Properties: RawContentValue.FromFields(fieldMap),
            CustomFields: fieldMap));
    }

    private static ContentStageInput CreateInput(IReadOnlyList<ContentDocument> documents, ILogger logger, AppConfig? config = null)
    {
        return new ContentStageInput(
            documents,
            EmptyContentBodyStore.Instance,
            config ?? new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "T" },
                Content = TestContent.Markdown() with
                {
                    Media = new MediaConfig { DownloadToLocal = false }
                },
                Build = new BuildConfig { Output = "dist" },
                Theme = new ThemeConfig { Layouts = "layouts" }
            },
            new ConfigOverrides(),
            "/tmp/test",
            "/tmp/test-media",
            logger);
    }

    [Theory]
    [InlineData("type-only", "post", null, null)]
    [InlineData("collection-only", null, "blog", null)]
    [InlineData("same-values", "post", "post", null)]
    [InlineData("distinct-values", "post", "companies", null)]
    [InlineData("data-metadata", "settings", null, "data")]
    public async Task ExecuteAsync_ContentMetadata_DoesNotEmitWarnings(
        string id,
        string? type,
        string? collection,
        string? sourceMode)
    {
        var logger = new TestLogger();
        var fields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (type is not null)
        {
            fields["type"] = type;
        }
        if (collection is not null)
        {
            fields["collection"] = collection;
        }
        if (sourceMode is not null)
        {
            fields["sourceMode"] = sourceMode;
        }
        var document = CreateDocument(id, fields);
        var stage = new CollectionWarningStage();
        var input = CreateInput(new[] { document }, logger);

        var output = await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Empty(logger.Warnings);
        Assert.Empty(logger.Infos);
        Assert.Equal(0, output.DurationMs);
    }

    [Fact]
    public async Task ExecuteAsync_FilteredListsWithParentListRoute_EmitStaticRoutePositioningInfo()
    {
        var logger = new TestLogger();
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "t",
                Title = "T",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["companies"] = new()
                    {
                        Permalink = "/companies/{slug}/",
                        Template = "pages/company.html",
                        ListRoute = "/companies/",
                        FilteredLists = new[]
                        {
                            new FilteredListConfig
                            {
                                Field = "country",
                                Value = "Malaysia",
                                ListRoute = "/companies/malaysia/"
                            },
                            new FilteredListConfig
                            {
                                Field = "country",
                                Value = "Singapore",
                                ListRoute = "/companies/singapore/"
                            }
                        }
                    }
                }
            },
            Content = TestContent.Markdown() with
            {
                Media = new MediaConfig { DownloadToLocal = false }
            },
            Build = new BuildConfig { Output = "dist" },
            Theme = new ThemeConfig { Layouts = "layouts" }
        };
        var input = CreateInput(Array.Empty<ContentDocument>(), logger, config);
        var stage = new CollectionWarningStage();

        var output = await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Empty(logger.Warnings);
        Assert.Single(logger.Infos);
        Assert.Equal(0, output.DurationMs);
        Assert.Contains("site.collections.companies.filteredLists", logger.Infos[0], StringComparison.Ordinal);
        Assert.Contains("defines 2 manual static filtered list routes", logger.Infos[0], StringComparison.Ordinal);
        Assert.Contains("field=country", logger.Infos[0], StringComparison.Ordinal);
        Assert.Contains("value=Malaysia", logger.Infos[0], StringComparison.Ordinal);
        Assert.Contains("taxonomy.kinds", logger.Infos[0], StringComparison.Ordinal);
        Assert.Contains("only when automatically generated", logger.Infos[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_FilteredListsWithoutParentListRoute_WarnsRouteWillNotBeGenerated()
    {
        var logger = new TestLogger();
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "t",
                Title = "T",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["companies"] = new()
                    {
                        Permalink = "/companies/{slug}/",
                        Template = "pages/company.html",
                        FilteredLists = new[]
                        {
                            new FilteredListConfig
                            {
                                Field = "country",
                                Value = "Malaysia",
                                ListRoute = "/companies/malaysia/"
                            },
                            new FilteredListConfig
                            {
                                Field = "country",
                                Value = "Singapore",
                                ListRoute = "/companies/singapore/"
                            }
                        }
                    }
                }
            },
            Content = TestContent.Markdown() with
            {
                Media = new MediaConfig { DownloadToLocal = false }
            },
            Build = new BuildConfig { Output = "dist" },
            Theme = new ThemeConfig { Layouts = "layouts" }
        };
        var input = CreateInput(Array.Empty<ContentDocument>(), logger, config);
        var stage = new CollectionWarningStage();

        var output = await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Equal(2, logger.Warnings.Count);
        Assert.Empty(logger.Infos);
        Assert.Equal(0, output.DurationMs);
        Assert.Contains("site.collections.companies.filteredLists[0]", logger.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("site.collections.companies.filteredLists[1]", logger.Warnings[1], StringComparison.Ordinal);
        Assert.Contains("site.collections.companies.listRoute is missing", logger.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("will not be generated", logger.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("Add listRoute", logger.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("taxonomy.kinds", logger.Warnings[0], StringComparison.Ordinal);
    }
}
