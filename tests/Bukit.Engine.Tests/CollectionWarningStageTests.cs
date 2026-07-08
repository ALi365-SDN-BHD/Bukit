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

        public void Debug(string message) { }
        public void Info(string message) { }
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

    [Fact]
    public async Task ExecuteAsync_TypePostWithoutCollection_EmitsWarning()
    {
        var logger = new TestLogger();
        var document = CreateDocument("my-post", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post"
        });
        var stage = new CollectionWarningStage();
        var input = CreateInput(new[] { document }, logger);

        await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Single(logger.Warnings);
        Assert.Contains("[WARN]", logger.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("my-post", logger.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("post", logger.Warnings[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TypePageWithoutCollection_EmitsWarning()
    {
        var logger = new TestLogger();
        var document = CreateDocument("my-page", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "page"
        });
        var stage = new CollectionWarningStage();
        var input = CreateInput(new[] { document }, logger);

        await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Single(logger.Warnings);
        Assert.Contains("[WARN]", logger.Warnings[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CollectionWithoutExplicitType_NoWarning()
    {
        var logger = new TestLogger();
        var document = CreateDocument("with-col", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["collection"] = "blog"
        });
        var stage = new CollectionWarningStage();
        var input = CreateInput(new[] { document }, logger);

        await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public async Task ExecuteAsync_CustomTypeWithoutCollection_NoWarning()
    {
        var logger = new TestLogger();
        var document = CreateDocument("custom", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "custom"
        });
        var stage = new CollectionWarningStage();
        var input = CreateInput(new[] { document }, logger);

        await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Single(logger.Warnings);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleItems_MultipleWarnings()
    {
        var logger = new TestLogger();
        var items = new[]
        {
            CreateDocument("a", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["type"] = "post" }),
            CreateDocument("b", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["collection"] = "news", ["type"] = "post" }),
            CreateDocument("c", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["type"] = "page" }),
        };
        var stage = new CollectionWarningStage();
        var input = CreateInput(items, logger);

        await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Equal(3, logger.Warnings.Count);
        Assert.Contains(logger.Warnings, w => w.Contains("\"a\"", StringComparison.Ordinal));
        Assert.Contains(logger.Warnings, w => w.Contains("[WARN]", StringComparison.Ordinal));
        Assert.Contains(logger.Warnings, w => w.Contains("\"b\"", StringComparison.Ordinal));
        Assert.Contains(logger.Warnings, w => w.Contains("\"c\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_TypePostWithCollection_EmitsConflictWarning()
    {
        var logger = new TestLogger();
        var document = CreateDocument("my-conflict", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post",
            ["collection"] = "companies"
        });
        var stage = new CollectionWarningStage();
        var input = CreateInput(new[] { document }, logger);

        await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Single(logger.Warnings);
        Assert.Contains("[WARN]", logger.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("type=post", logger.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("collection=companies", logger.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("Collection routing uses collection", logger.Warnings[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CustomTypeWithCollection_EmitsConflictWarning()
    {
        var logger = new TestLogger();
        var document = CreateDocument("my-custom", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "custom",
            ["collection"] = "companies"
        });
        var stage = new CollectionWarningStage();
        var input = CreateInput(new[] { document }, logger);

        await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.NotEmpty(logger.Warnings);
    }

    [Fact]
    public async Task ExecuteAsync_InternalRecordTypeWithSourceCollection_NoWarning()
    {
        var logger = new TestLogger();
        var fieldMap = ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["collection"] = "post"
        });
        var document = ContentDocumentNormalizer.ToDocument(new RawContentDocument(
            Id: "source-post",
            Title: "Source Post",
            Slug: "source-post",
            PublishAt: DateTimeOffset.UtcNow,
            Body: new RawBody(InlineHtml: "<p>hi</p>"),
            Properties: RawContentValue.FromFields(fieldMap),
            CustomFields: fieldMap));
        var stage = new CollectionWarningStage();
        var input = CreateInput(new[] { document }, logger);

        await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Equal("post", ContentFieldReader.GetContentType(document));
        Assert.Equal("post", ContentFieldReader.GetCollection(document));
        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public async Task ExecuteAsync_FieldOnlyTypeWithoutCollection_EmitsWarning()
    {
        var logger = new TestLogger();
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = new("text", "post")
        };
        var document = ContentDocumentNormalizer.ToDocument(new RawContentDocument(
            Id: "field-post",
            Title: "Field Post",
            Slug: "field-post",
            PublishAt: DateTimeOffset.UtcNow,
            Body: new RawBody(InlineHtml: "<p>hi</p>"),
            Properties: RawContentValue.FromFields(fields),
            CustomFields: fields));
        var stage = new CollectionWarningStage();
        var input = CreateInput(new[] { document }, logger);

        await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Single(logger.Warnings);
        Assert.Contains("type=post", logger.Warnings[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_FilteredLists_EmitStaticRoutePositioningWarning()
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

        await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Single(logger.Warnings);
        Assert.Contains("site.collections.companies.filteredLists[0]", logger.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("manual static filtered list route", logger.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("field=country", logger.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("value=Malaysia", logger.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("taxonomy.kinds", logger.Warnings[0], StringComparison.Ordinal);
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

        await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Single(logger.Warnings);
        Assert.Contains("site.collections.companies.filteredLists[0]", logger.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("site.collections.companies.listRoute is missing", logger.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("will not be generated", logger.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("Add listRoute", logger.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("taxonomy.kinds", logger.Warnings[0], StringComparison.Ordinal);
    }
}
