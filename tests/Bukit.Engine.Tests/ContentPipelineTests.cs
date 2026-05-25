using Bukit.Config;
using Bukit.Content;
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
        Assert.Equal("published", item.Meta["status"]);
        Assert.Same(EmptyContentBodyStore.Instance, result.BodyStore);
        Assert.Empty(result.SchemaErrors);
        Assert.Contains(logger.Infos, message => message == "event=content.draft_filtered removed=1");
        Assert.Contains(logger.Infos, message => message == "event=content.loaded count=1");
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

    private static ContentItem Item(string id, string slug, IReadOnlyDictionary<string, object> meta)
    {
        return new ContentItem(id, id, slug, DateTimeOffset.UnixEpoch, $"<p>{id}</p>", meta);
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
