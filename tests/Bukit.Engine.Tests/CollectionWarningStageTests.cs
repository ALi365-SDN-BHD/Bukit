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

    private static ContentItem CreateItem(string id, IReadOnlyDictionary<string, object> meta)
    {
        return new ContentItem(
            Id: id,
            Title: $"Item {id}",
            Slug: $"item-{id}",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>hi</p>",
            Meta: meta,
            Fields: null);
    }

    private static ContentStageInput CreateInput(IReadOnlyList<ContentItem> items, ILogger logger)
    {
        return new ContentStageInput(
            items,
            EmptyContentBodyStore.Instance,
            new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "T" },
                Content = new ContentConfig
                {
                    Provider = "markdown",
                    Markdown = new MarkdownConfig { Dir = "content" },
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
        var item = CreateItem("my-post", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post"
        });
        var stage = new CollectionWarningStage();
        var input = CreateInput(new[] { item }, logger);

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
        var item = CreateItem("my-page", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "page"
        });
        var stage = new CollectionWarningStage();
        var input = CreateInput(new[] { item }, logger);

        await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Single(logger.Warnings);
        Assert.Contains("[WARN]", logger.Warnings[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_HasCollection_NoWarning()
    {
        var logger = new TestLogger();
        var item = CreateItem("with-col", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["collection"] = "blog",
            ["type"] = "custom"
        });
        var stage = new CollectionWarningStage();
        var input = CreateInput(new[] { item }, logger);

        await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.NotEmpty(logger.Warnings);
    }

    [Fact]
    public async Task ExecuteAsync_CustomTypeWithoutCollection_NoWarning()
    {
        var logger = new TestLogger();
        var item = CreateItem("custom", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "custom"
        });
        var stage = new CollectionWarningStage();
        var input = CreateInput(new[] { item }, logger);

        await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Single(logger.Warnings);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleItems_MultipleWarnings()
    {
        var logger = new TestLogger();
        var items = new[]
        {
            CreateItem("a", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["type"] = "post" }),
            CreateItem("b", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["collection"] = "news", ["type"] = "post" }),
            CreateItem("c", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["type"] = "page" }),
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
        var item = CreateItem("my-conflict", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post",
            ["collection"] = "companies"
        });
        var stage = new CollectionWarningStage();
        var input = CreateInput(new[] { item }, logger);

        await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Single(logger.Warnings);
        Assert.Contains("[WARN]", logger.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("type=post", logger.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("collection=companies", logger.Warnings[0], StringComparison.Ordinal);
        Assert.Contains("Collection routing uses collection", logger.Warnings[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TypeWithNonPostPageCollection_NoWarning()
    {
        var logger = new TestLogger();
        var item = CreateItem("my-custom", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "custom",
            ["collection"] = "companies"
        });
        var stage = new CollectionWarningStage();
        var input = CreateInput(new[] { item }, logger);

        await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.NotEmpty(logger.Warnings);
    }

    [Fact]
    public async Task ExecuteAsync_FieldOnlyTypeWithoutCollection_EmitsWarning()
    {
        var logger = new TestLogger();
        var item = new ContentItem(
            Id: "field-post",
            Title: "Field Post",
            Slug: "field-post",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "<p>hi</p>",
            Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", "post")
            });
        var stage = new CollectionWarningStage();
        var input = CreateInput(new[] { item }, logger);

        await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Single(logger.Warnings);
        Assert.Contains("type=post", logger.Warnings[0], StringComparison.Ordinal);
    }
}
