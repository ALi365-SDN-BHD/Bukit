using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ContentPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_LoadsLocalizesFiltersDraftsAndAppliesSchemaDefaults()
    {
        var published = Item("published", "published", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post"
        });
        var draft = Item("draft", "draft", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post",
            ["draft"] = true
        });
        var loadResult = new ContentLoadResult(new[] { published, draft }, EmptyContentBodyStore.Instance);
        var factory = new RecordingContentProviderFactory(loadResult);
        var logger = new RecordingLogger();
        var config = Config(draft: false, schemaFailMode: "warn");
        var pipeline = new ContentPipeline(factory, logger);

        var result = await pipeline.ExecuteAsync(config, "/tmp/site", new ConfigOverrides { IsCI = true }, "/tmp/site/.cache/media", CancellationToken.None);

        Assert.True(factory.CreateCalled);
        Assert.True(factory.LocalizeCalled);
        Assert.True(factory.IsCiObserved);
        Assert.Equal("/tmp/site", factory.RootDirObserved);
        var item = Assert.Single(result.Items);
        Assert.Equal("published", item.Id);
        Assert.Equal("published", item.Fields!["status"].Value);
        var record = Assert.Single(result.ContentGraph!.Records);
        Assert.Equal("published", record.Identity.Id);
        Assert.Equal("published", record.Trust.ReviewStatus);
        Assert.Equal("post", record.Classification.Collection);
        Assert.NotNull(result.BodyStore);
        Assert.NotNull(result.BodyCacheMetrics);
        Assert.Empty(result.SchemaErrors);
        Assert.Contains(logger.Infos, message => message.StartsWith("event=content.loaded count=", StringComparison.Ordinal));
        Assert.Contains(logger.Infos, message => message == "event=content.draft_filtered removed=1");
    }

    [Fact]
    public async Task ExecuteAsync_WhenSchemaStrict_ThrowsConfigException()
    {
        var item = Item("missing-status", "missing-status", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post"
        });
        var factory = new RecordingContentProviderFactory(new ContentLoadResult(new[] { item }, EmptyContentBodyStore.Instance));
        var logger = new RecordingLogger();
        var config = Config(draft: true, schemaFailMode: "strict", requiredStatus: true);
        var pipeline = new ContentPipeline(factory, logger);

        var ex = await Assert.ThrowsAsync<ConfigException>(() => pipeline.ExecuteAsync(config, "/tmp/site", new ConfigOverrides(), "/tmp/site/.cache/media", CancellationToken.None));

        Assert.Equal("Schema validation failed with 1 error(s).", ex.Message);
        Assert.Contains(logger.Warnings, message => message.Contains("event=schema.validation", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_BuildsCanonicalContentGraph()
    {
        var localized = Item("localized-post", "localized-post", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post",
            ["collection"] = "post",
            ["summary"] = "Canonical summary",
            ["author"] = "Ali",
            ["language"] = "ms",
            ["tags"] = new[] { "bukit", "content" },
            ["categories"] = new[] { "guides" },
            ["source"] = "notion",
            ["original_url"] = "https://example.com/original",
            ["review_status"] = "approved",
            ["entities"] = new object[]
            {
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["type"] = "company",
                    ["name"] = "Bukit"
                }
            }
        });

        var factory = new RecordingContentProviderFactory(new ContentLoadResult(new[] { localized }, EmptyContentBodyStore.Instance));
        var pipeline = new ContentPipeline(factory, new RecordingLogger());

        var result = await pipeline.ExecuteAsync(Config(draft: true, schemaFailMode: "warn"), "/tmp/site", new ConfigOverrides(), "/tmp/site/.cache/media", CancellationToken.None);

        var record = Assert.Single(result.ContentGraph!.Records);
        Assert.Equal("localized-post", record.Identity.Id);
        Assert.Equal("post", record.Identity.ContentType);
        Assert.Equal("Canonical summary", record.Presentation.Summary);
        Assert.Equal("Ali", record.Ownership.Author);
        Assert.Equal("ms", record.Presentation.Language);
        Assert.Contains("bukit", record.Classification.Tags);
        Assert.Contains(record.Entities, x => x.Name == "Bukit" && x.Type == "company");
        Assert.Equal("https://example.com/original", record.Provenance.OriginalSource);
        Assert.Equal("approved", record.Trust.ReviewStatus);
    }

    [Fact]
    public async Task ExecuteAsync_BuildsContentDocumentsFromCanonicalGraph()
    {
        var item = Item("doc-post", "doc-post", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post",
            ["collection"] = "post",
            ["summary"] = "Document summary",
            ["draft"] = false
        });
        var factory = new RecordingContentProviderFactory(new ContentLoadResult(new[] { item }, EmptyContentBodyStore.Instance));
        var pipeline = new ContentPipeline(factory, new RecordingLogger());

        var result = await pipeline.ExecuteAsync(Config(draft: true, schemaFailMode: "warn"), "/tmp/site", new ConfigOverrides(), "/tmp/site/.cache/media", CancellationToken.None);

        var document = Assert.Single(result.Documents);
        Assert.Equal("doc-post", document.Record.Identity.Id);
        Assert.Equal("Document summary", document.Record.Presentation.Summary);
        Assert.Equal("<p>doc-post</p>", document.Body.Html);
        Assert.False(document.Publish.Draft);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderSupportsRawContent_NormalizesDocumentsBeforeLegacyItems()
    {
        var raw = new RawContentDocument(
            SourceId: "raw-post",
            SourceKind: "post",
            Title: "Raw Post",
            Slug: "raw-post",
            PublishedAt: DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            Body: new RawBody("<p>Raw</p>", null, "# Raw", "Raw"),
            Properties: new Dictionary<string, RawContentValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["summary"] = new("text", "Raw summary"),
                ["author"] = new("text", "Ali"),
                ["language"] = new("text", "en"),
                ["review_status"] = new("text", "approved"),
                ["noindex"] = new("boolean", true)
            },
            Source: new ContentSourceInfo("markdown", "content", "content/raw.md", "raw-post", null, DateTimeOffset.Parse("2026-06-02T00:00:00Z"), "synced"),
            CustomFields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["seo_title"] = new("text", "Raw SEO Title")
            });
        var provider = new RawOnlyContentProvider(new RawContentLoadResult(new[] { raw }, EmptyContentBodyStore.Instance));
        var factory = new RawContentProviderFactory(provider);
        var pipeline = new ContentPipeline(factory, new RecordingLogger());

        var result = await pipeline.ExecuteAsync(Config(draft: true, schemaFailMode: "warn"), "/tmp/site", new ConfigOverrides(), "/tmp/site/.cache/media", CancellationToken.None);

        Assert.True(provider.LoadRawCalled);
        Assert.False(provider.LoadCalled);
        var document = Assert.Single(result.Documents);
        Assert.Equal("raw-post", document.Record.Identity.Id);
        Assert.Equal("Raw summary", document.Record.Presentation.Summary);
        Assert.Equal("Ali", document.Record.Ownership.Author);
        Assert.Equal("en", document.Record.Presentation.Language);
        Assert.Equal("approved", document.Record.Trust.ReviewStatus);
        Assert.True(document.Publish.NoIndex);
        Assert.Equal("Raw SEO Title", document.CustomFields["seo_title"].Value);
        Assert.Equal(new[] { document.Record }, result.ContentGraph!.Records);
    }

    [Fact]
    public void BuildFromDocuments_UsesContentDocumentsAsCanonicalGraphSource()
    {
        var entity = new EntityRecord("company", "Bukit", null, "company:bukit");
        var relation = new ContentRelation("mentions", "Bukit");
        var record = new ContentRecord(
            Identity: new ContentIdentity("doc-1", "doc-1", "doc-1", "post", "published"),
            Presentation: new ContentPresentation("Doc 1", "Summary", "<p>Doc 1</p>", "en", Array.Empty<string>()),
            Classification: new ContentClassification("post", "post", Array.Empty<string>(), Array.Empty<string>()),
            Ownership: new ContentOwnership("Ali", null, null, null),
            Lifecycle: new ContentLifecycle(DateTimeOffset.UnixEpoch, null, null, null),
            Provenance: new ProvenanceRecord("markdown", null, Array.Empty<string>(), Array.Empty<string>(), null),
            Trust: new TrustMetadata(null, "approved", Array.Empty<string>()),
            Entities: new[] { entity },
            Relations: new[] { relation },
            Media: Array.Empty<MediaAsset>());
        var document = new ContentDocument(
            record,
            new ContentBodyRef("<p>Doc 1</p>", null, null, null),
            new ContentRoutePolicy(null, null, null, null, "post"),
            new ContentPublishPolicy(false, false, false, false, false, false, false),
            new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<ContentDiagnostic>());

        var graph = CanonicalContentGraphBuilder.BuildFromDocuments(new[] { document });

        Assert.Equal(new[] { document }, graph.Documents);
        Assert.Equal(new[] { record }, graph.Records);
        Assert.Equal(new[] { entity }, graph.Entities);
        Assert.Equal(new[] { relation }, graph.Relations);
    }

    [Fact]
    public async Task ExecuteAsync_BuildsCanonicalContentGraphFromStructuredFields()
    {
        var item = new ContentItem(
            "notion-post",
            "Notion Post",
            "notion-post",
            DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            "<p>Body</p>",
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "post",
                ["collection"] = "post"
            },
            new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["summary"] = new("text", "Field summary"),
                ["authors"] = new("list", new List<string> { "Ali" }),
                ["language"] = new("text", "en"),
                ["review_status"] = new("text", "approved"),
                ["source"] = new("text", "notion"),
                ["companies"] = new("list", new List<string> { "Bukit" }),
                ["related_posts_links"] = new("list", new List<Dictionary<string, object?>>
                {
                    new(StringComparer.OrdinalIgnoreCase)
                    {
                        ["id"] = "rel-1",
                        ["title"] = "Related One",
                        ["type"] = "post"
                    }
                }),
                ["gallery"] = new("files", new List<string> { "https://img.example/1.jpg", "https://img.example/2.jpg" })
            });

        var factory = new RecordingContentProviderFactory(new ContentLoadResult(new[] { item }, EmptyContentBodyStore.Instance));
        var pipeline = new ContentPipeline(factory, new RecordingLogger());

        var result = await pipeline.ExecuteAsync(Config(draft: true, schemaFailMode: "warn"), "/tmp/site", new ConfigOverrides(), "/tmp/site/.cache/media", CancellationToken.None);

        var record = Assert.Single(result.ContentGraph!.Records);
        Assert.Equal("Field summary", record.Presentation.Summary);
        Assert.Equal("Ali", record.Ownership.Author);
        Assert.Equal("en", record.Presentation.Language);
        Assert.Equal("approved", record.Trust.ReviewStatus);
        Assert.Equal("notion", record.Provenance.Source);
        Assert.Contains(record.Entities, x => x.Name == "Bukit" && x.Type == "company");
        Assert.Contains(record.Relations, x => x.Type == "related-posts" && x.Target == "Related One");
        Assert.Contains(record.Media, x => x.Kind == "image" && x.Url == "https://img.example/1.jpg");
        Assert.Contains(record.Media, x => x.Kind == "image" && x.Url == "https://img.example/2.jpg");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportCanonicalMediaAltGap_WhenImageHasNoAltText()
    {
        var item = new ContentItem(
            "image-post",
            "Image Post",
            "image-post",
            DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            "<p>Body</p>",
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "post",
                ["collection"] = "post",
                ["source"] = "markdown"
            },
            new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["image"] = new("file", "https://img.example/cover.jpg")
            });

        var factory = new RecordingContentProviderFactory(new ContentLoadResult(new[] { item }, EmptyContentBodyStore.Instance));
        var pipeline = new ContentPipeline(factory, new RecordingLogger());

        var result = await pipeline.ExecuteAsync(Config(draft: true, schemaFailMode: "warn"), "/tmp/site", new ConfigOverrides(), "/tmp/site/.cache/media", CancellationToken.None);

        Assert.Contains(result.SchemaErrors, error =>
            error.Code == "canonical_media_alt_missing" &&
            error.Field == "media.alt" &&
            error.SourcePath == "image-post");
    }

    private static ContentItem Item(string id, string slug, IReadOnlyDictionary<string, object> fields)
    {
        return new ContentItem(
            id,
            id,
            slug,
            DateTimeOffset.UnixEpoch,
            $"<p>{id}</p>",
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            fields.ToDictionary(kv => kv.Key, kv => new ContentField("test", kv.Value), StringComparer.OrdinalIgnoreCase));
    }

    private static AppConfig Config(bool draft, string schemaFailMode, bool requiredStatus = false)
    {
        return new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["post"] = new CollectionConfig
                    {
                        Permalink = "/blog/{slug}/",
                        Template = "pages/post.html",
                        Schema = new[]
                        {
                            new SchemaFieldDefinition
                            {
                                Name = "status",
                                Type = "string",
                                Required = requiredStatus,
                                Default = requiredStatus ? null : "published"
                            }
                        }
                    }
                }
            },
            Content = new ContentConfig
            {
                Provider = "markdown"
            },
            Build = new BuildConfig
            {
                Draft = draft,
                SchemaFailMode = schemaFailMode
            }
        };
    }

    private sealed class RecordingContentProviderFactory : IContentProviderFactory
    {
        private readonly ContentLoadResult _loadResult;

        public RecordingContentProviderFactory(ContentLoadResult loadResult)
        {
            _loadResult = loadResult;
        }

        public bool CreateCalled { get; private set; }
        public bool LocalizeCalled { get; private set; }
        public bool IsCiObserved { get; private set; }
        public string? RootDirObserved { get; private set; }

        public IContentProvider Create(AppConfig config, string rootDir, bool isCi, ILogger logger)
        {
            CreateCalled = true;
            IsCiObserved = isCi;
            RootDirObserved = rootDir;
            return new RecordingContentProvider(_loadResult);
        }

        public Task<ContentLoadResult> LocalizeContentImagesAsync(ContentLoadResult result, MediaConfig media, string rootDir, string cacheDir, ILogger logger, CancellationToken cancellationToken)
        {
            LocalizeCalled = true;
            RootDirObserved = rootDir;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingContentProvider : IContentProvider
    {
        private readonly ContentLoadResult _loadResult;

        public RecordingContentProvider(ContentLoadResult loadResult)
        {
            _loadResult = loadResult;
        }

        public Task<ContentLoadResult> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_loadResult);
        }
    }

    private sealed class RawContentProviderFactory : IContentProviderFactory
    {
        private readonly RawOnlyContentProvider _provider;

        public RawContentProviderFactory(RawOnlyContentProvider provider)
        {
            _provider = provider;
        }

        public IContentProvider Create(AppConfig config, string rootDir, bool isCi, ILogger logger) => _provider;

        public Task<ContentLoadResult> LocalizeContentImagesAsync(ContentLoadResult result, MediaConfig media, string rootDir, string cacheDir, ILogger logger, CancellationToken cancellationToken)
            => Task.FromResult(result);
    }

    private sealed class RawOnlyContentProvider : IContentProvider, IRawContentProvider
    {
        private readonly RawContentLoadResult _rawResult;

        public RawOnlyContentProvider(RawContentLoadResult rawResult)
        {
            _rawResult = rawResult;
        }

        public bool LoadRawCalled { get; private set; }
        public bool LoadCalled { get; private set; }

        public Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
        {
            LoadRawCalled = true;
            return Task.FromResult(_rawResult);
        }

        public Task<ContentLoadResult> LoadAsync(CancellationToken cancellationToken = default)
        {
            LoadCalled = true;
            throw new InvalidOperationException("Raw-first pipeline should not call legacy LoadAsync.");
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Infos { get; } = new();
        public List<string> Warnings { get; } = new();

        public void Debug(string message)
        {
        }

        public void Info(string message)
        {
            Infos.Add(message);
        }

        public void Warn(string message)
        {
            Warnings.Add(message);
        }

        public void Error(string message)
        {
        }
    }
}
