using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Stages;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ContentStagesTests
{
    private static ContentItem Item(string id, string slug, IReadOnlyDictionary<string, object> fields) =>
        new(
            id,
            id,
            slug,
            DateTimeOffset.UnixEpoch,
            $"<p>{id}</p>",
            fields.ToDictionary(kv => kv.Key, kv => new ContentField("test", kv.Value), StringComparer.OrdinalIgnoreCase));

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
        var loadResult = new ContentLoadResult(
            new[] { Item("a", "a", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["type"] = "page" }) },
            EmptyContentBodyStore.Instance);
        var factory = new StubContentProviderFactory(loadResult);
        var stage = new ContentLoadStage(factory);
        var input = new ContentStageInput(Array.Empty<ContentItem>(), EmptyContentBodyStore.Instance, Config(), NoOverrides, "/root", "/cache", new NoOpLogger());

        var output = await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Single(output.Items);
        Assert.Equal("a", output.Items[0].Id);
        Assert.True(output.DurationMs >= 0);
        Assert.Equal(stage.Name, output.StageName);
    }

    [Fact]
    public async Task DraftFilterStage_RemovesDraftItems()
    {
        var published = Item("published", "pub", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase));
        var draft = Item("draft", "draft", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["draft"] = true
        });
        var stage = new DraftFilterStage();
        var input = new ContentStageInput(new[] { published, draft }, EmptyContentBodyStore.Instance, Config(draft: false), NoOverrides, "/root", "/cache", new NoOpLogger());

        var output = await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Single(output.Items);
        Assert.Equal("published", output.Items[0].Id);
        Assert.Equal("DraftFilter", output.StageName);
    }

    [Fact]
    public async Task DraftFilterStage_DraftMode_KeepsAll()
    {
        var published = Item("published", "pub", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase));
        var draft = Item("draft", "draft", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["draft"] = true
        });
        var stage = new DraftFilterStage();
        var input = new ContentStageInput(new[] { published, draft }, EmptyContentBodyStore.Instance, Config(draft: true), NoOverrides, "/root", "/cache", new NoOpLogger());

        var output = await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Equal(2, output.Items.Count);
    }

    [Fact]
    public async Task SchemaDefaultsStage_AppliesDefaultValues()
    {
        var item = Item("a", "a", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post"
        });
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["post"] = new()
                    {
                        Permalink = "/blog/{slug}/",
                        Template = "pages/post.html",
                        Schema = new[]
                        {
                            new SchemaFieldDefinition { Name = "status", Type = "string", Default = "published" }
                        }
                    }
                }
            },
            Content = new ContentConfig { Provider = "markdown", Markdown = new MarkdownConfig() },
            Build = new BuildConfig { Draft = true }
        };
        var stage = new SchemaDefaultsStage();
        var input = new ContentStageInput(new[] { item }, EmptyContentBodyStore.Instance, config, NoOverrides, "/root", "/cache", new NoOpLogger());

        var output = await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Equal("published", output.Items[0].Fields!["status"].Value);
        Assert.Equal("SchemaDefaults", output.StageName);
    }

    [Fact]
    public async Task SchemaValidateStage_WarnMode_CollectsErrors()
    {
        var item = Item("a", "a", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post"
        });
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["post"] = new()
                    {
                        Permalink = "/blog/{slug}/",
                        Template = "pages/post.html",
                        Schema = new[]
                        {
                            new SchemaFieldDefinition { Name = "required_field", Type = "string", Required = true }
                        }
                    }
                }
            },
            Content = new ContentConfig { Provider = "markdown", Markdown = new MarkdownConfig() },
            Build = new BuildConfig { SchemaFailMode = "warn" }
        };
        var stage = new SchemaValidateStage();
        var input = new ContentStageInput(new[] { item }, EmptyContentBodyStore.Instance, config, NoOverrides, "/root", "/cache", new NoOpLogger());

        var output = await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.NotNull(output.SchemaErrors);
        Assert.NotEmpty(output.SchemaErrors);
        Assert.Contains(output.SchemaErrors, e => e.Code.Contains("required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SchemaValidateStage_StrictMode_Throws()
    {
        var item = Item("a", "a", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post"
        });
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["post"] = new()
                    {
                        Permalink = "/blog/{slug}/",
                        Template = "pages/post.html",
                        Schema = new[]
                        {
                            new SchemaFieldDefinition { Name = "required_field", Type = "string", Required = true }
                        }
                    }
                }
            },
            Content = new ContentConfig { Provider = "markdown", Markdown = new MarkdownConfig() },
            Build = new BuildConfig { SchemaFailMode = "strict" }
        };
        var stage = new SchemaValidateStage();
        var input = new ContentStageInput(new[] { item }, EmptyContentBodyStore.Instance, config, NoOverrides, "/root", "/cache", new NoOpLogger());

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
        var item = Item("a", "a", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase));
        var input = new ContentStageInput(new[] { item }, EmptyContentBodyStore.Instance, config, NoOverrides, "/root", "/cache", new NoOpLogger());

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
        var input = new ContentStageInput(Array.Empty<ContentItem>(), EmptyContentBodyStore.Instance, config, NoOverrides, "/root", "/cache", new NoOpLogger());

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
            return Task.FromResult(new ContentStageOutput(input.Items, input.BodyStore, Name, 0, null));
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
        private readonly ContentLoadResult _result;

        public StubContentProviderFactory(ContentLoadResult result) => _result = result;

        public IContentProvider Create(AppConfig config, string rootDir, bool isCi, ILogger logger)
        {
            return new StubContentProvider(_result);
        }

        public Task<ContentLoadResult> LocalizeContentImagesAsync(ContentLoadResult result, MediaConfig media, string rootDir, string cacheDir, ILogger logger, CancellationToken cancellationToken)
        {
            return Task.FromResult(result);
        }
    }

    private sealed class StubContentProvider : IContentProvider
    {
        private readonly ContentLoadResult _result;

        public StubContentProvider(ContentLoadResult result) => _result = result;

        public Task<ContentLoadResult> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed class NoOpLogger : ILogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
