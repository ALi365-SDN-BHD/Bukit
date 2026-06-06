using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Stages;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ContentStagesTests
{
    private static ContentDocument Document(string id, string slug, IReadOnlyDictionary<string, object> fields) =>
        ContentDocumentNormalizer.ToDocument(new RawContentDocument(
            id,
            id,
            slug,
            DateTimeOffset.UnixEpoch,
            $"<p>{id}</p>",
            ContentFieldReader.ToFieldMap(fields)));

    private static AppConfig Config(bool draft = false) => new()
    {
        Site = new SiteConfig { Name = "test", Title = "Test" },
        Content = new ContentConfig { Provider = "markdown", Markdown = new MarkdownConfig { Dir = "content" } },
        Build = new BuildConfig { Draft = draft }
    };

    private static ConfigOverrides NoOverrides => new();

    [Fact]
    public async Task ContentLoadStage_RoutesToProviderFactory()
    {
        var loadResult = new RawContentLoadResult(
            new[]
            {
                new RawContentDocument(
                    "a",
                    "a",
                    "a",
                    DateTimeOffset.UnixEpoch,
                    "<p>a</p>",
                    ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["type"] = "page" }))
            },
            EmptyContentBodyStore.Instance);
        var factory = new StubContentProviderFactory(loadResult);
        var stage = new ContentLoadStage(factory);
        var input = new ContentStageInput(Array.Empty<ContentDocument>(), EmptyContentBodyStore.Instance, Config(), NoOverrides, "/root", "/cache", new NoOpLogger());

        var output = await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Single(output.Documents);
        Assert.Equal("a", output.Documents[0].Id);
        Assert.True(output.DurationMs >= 0);
        Assert.Equal(stage.Name, output.StageName);
    }

    [Fact]
    public void ContentDocumentNormalizer_MapsRawBodySourcePoliciesAndDiagnostics()
    {
        var raw = new RawContentDocument(
            Id: "doc-1",
            Title: "Doc",
            Slug: "doc",
            PublishAt: DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            Body: new RawBody(
                InlineHtml: "<p>Doc</p>",
                BodyKey: "body-1",
                Markdown: "# Doc",
                PlainText: "Doc"),
            Properties: new Dictionary<string, RawContentValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", "post"),
                ["url"] = new("text", "/docs/doc/"),
                ["template"] = new("text", "article"),
                ["draft"] = new("bool", true),
                ["sourceMode"] = new("text", "data"),
                ["unknown"] = new("text", "value")
            },
            Source: new ContentSourceInfo(
                Provider: "markdown",
                SourceKey: "docs",
                SourcePath: "content/doc.md",
                ExternalId: "doc-1",
                ExternalUrl: new Uri("https://example.com/doc"),
                SyncedAt: DateTimeOffset.Parse("2026-06-02T00:00:00Z"),
                SyncStatus: "synced"));
        var schema = new ContentModelSchema(
            CanonicalMappings: new Dictionary<string, CanonicalFieldMapping>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("type")
            },
            CustomFields: new Dictionary<string, CustomFieldDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["url"] = new("url", "text"),
                ["template"] = new("template", "text"),
                ["draft"] = new("draft", "bool"),
                ["sourceMode"] = new("sourceMode", "text")
            },
            RejectUnknownRawKeys: true);

        var document = ContentDocumentNormalizer.ToDocument(raw, schema);

        Assert.Equal("<p>Doc</p>", document.Body.Html);
        Assert.Equal("body-1", document.Body.BodyKey);
        Assert.Equal("# Doc", document.Body.Markdown);
        Assert.Equal("Doc", document.Body.PlainText);
        Assert.Equal("markdown", document.Source.Provider);
        Assert.Equal("content/doc.md", document.Source.SourcePath);
        Assert.Equal("/docs/doc/", document.Route.Url);
        Assert.Equal("article", document.Route.Template);
        Assert.True(document.Publish.Draft);
        Assert.True(document.Publish.IsDataModule);
        Assert.Contains(document.Diagnostics, diagnostic =>
            diagnostic.Code == "content.unknown_raw_key" &&
            diagnostic.Field == "unknown" &&
            diagnostic.SourceId == "doc-1");
    }

    [Fact]
    public async Task DraftFilterStage_RemovesDraftItems()
    {
        var published = Document("published", "pub", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase));
        var draft = Document("draft", "draft", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["draft"] = true
        });
        var stage = new DraftFilterStage();
        var input = new ContentStageInput(new[] { published, draft }, EmptyContentBodyStore.Instance, Config(draft: false), NoOverrides, "/root", "/cache", new NoOpLogger());

        var output = await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Single(output.Documents);
        Assert.Equal("published", output.Documents[0].Id);
        Assert.Equal("DraftFilter", output.StageName);
    }

    [Fact]
    public async Task DraftFilterStage_DraftMode_KeepsAll()
    {
        var published = Document("published", "pub", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase));
        var draft = Document("draft", "draft", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["draft"] = true
        });
        var stage = new DraftFilterStage();
        var input = new ContentStageInput(new[] { published, draft }, EmptyContentBodyStore.Instance, Config(draft: true), NoOverrides, "/root", "/cache", new NoOpLogger());

        var output = await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Equal(2, output.Documents.Count);
    }

    [Fact]
    public async Task ContentGraphValidateStage_WarnMode_CollectsCanonicalErrors()
    {
        var document = Document("a", "a", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post",
            ["status"] = "bad-status"
        });
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test"
            },
            Content = new ContentConfig { Provider = "markdown", Markdown = new MarkdownConfig() },
            Build = new BuildConfig { SchemaFailMode = "warn" }
        };
        var stage = new ContentGraphValidateStage();
        var input = new ContentStageInput(new[] { document }, EmptyContentBodyStore.Instance, config, NoOverrides, "/root", "/cache", new NoOpLogger());

        var output = await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.NotNull(output.SchemaErrors);
        Assert.Contains(output.SchemaErrors, e => e.Code == "canonical_status_invalid");
        Assert.Equal("ContentGraphValidate", output.StageName);
    }

    [Fact]
    public async Task ContentGraphValidateStage_StrictMode_Throws()
    {
        var document = Document("a", "a", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post",
            ["status"] = "bad-status"
        });
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test"
            },
            Content = new ContentConfig { Provider = "markdown", Markdown = new MarkdownConfig() },
            Build = new BuildConfig { SchemaFailMode = "strict" }
        };
        var stage = new ContentGraphValidateStage();
        var input = new ContentStageInput(new[] { document }, EmptyContentBodyStore.Instance, config, NoOverrides, "/root", "/cache", new NoOpLogger());

        await Assert.ThrowsAsync<ConfigException>(() => stage.ExecuteAsync(input, CancellationToken.None));
    }

    [Fact]
    public async Task ContentPipeline_WithExplicitStages_ExecutesInOrder()
    {
        var order = new List<string>();
        var stages = new IContentStage[]
        {
            new RecordingStage("stage1", order),
            new RecordingStage("stage2", order),
            new RecordingStage("stage3", order)
        };
        var pipeline = new ContentPipeline(stages, new NoOpLogger());
        var config = Config(draft: true);
        var document = Document("a", "a", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase));
        var input = new ContentStageInput(new[] { document }, EmptyContentBodyStore.Instance, config, NoOverrides, "/root", "/cache", new NoOpLogger());

        var result = await pipeline.ExecuteAsync(input, CancellationToken.None);

        Assert.Empty(result.SchemaErrors);
        Assert.Equal(3, order.Count);
        Assert.Equal("stage1", order[0]);
        Assert.Equal("stage2", order[1]);
        Assert.Equal("stage3", order[2]);
    }

    [Fact]
    public async Task ContentPipeline_WithDiagnosticCode()
    {
        var stages = new IContentStage[]
        {
            new ThrowingStage(DiagnosticCode.ContentLoadFailed)
        };
        var pipeline = new ContentPipeline(stages, new NoOpLogger());
        var config = Config();
        var input = new ContentStageInput(Array.Empty<ContentDocument>(), EmptyContentBodyStore.Instance, config, NoOverrides, "/root", "/cache", new NoOpLogger());

        var ex = await Assert.ThrowsAsync<ConfigException>(() => pipeline.ExecuteAsync(input, CancellationToken.None));

        Assert.Equal(DiagnosticCode.ContentLoadFailed, ex.Code);
    }

    private sealed class RecordingStage : IContentStage
    {
        private readonly string _name;
        private readonly List<string> _order;

        public RecordingStage(string name, List<string> order)
        {
            _name = name;
            _order = order;
        }

        public string Name => _name;

        public Task<ContentStageOutput> ExecuteAsync(ContentStageInput input, CancellationToken cancellationToken)
        {
            _order.Add(_name);
            return Task.FromResult(new ContentStageOutput(input.Documents, input.BodyStore, Name, 0, null));
        }
    }

    private sealed class ThrowingStage : IContentStage
    {
        private readonly DiagnosticCode _code;

        public ThrowingStage(DiagnosticCode code) => _code = code;

        public string Name => "thrower";

        public Task<ContentStageOutput> ExecuteAsync(ContentStageInput input, CancellationToken cancellationToken)
        {
            throw new ConfigException("test failure", _code);
        }
    }

    private sealed class StubContentProviderFactory : IContentProviderFactory
    {
        private readonly RawContentLoadResult _result;

        public StubContentProviderFactory(RawContentLoadResult result) => _result = result;

        public IContentProvider Create(AppConfig config, string rootDir, bool isCi, ILogger logger)
        {
            return new StubContentProvider(_result);
        }

        public Task<RawContentLoadResult> LocalizeContentImagesAsync(RawContentLoadResult result, MediaConfig media, string rootDir, string cacheDir, ILogger logger, CancellationToken cancellationToken)
        {
            return Task.FromResult(result);
        }
    }

    private sealed class StubContentProvider : IContentProvider
    {
        private readonly RawContentLoadResult _result;

        public StubContentProvider(RawContentLoadResult result) => _result = result;

        public Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ToRawResult(_result));
        }
    }

    private static RawContentLoadResult ToRawResult(RawContentLoadResult result) => result;

    private sealed class NoOpLogger : ILogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
