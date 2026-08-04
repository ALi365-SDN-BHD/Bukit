using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Stages;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ContentPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_WhenArbitraryImageLocalizeStageThrows_DisposesPriorBuiltInStoreExactlyOnce()
    {
        var ownedStore = new TrackingBodyStore();
        var factory = new RecordingContentProviderFactory(
            new RawContentLoadResult(Array.Empty<RawContentDocument>(), ownedStore));
        var stages = new IContentStage[]
        {
            new ContentLoadStage(factory),
            new ThrowingContentStage("ImageLocalize")
        };
        var pipeline = new ContentPipeline(stages, new RecordingLogger());
        var input = new ContentStageInput(
            Array.Empty<ContentDocument>(),
            EmptyContentBodyStore.Instance,
            Config(draft: true, schemaFailMode: "warn"),
            new ConfigOverrides(),
            "/tmp/site",
            "/tmp/site/.cache/media",
            new RecordingLogger());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pipeline.ExecuteAsync(input, CancellationToken.None));

        Assert.Equal(1, ownedStore.DisposeCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCustomContentLoadStageStoreFails_DoesNotDisposeCallerOwnedStore()
    {
        var externalStore = new TrackingBodyStore();
        var stages = new IContentStage[]
        {
            new BodyStoreStage("ContentLoad", externalStore),
            new ThrowingContentStage()
        };
        var pipeline = new ContentPipeline(stages, new RecordingLogger());
        var input = new ContentStageInput(
            Array.Empty<ContentDocument>(),
            EmptyContentBodyStore.Instance,
            Config(draft: true, schemaFailMode: "warn"),
            new ConfigOverrides(),
            "/tmp/site",
            "/tmp/site/.cache/media",
            new RecordingLogger());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pipeline.ExecuteAsync(input, CancellationToken.None));

        Assert.Equal(0, externalStore.DisposeCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenImageLocalizeStageLoggingThrows_DisposesReturnedStoreChainExactlyOnce()
    {
        var inner = new TrackingBodyStore();
        var localized = new ForwardingBodyStore(inner);
        var factory = new RecordingContentProviderFactory(
            new RawContentLoadResult(Array.Empty<RawContentDocument>(), inner),
            result => Task.FromResult(new RawContentLoadResult(result.Documents, localized)));
        var stages = new IContentStage[]
        {
            new ContentLoadStage(factory),
            new ImageLocalizeStage(factory)
        };
        var pipeline = new ContentPipeline(
            stages,
            new ThrowingInfoLogger(message =>
                message.StartsWith("event=content.stage stage=ImageLocalize ", StringComparison.Ordinal)));
        var input = new ContentStageInput(
            Array.Empty<ContentDocument>(),
            EmptyContentBodyStore.Instance,
            Config(draft: true, schemaFailMode: "warn"),
            new ConfigOverrides(),
            "/tmp/site",
            "/tmp/site/.cache/media",
            new RecordingLogger());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pipeline.ExecuteAsync(input, CancellationToken.None));

        Assert.Equal(1, localized.DisposeCount);
        Assert.Equal(1, inner.DisposeCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenImageLocalizeStageFails_DisposesInputStoreExactlyOnce()
    {
        var ownedStore = new TrackingBodyStore();
        var factory = new RecordingContentProviderFactory(
            new RawContentLoadResult(Array.Empty<RawContentDocument>(), ownedStore),
            _ => Task.FromException<RawContentLoadResult>(new InvalidOperationException("localization failed")));
        var stages = new IContentStage[]
        {
            new ContentLoadStage(factory),
            new ImageLocalizeStage(factory)
        };
        var pipeline = new ContentPipeline(stages, new RecordingLogger());
        var input = new ContentStageInput(
            Array.Empty<ContentDocument>(),
            EmptyContentBodyStore.Instance,
            Config(draft: true, schemaFailMode: "warn"),
            new ConfigOverrides(),
            "/tmp/site",
            "/tmp/site/.cache/media",
            new RecordingLogger());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pipeline.ExecuteAsync(input, CancellationToken.None));

        Assert.Equal(1, ownedStore.DisposeCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenImageLocalizeInputConversionFails_DisposesLoaderStoreExactlyOnce()
    {
        var ownedStore = new TrackingBodyStore();
        var factory = new RecordingContentProviderFactory(
            new RawContentLoadResult(Array.Empty<RawContentDocument>(), ownedStore));
        var stages = new IContentStage[]
        {
            new ContentLoadStage(factory),
            new BodyStoreStage("Malformed", ownedStore, [(ContentDocument)null!]),
            new ImageLocalizeStage(factory)
        };
        var pipeline = new ContentPipeline(stages, new RecordingLogger());
        var input = new ContentStageInput(
            Array.Empty<ContentDocument>(),
            EmptyContentBodyStore.Instance,
            Config(draft: true, schemaFailMode: "warn"),
            new ConfigOverrides(),
            "/tmp/site",
            "/tmp/site/.cache/media",
            new RecordingLogger());

        await Assert.ThrowsAsync<NullReferenceException>(() =>
            pipeline.ExecuteAsync(input, CancellationToken.None));

        Assert.False(factory.LocalizeCalled);
        Assert.Equal(1, ownedStore.DisposeCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGraphConstructionThrows_DisposesPipelineOwnedStoreExactlyOnce()
    {
        var ownedStore = new TrackingBodyStore();
        var factory = new RecordingContentProviderFactory(
            new RawContentLoadResult(Array.Empty<RawContentDocument>(), ownedStore));
        var stages = new IContentStage[]
        {
            new ContentLoadStage(factory),
            new BodyStoreStage("External", ownedStore, [(ContentDocument)null!])
        };
        var pipeline = new ContentPipeline(stages, new RecordingLogger());
        var input = new ContentStageInput(
            Array.Empty<ContentDocument>(),
            EmptyContentBodyStore.Instance,
            Config(draft: true, schemaFailMode: "warn"),
            new ConfigOverrides(),
            "/tmp/site",
            "/tmp/site/.cache/media",
            new RecordingLogger());

        await Assert.ThrowsAsync<NullReferenceException>(() =>
            pipeline.ExecuteAsync(input, CancellationToken.None));

        Assert.Equal(1, ownedStore.DisposeCount);
    }

    [Fact]
    public async Task ExecuteAsync_ContentModeTypeOnly_ThrowsConfigException()
    {
        var item = Item("article-only", "article-only", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "article"
        });
        var factory = new RecordingContentProviderFactory(RawResult(item));
        var pipeline = new ContentPipeline(factory, new RecordingLogger());

        await Assert.ThrowsAsync<ConfigException>(() => pipeline.ExecuteAsync(
            Config(draft: true, schemaFailMode: "warn"),
            "/tmp/site",
            new ConfigOverrides(),
            "/tmp/site/.cache/media",
            CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_LoadsLocalizesFiltersDraftsAndBuildsCanonicalContent()
    {
        var published = Item("published", "published", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post",
            ["collection"] = "post"
        });
        var draft = Item("draft", "draft", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post",
            ["collection"] = "post",
            ["draft"] = true
        });
        var loadResult = RawResult(published, draft);
        var factory = new RecordingContentProviderFactory(loadResult);
        var logger = new RecordingLogger();
        var config = Config(draft: false, schemaFailMode: "warn");
        var pipeline = new ContentPipeline(factory, logger);

        var result = await pipeline.ExecuteAsync(config, "/tmp/site", new ConfigOverrides { IsCI = true }, "/tmp/site/.cache/media", CancellationToken.None);

        Assert.True(factory.CreateCalled);
        Assert.True(factory.LocalizeCalled);
        Assert.True(factory.IsCiObserved);
        Assert.Equal("/tmp/site", factory.RootDirObserved);
        var document = Assert.Single(result.Documents);
        Assert.Equal("published", document.Id);
        Assert.Equal("published", ContentFieldReader.GetText(document.CustomFields, "status"));
        var record = Assert.Single(result.ContentGraph!.Records);
        Assert.Equal("published", record.Identity.Id);
        Assert.Equal("published", record.Trust.ReviewStatus);
        Assert.Equal("post", record.Classification.Collection);
        Assert.NotNull(result.BodyStore);
        Assert.NotNull(result.BodyCacheMetrics);
        Assert.Empty(result.SchemaErrors);
        Assert.Contains(logger.Infos, message => message.StartsWith("event=content.loaded", StringComparison.Ordinal) && message.Contains("count=", StringComparison.Ordinal));
        Assert.Contains(logger.Infos, message => message == "event=content.draft_filtered removed=1");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCanonicalSchemaStrict_ThrowsConfigException()
    {
        var item = Item("invalid-status", "invalid-status", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post",
            ["collection"] = "post",
            ["status"] = "invalid"
        });
        var factory = new RecordingContentProviderFactory(RawResult(item));
        var logger = new RecordingLogger();
        var config = Config(draft: true, schemaFailMode: "strict");
        var pipeline = new ContentPipeline(factory, logger);

        var ex = await Assert.ThrowsAsync<ConfigException>(() => pipeline.ExecuteAsync(config, "/tmp/site", new ConfigOverrides(), "/tmp/site/.cache/media", CancellationToken.None));

        Assert.Equal("Canonical content validation failed with 2 error(s).", ex.Message);
        Assert.Contains(logger.Warnings, message => message.Contains("event=canonical.validation", StringComparison.Ordinal));
        Assert.Contains(logger.Warnings, message => message.Contains("canonical_status_invalid", StringComparison.Ordinal));
        Assert.Contains(logger.Warnings, message => message.Contains("canonical_review_status_invalid", StringComparison.Ordinal));
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

        var factory = new RecordingContentProviderFactory(RawResult(localized));
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
        var graphDocument = Assert.Single(result.ContentGraph.Documents);
        Assert.Equal("localized-post", graphDocument.Id);
        Assert.Contains(result.ContentGraph.Entities, x => x.Name == "Bukit" && x.Type == "company");
    }

    [Fact]
    public async Task ExecuteAsync_BuildsCanonicalContentGraphFromStructuredFields()
    {
        var item = ContentDocument.Create(
            "notion-post",
            "Notion Post",
            "notion-post",
            DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            "<p>Body</p>",
            new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", "post"),
                ["collection"] = new("text", "post"),
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

        var factory = new RecordingContentProviderFactory(RawResult(item));
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
        Assert.Contains(result.ContentGraph!.Relations, x => x.Type == "related-posts" && x.Target == "Related One");
        Assert.Contains(record.Media, x => x.Kind == "image" && x.Url == "https://img.example/1.jpg");
        Assert.Contains(record.Media, x => x.Kind == "image" && x.Url == "https://img.example/2.jpg");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportCanonicalMediaAltGap_WhenImageHasNoAltText()
    {
        var item = ContentDocument.Create(
            "image-post",
            "Image Post",
            "image-post",
            DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            "<p>Body</p>",
            new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", "post"),
                ["collection"] = new("text", "post"),
                ["source"] = new("text", "markdown"),
                ["image"] = new("file", "https://img.example/cover.jpg")
            });

        var factory = new RecordingContentProviderFactory(RawResult(item));
        var pipeline = new ContentPipeline(factory, new RecordingLogger());

        var result = await pipeline.ExecuteAsync(Config(draft: true, schemaFailMode: "warn"), "/tmp/site", new ConfigOverrides(), "/tmp/site/.cache/media", CancellationToken.None);

        Assert.Contains(result.SchemaErrors, error =>
            error.Code == "canonical_media_alt_missing" &&
            error.Field == "media.alt" &&
            error.SourcePath == "image-post");
    }

    private static ContentDocument Item(string id, string slug, IReadOnlyDictionary<string, object> meta)
    {
        return ContentDocument.Create(id, id, slug, DateTimeOffset.UnixEpoch, $"<p>{id}</p>", ContentFieldReader.ToFieldMap(meta));
    }

    private static RawContentLoadResult RawResult(params ContentDocument[] items)
        => new(items.Select(ToRawDocument).ToArray(), EmptyContentBodyStore.Instance);

    private static RawContentDocument ToRawDocument(ContentDocument item)
        => new(
            Id: item.Id,
            Title: item.Title,
            Slug: item.Slug,
            PublishAt: item.PublishAt,
            Body: new RawBody(item.Body.Html, item.Body.BodyKey, item.Body.Markdown, item.Body.PlainText),
            Properties: RawContentValue.FromFields(item.CustomFields),
            Source: item.Source,
            CustomFields: item.CustomFields);

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
                        Template = "pages/post.html"
                    }
                }
            },
            Content = TestContent.Markdown() with
            {
                ModelSchema = new ContentModelSchemaConfig
                {
                    FieldScopes = new Dictionary<string, IReadOnlyList<CustomFieldDefinitionConfig>>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["post"] = new[]
                        {
                            new CustomFieldDefinitionConfig
                            {
                                Name = "status",
                                FieldType = "string",
                                Required = requiredStatus,
                                Default = requiredStatus ? null : "published"
                            }
                        }
                    }
                }
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
        private readonly RawContentLoadResult _loadResult;
        private readonly Func<RawContentLoadResult, Task<RawContentLoadResult>>? _localizeAsync;

        public RecordingContentProviderFactory(
            RawContentLoadResult loadResult,
            Func<RawContentLoadResult, Task<RawContentLoadResult>>? localizeAsync = null)
        {
            _loadResult = loadResult;
            _localizeAsync = localizeAsync;
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

        public Task<RawContentLoadResult> LocalizeContentImagesAsync(RawContentLoadResult result, MediaConfig media, string rootDir, string cacheDir, ILogger logger, CancellationToken cancellationToken)
        {
            LocalizeCalled = true;
            RootDirObserved = rootDir;
            return _localizeAsync?.Invoke(result) ?? Task.FromResult(result);
        }
    }

    private sealed class RecordingContentProvider : IContentProvider
    {
        private readonly RawContentLoadResult _loadResult;

        public RecordingContentProvider(RawContentLoadResult loadResult)
        {
            _loadResult = loadResult;
        }

        public Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ToRawResult(_loadResult));
        }
    }

    private static RawContentLoadResult ToRawResult(RawContentLoadResult result) => result;

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

    private sealed class BodyStoreStage : IContentStage
    {
        private readonly IContentBodyStore _bodyStore;

        public BodyStoreStage(
            string name,
            IContentBodyStore bodyStore,
            IReadOnlyList<ContentDocument>? documents = null)
        {
            Name = name;
            _bodyStore = bodyStore;
            _documents = documents;
        }

        public string Name { get; }
        private readonly IReadOnlyList<ContentDocument>? _documents;

        public Task<ContentStageOutput> ExecuteAsync(ContentStageInput input, CancellationToken cancellationToken)
            => Task.FromResult(new ContentStageOutput(_documents ?? input.Documents, _bodyStore, Name, 0, null));
    }

    private sealed class ThrowingContentStage : IContentStage
    {
        public ThrowingContentStage(string name = "Throwing")
        {
            Name = name;
        }

        public string Name { get; }

        public Task<ContentStageOutput> ExecuteAsync(ContentStageInput input, CancellationToken cancellationToken)
            => Task.FromException<ContentStageOutput>(new InvalidOperationException("stage failed"));
    }

    private sealed class ThrowingInfoLogger(Func<string, bool> shouldThrow) : ILogger
    {
        public void Debug(string message) { }

        public void Info(string message)
        {
            if (shouldThrow(message))
            {
                throw new InvalidOperationException("stage logging failed");
            }
        }

        public void Warn(string message) { }
        public void Error(string message) { }
    }

    private sealed class ForwardingBodyStore : IContentBodyStore, IAsyncDisposable
    {
        private readonly IContentBodyStore _inner;

        public ForwardingBodyStore(IContentBodyStore inner)
        {
            _inner = inner;
        }

        public int DisposeCount { get; private set; }

        public Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
            => _inner.GetAsync(document, cancellationToken);

        public async ValueTask DisposeAsync()
        {
            DisposeCount++;
            if (_inner is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
        }
    }

    private sealed class TrackingBodyStore : IContentBodyStore, IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
            => Task.FromResult(new ContentBody(string.Empty));

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
