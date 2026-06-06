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
            Array.Empty<ContentDocument>(), "zh-CN", new StubBodyStore());

        Assert.Null(result);
    }

    [Fact]
    public void BuildModules_WithDataItems_GroupsByType()
    {
        var items = new[]
        {
            CreateDocument("m1", "Hero Banner", "hero-banner", "<p>hero</p>",
                ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "hero" })),
            CreateDocument("m2", "Footer", "footer", "<p>footer</p>",
                ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "footer" })),
            CreateDocument("m3", "Hero Secondary", "hero-secondary", "<p>hero2</p>",
                ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "hero" })),
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
            CreateDocument("m1", "No Type", "no-type", "<p>module</p>",
                ContentFieldReader.ToFieldMap(new Dictionary<string, object>
                {
                    ["sourceMode"] = "data"
                })),
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
            CreateDocument("c", "Charlie", "charlie", "<p>c</p>",
                ContentFieldReader.WithValues(
                    new Dictionary<string, ContentField> { ["order"] = new("number", 3d) },
                    new Dictionary<string, object> { ["type"] = "widget" })),
            CreateDocument("a", "Alpha", "alpha", "<p>a</p>",
                ContentFieldReader.WithValues(
                    new Dictionary<string, ContentField> { ["order"] = new("number", 1d) },
                    new Dictionary<string, object> { ["type"] = "widget" })),
            CreateDocument("b1", "Beta", "beta", "<p>b1</p>",
                ContentFieldReader.WithValues(
                    new Dictionary<string, ContentField> { ["order"] = new("number", 2d) },
                    new Dictionary<string, object> { ["type"] = "widget" })),
            CreateDocument("b2", "Beta A", "beta-a", "<p>b2</p>",
                ContentFieldReader.WithValues(
                    new Dictionary<string, ContentField> { ["order"] = new("number", 2d) },
                    new Dictionary<string, object> { ["type"] = "widget" })),
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
            CreateDocument("m1", "Enabled", "enabled", "<p>enabled</p>",
                ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "widget" })),
            CreateDocument("m2", "Disabled", "disabled", "<p>disabled</p>",
                ContentFieldReader.WithValues(
                    new Dictionary<string, ContentField> { ["enabled"] = new("bool", false) },
                    new Dictionary<string, object> { ["type"] = "widget" })),
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
            ["color"] = new("text", "red")
        };
        var items = new[]
        {
            CreateDocument("mod-1", "Test Module", "test-module", "<p>content</p>",
                ContentFieldReader.WithValues(fields, new Dictionary<string, object> { ["type"] = "banner" })),
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
            CreateDocument("m1", "From Store", "from-store", null,
                ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "widget" })),
        };
        var bodyStore = new StubBodyStore(html: "<p>stored content</p>");

        var result = DataModuleBuilder.BuildModules(items, "zh-CN", bodyStore);

        Assert.NotNull(result);
        Assert.Equal("<p>stored content</p>", result!["widget"][0].Content);
    }

    private static ContentDocument CreateDocument(
        string id,
        string title,
        string slug,
        string? contentHtml,
        IReadOnlyDictionary<string, ContentField>? fields)
        => ContentDocument.Create(id, title, slug, DateTimeOffset.UtcNow, contentHtml, fields);

    private sealed class StubBodyStore : IContentBodyStore
    {
        private readonly string _html;

        public StubBodyStore(string html = "")
        {
            _html = html;
        }

        public Task<ContentBody> GetAsync(ContentDocument item, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ContentBody(_html));
        }
    }
}
