using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ContentPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_LoadsLocalizesFiltersDraftsAndBuildsCanonicalContent()
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
        Assert.Equal("published", ContentFieldReader.GetText(document.Fields, "status"));
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
            item.Id,
            item.Title,
            item.Slug,
            item.PublishAt,
            item.ContentHtml,
            item.Fields,
            item.BodyKey);

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
        private readonly RawContentLoadResult _loadResult;

        public RecordingContentProviderFactory(RawContentLoadResult loadResult)
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

        public Task<RawContentLoadResult> LocalizeContentImagesAsync(RawContentLoadResult result, MediaConfig media, string rootDir, string cacheDir, ILogger logger, CancellationToken cancellationToken)
        {
            LocalizeCalled = true;
            RootDirObserved = rootDir;
            return Task.FromResult(result);
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
}
