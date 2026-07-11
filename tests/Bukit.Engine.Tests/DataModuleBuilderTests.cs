using Bukit.Content;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;
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

    [Fact]
    public void BuildDataIndex_WithScopedKeyValues_BuildsScalarIndex()
    {
        var items = new[]
        {
            CreateIndexedDocument("email", "contact", "email", "contact@example.com", "email"),
            CreateIndexedDocument("copyright", "footer", "copyright_text", "Copyright", "multiline")
        };

        var result = DataModuleBuilder.BuildDataIndex(items, [CreateIndexedSource()]);

        Assert.NotNull(result);
        var settings = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(result!["settings"]);
        var contact = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(settings["contact"]);
        var footer = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(settings["footer"]);
        Assert.Equal("contact@example.com", contact["email"]);
        Assert.Equal("Copyright", footer["copyright_text"]);
    }

    [Fact]
    public void BuildDataIndex_DuplicateScopeAndKey_Throws()
    {
        var items = new[]
        {
            CreateIndexedDocument("email-a", "contact", "email", "a@example.com", "email"),
            CreateIndexedDocument("email-b", "contact", "email", "b@example.com", "email")
        };

        var ex = Assert.Throws<ContentException>(() =>
            DataModuleBuilder.BuildDataIndex(items, [CreateIndexedSource()]));

        Assert.Contains("duplicate key 'contact.email'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDataIndex_MissingRequiredValue_Throws()
    {
        var source = CreateIndexedSource() with
        {
            DataIndex = CreateIndexedSource().DataIndex! with
            {
                RequiredKeys = ["contact.email"]
            }
        };
        var items = new[]
        {
            CreateIndexedDocument("phone", "contact", "phone", "+60 00", "phone")
        };

        var ex = Assert.Throws<ContentException>(() =>
            DataModuleBuilder.BuildDataIndex(items, [source]));

        Assert.Contains("required key 'contact.email'", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("unsupported", "value_type")]
    [InlineData("not-an-email", "email")]
    [InlineData("ftp://example.com", "url")]
    public void BuildDataIndex_InvalidValue_Throws(string value, string valueType)
    {
        var items = new[]
        {
            CreateIndexedDocument("invalid", "contact", "email", value, valueType)
        };

        Assert.Throws<ContentException>(() =>
            DataModuleBuilder.BuildDataIndex(items, [CreateIndexedSource()]));
    }

    [Fact]
    public void BuildDataIndex_NotionCamelCaseProjection_ResolvesConfiguredSnakeCaseField()
    {
        var fields = ContentFieldReader.ToFieldMap(new Dictionary<string, object>
        {
            ["sourceKey"] = "settings",
            ["sourceMode"] = "data",
            ["scope"] = "contact",
            ["key"] = "email",
            ["value"] = "contact@example.com",
            ["valuetype"] = "email"
        });
        var item = CreateDocument("email", "email", "email", null, fields);

        var result = DataModuleBuilder.BuildDataIndex([item], [CreateIndexedSource()]);

        var source = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(result!["settings"]);
        var scope = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(source["contact"]);
        Assert.Equal("contact@example.com", scope["email"]);
    }

    [Fact]
    public void BuildDataIndex_OptionalMissingValue_BuildsEmptyScalar()
    {
        var fields = ContentFieldReader.ToFieldMap(new Dictionary<string, object>
        {
            ["sourceKey"] = "settings",
            ["sourceMode"] = "data",
            ["scope"] = "contact",
            ["key"] = "phone",
            ["value_type"] = "phone"
        });
        var item = CreateDocument("phone", "phone", "phone", null, fields);

        var result = DataModuleBuilder.BuildDataIndex([item], [CreateIndexedSource()]);

        var source = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(result!["settings"]);
        var scope = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(source["contact"]);
        Assert.Equal(string.Empty, scope["phone"]);
    }

    [Theory]
    [InlineData("//evil.example/path")]
    [InlineData("/bad path")]
    [InlineData("/bad\\path")]
    public void BuildDataIndex_UnsafeRootRelativeUrl_Throws(string value)
    {
        var items = new[]
        {
            CreateIndexedDocument("url", "footer", "powered_by_url", value, "url")
        };

        Assert.Throws<ContentException>(() =>
            DataModuleBuilder.BuildDataIndex(items, [CreateIndexedSource()]));
    }

    [Theory]
    [InlineData("/about/")]
    [InlineData("/search/?q=bukit#results")]
    public void BuildDataIndex_ValidRootRelativeUrl_BuildsScalar(string value)
    {
        var items = new[]
        {
            CreateIndexedDocument("url", "footer", "powered_by_url", value, "url")
        };

        var result = DataModuleBuilder.BuildDataIndex(items, [CreateIndexedSource()]);

        Assert.NotNull(result);
    }

    private static ContentSourceConfig CreateIndexedSource() => new()
    {
        Type = "notion",
        Name = "settings",
        Mode = "data",
        DataIndex = new DataIndexConfig
        {
            ScopeField = "scope",
            KeyField = "key",
            ValueField = "value",
            ValueTypeField = "value_type"
        }
    };

    private static ContentDocument CreateIndexedDocument(
        string id,
        string scope,
        string key,
        string value,
        string valueType)
        => CreateDocument(id, id, id, null,
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["sourceKey"] = "settings",
                ["sourceMode"] = "data",
                ["scope"] = scope,
                ["key"] = key,
                ["value"] = value,
                ["value_type"] = valueType
            }));

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
