using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class DataModuleBuilderTests
{
    [Fact]
    public void BuildModules_WithEmptyItems_ReturnsNull()
    {
        var result = DataModuleBuilder.BuildModules(
            Array.Empty<ContentItem>(), "zh-CN", new StubBodyStore());

        Assert.Null(result);
    }

    [Fact]
    public void BuildModules_WithDataItems_GroupsByType()
    {
        var items = new[]
        {
            new ContentItem(
                Id: "m1",
                Title: "Hero Banner",
                Slug: "hero-banner",
                PublishAt: DateTimeOffset.UtcNow,
                ContentHtml: "<p>hero</p>",
                Fields: new Dictionary<string, ContentField> { ["type"] = new("text", "hero") }),
            new ContentItem(
                Id: "m2",
                Title: "Footer",
                Slug: "footer",
                PublishAt: DateTimeOffset.UtcNow,
                ContentHtml: "<p>footer</p>",
                Fields: new Dictionary<string, ContentField> { ["type"] = new("text", "footer") }),
            new ContentItem(
                Id: "m3",
                Title: "Hero Secondary",
                Slug: "hero-secondary",
                PublishAt: DateTimeOffset.UtcNow,
                ContentHtml: "<p>hero2</p>",
                Fields: new Dictionary<string, ContentField> { ["type"] = new("text", "hero") }),
        };

        var result = DataModuleBuilder.BuildModules(items, "zh-CN", new StubBodyStore());

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.True(result.ContainsKey("hero"));
        Assert.True(result.ContainsKey("footer"));
        Assert.Equal(2, result["hero"].Count);
        Assert.Single(result["footer"]);
    }

    [Fact]
    public void BuildModules_WithoutType_UsesModuleAsDefault()
    {
        var items = new[]
        {
            new ContentItem(
                Id: "m1",
                Title: "No Type",
                Slug: "no-type",
                PublishAt: DateTimeOffset.UtcNow,
                ContentHtml: "<p>module</p>"),
        };

        var result = DataModuleBuilder.BuildModules(items, "zh-CN", new StubBodyStore());

        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("module"));
    }

    [Fact]
    public void BuildModules_OrdersByOrderFieldThenTitle()
    {
        var items = new[]
        {
            new ContentItem(
                Id: "c",
                Title: "Charlie",
                Slug: "charlie",
                PublishAt: DateTimeOffset.UtcNow,
                ContentHtml: "<p>c</p>",
                Fields: new Dictionary<string, ContentField> { ["type"] = new("text", "widget"), ["order"] = new("number", 3d) }),
            new ContentItem(
                Id: "a",
                Title: "Alpha",
                Slug: "alpha",
                PublishAt: DateTimeOffset.UtcNow,
                ContentHtml: "<p>a</p>",
                Fields: new Dictionary<string, ContentField> { ["type"] = new("text", "widget"), ["order"] = new("number", 1d) }),
            new ContentItem(
                Id: "b1",
                Title: "Beta",
                Slug: "beta",
                PublishAt: DateTimeOffset.UtcNow,
                ContentHtml: "<p>b1</p>",
                Fields: new Dictionary<string, ContentField> { ["type"] = new("text", "widget"), ["order"] = new("number", 2d) }),
            new ContentItem(
                Id: "b2",
                Title: "Beta A",
                Slug: "beta-a",
                PublishAt: DateTimeOffset.UtcNow,
                ContentHtml: "<p>b2</p>",
                Fields: new Dictionary<string, ContentField> { ["type"] = new("text", "widget"), ["order"] = new("number", 2d) }),
        };

        var result = DataModuleBuilder.BuildModules(items, "zh-CN", new StubBodyStore());

        Assert.NotNull(result);
        var widgets = result!["widget"];
        Assert.Equal(4, widgets.Count);
        Assert.Equal("Alpha", widgets[0].Title);
        Assert.Equal("Beta", widgets[1].Title);
        Assert.Equal("Beta A", widgets[2].Title);
        Assert.Equal("Charlie", widgets[3].Title);
    }

    [Fact]
    public void BuildModules_ItemsWithEnabledFalse_AreSkipped()
    {
        var items = new[]
        {
            new ContentItem(
                Id: "m1",
                Title: "Enabled",
                Slug: "enabled",
                PublishAt: DateTimeOffset.UtcNow,
                ContentHtml: "<p>enabled</p>",
                Fields: new Dictionary<string, ContentField> { ["type"] = new("text", "widget") }),
            new ContentItem(
                Id: "m2",
                Title: "Disabled",
                Slug: "disabled",
                PublishAt: DateTimeOffset.UtcNow,
                ContentHtml: "<p>disabled</p>",
                Fields: new Dictionary<string, ContentField> { ["type"] = new("text", "widget"), ["enabled"] = new("bool", false) }),
        };

        var result = DataModuleBuilder.BuildModules(items, "zh-CN", new StubBodyStore());

        Assert.NotNull(result);
        Assert.Single(result!["widget"]);
        Assert.Equal("Enabled", result["widget"][0].Title);
    }

    [Fact]
    public void BuildModules_PopulatesModuleInfoCorrectly()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["type"] = new("text", "banner"),
            ["color"] = new("text", "red")
        };
        var items = new[]
        {
            new ContentItem(
                Id: "mod-1",
                Title: "Test Module",
                Slug: "test-module",
                PublishAt: DateTimeOffset.UtcNow,
                ContentHtml: "<p>content</p>",
                Fields: fields),
        };

        var result = DataModuleBuilder.BuildModules(items, "zh-CN", new StubBodyStore());

        Assert.NotNull(result);
        var module = result!["banner"][0];
        Assert.Equal("mod-1", module.Id);
        Assert.Equal("Test Module", module.Title);
        Assert.Equal("test-module", module.Slug);
        Assert.Equal("<p>content</p>", module.Content);
        Assert.NotNull(module.Fields);
        Assert.True(module.Fields!.ContainsKey("color"));
    }

    [Fact]
    public void BuildModules_UsesBodyStoreWhenContentHtmlIsNull()
    {
        var items = new[]
        {
            new ContentItem(
                Id: "m1",
                Title: "From Store",
                Slug: "from-store",
                PublishAt: DateTimeOffset.UtcNow,
                ContentHtml: null,
                Fields: new Dictionary<string, ContentField> { ["type"] = new("text", "widget") }),
        };
        var bodyStore = new StubBodyStore(html: "<p>stored content</p>");

        var result = DataModuleBuilder.BuildModules(items, "zh-CN", bodyStore);

        Assert.NotNull(result);
        Assert.Equal("<p>stored content</p>", result!["widget"][0].Content);
    }

    private sealed class StubBodyStore : IContentBodyStore
    {
        private readonly string _html;

        public StubBodyStore(string html = "")
        {
            _html = html;
        }

        public Task<ContentBody> GetAsync(ContentItem item, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ContentBody(_html));
        }
    }
}
