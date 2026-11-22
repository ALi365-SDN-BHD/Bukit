using Bukit.Content;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class DataModuleBuilderTests
{
    [Fact]
    public async Task BuildModules_WithEmptyItems_ReturnsNull()
    {
        var result = await DataModuleBuilder.BuildModulesAsync(
            Array.Empty<ContentDocument>(), "zh-CN", new StubBodyStore());

        Assert.Null(result);
    }

    [Fact]
    public async Task BuildModules_WithDataItems_GroupsByType()
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

        var result = await DataModuleBuilder.BuildModulesAsync(items, "zh-CN", new StubBodyStore());

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.True(result.ContainsKey("hero"));
        Assert.True(result.ContainsKey("footer"));
        Assert.Equal(2, result["hero"].Count);
        Assert.Single(result["footer"]);
    }

    [Fact]
    public async Task BuildModules_WithoutType_UsesModuleAsDefault()
    {
        var items = new[]
        {
            CreateDocument("m1", "No Type", "no-type", "<p>module</p>",
                ContentFieldReader.ToFieldMap(new Dictionary<string, object>
                {
                    ["sourceMode"] = "data"
                })),
        };

        var result = await DataModuleBuilder.BuildModulesAsync(items, "zh-CN", new StubBodyStore());

        Assert.NotNull(result);
        Assert.True(result!.ContainsKey("module"));
    }

    [Fact]
    public async Task BuildModules_OrdersByOrderFieldThenTitle()
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

        var result = await DataModuleBuilder.BuildModulesAsync(items, "zh-CN", new StubBodyStore());

        Assert.NotNull(result);
        var widgets = result!["widget"];
        Assert.Equal(4, widgets.Count);
        Assert.Equal("Alpha", widgets[0].Title);
        Assert.Equal("Beta", widgets[1].Title);
        Assert.Equal("Beta A", widgets[2].Title);
        Assert.Equal("Charlie", widgets[3].Title);
    }

    [Fact]
    public async Task BuildModules_ItemsWithEnabledFalse_AreSkipped()
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

        var result = await DataModuleBuilder.BuildModulesAsync(items, "zh-CN", new StubBodyStore());

        Assert.NotNull(result);
        Assert.Single(result!["widget"]);
        Assert.Equal("Enabled", result["widget"][0].Title);
    }

    [Fact]
    public async Task BuildModules_PopulatesModuleInfoCorrectly()
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

        var result = await DataModuleBuilder.BuildModulesAsync(items, "zh-CN", new StubBodyStore());

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
    public async Task BuildModules_UsesBodyStoreWhenContentHtmlIsNull()
    {
        var items = new[]
        {
            CreateDocument("m1", "From Store", "from-store", null,
                ContentFieldReader.ToFieldMap(new Dictionary<string, object> { ["type"] = "widget" })),
        };
        var bodyStore = new StubBodyStore(html: "<p>stored content</p>");

        var result = await DataModuleBuilder.BuildModulesAsync(items, "zh-CN", bodyStore);

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
    public void BuildDataIndex_AmbiguousConfiguredAliases_PreservesDiagnosticContext()
    {
        var fields = ContentFieldReader.ToFieldMap(new Dictionary<string, object>
        {
            ["sourceKey"] = "settings",
            ["sourceMode"] = "data",
            ["scope"] = "contact",
            ["key"] = "email",
            ["value"] = "contact@example.com",
            ["valueType"] = "email",
            ["value_type"] = "email"
        });
        var item = CreateDocument("email", "email", "email", null, fields);
        var source = CreateIndexedSource() with
        {
            DataIndex = CreateIndexedSource().DataIndex! with { ValueTypeField = "value__type" }
        };

        var ex = Assert.Throws<ContentException>(() =>
            DataModuleBuilder.BuildDataIndex([item], [source]));

        Assert.Contains("Data index source 'settings' content 'email'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("ambiguous aliases", ex.Message, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void BuildDataIndex_ObjectFromMarkdownMap_SortsKeysAndResolvesScalars()
    {
        var value = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = "Penang",
            ["days"] = "5",
            ["open"] = "false",
            ["note"] = null,
            ["stops"] = new List<object?>
            {
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["name"] = "George Town" },
                "  free text stays as is  "
            }
        };

        var info = ReadIndexed(CreateIndexedDocument("trip", "trip", "info", value, "object"), "trip", "info");

        var map = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(info);
        Assert.Equal(["days", "note", "open", "stops", "title"], map.Keys);
        Assert.Equal(5L, map["days"]);
        Assert.Equal(false, map["open"]);
        Assert.Null(map["note"]);
        var stops = Assert.IsType<List<object?>>(map["stops"]);
        var stop = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(stops[0]);
        Assert.Equal("George Town", stop["name"]);
        Assert.Equal("  free text stays as is  ", stops[1]);
    }

    [Fact]
    public void BuildDataIndex_ObjectFromNotionJson_KeepsJsonTypes()
    {
        const string json = """ {"title":"Penang","days":5,"price":12.50,"ratio":1e-3,"big":123456789012345678901234567890123,"open":true,"closed":false,"note":null,"code":"5","html":"<b>x</b>"} """;

        var info = ReadIndexed(CreateIndexedDocument("trip", "trip", "info", json, "object"), "trip", "info");

        var map = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(info);
        Assert.Equal(["big", "closed", "code", "days", "html", "note", "open", "price", "ratio", "title"], map.Keys);
        Assert.Equal(5L, map["days"]);
        var price = Assert.IsType<decimal>(map["price"]);
        Assert.Equal("12.50", price.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(0.001m, map["ratio"]);
        Assert.Equal(1.2345678901234568E+32, Assert.IsType<double>(map["big"]));
        Assert.Equal(true, map["open"]);
        Assert.Equal(false, map["closed"]);
        Assert.Null(map["note"]);
        // A JSON string stays text even when it looks like a number.
        Assert.Equal("5", map["code"]);
        Assert.Equal("<b>x</b>", map["html"]);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("FALSE", false)]
    [InlineData("-12", -12L)]
    [InlineData("0", 0L)]
    [InlineData("9223372036854775807", long.MaxValue)]
    [InlineData("0123", "0123")]
    [InlineData("+60", "+60")]
    [InlineData(".5", ".5")]
    [InlineData("1.", "1.")]
    [InlineData("yes", "yes")]
    [InlineData("tRUE", "tRUE")]
    [InlineData("NaN", "NaN")]
    [InlineData("60 12-345", "60 12-345")]
    public void BuildDataIndex_MarkdownScalar_ResolvesOnlyCoreBooleansAndJsonNumbers(string text, object expected)
    {
        var value = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["v"] = text };

        var map = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(
            ReadIndexed(CreateIndexedDocument("trip", "trip", "info", value, "object"), "trip", "info"));

        Assert.Equal(expected, map["v"]);
    }

    [Fact]
    public void BuildDataIndex_MarkdownDecimal_KeepsDigits()
    {
        var value = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["price"] = "12.50" };

        var map = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(
            ReadIndexed(CreateIndexedDocument("trip", "trip", "info", value, "object"), "trip", "info"));

        Assert.Equal("12.50", Assert.IsType<decimal>(map["price"]).ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void BuildDataIndex_MarkdownAndNotionObjects_WithSameContent_AreEqual()
    {
        var markdown = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["days"] = "5", ["open"] = "true", ["price"] = "12.50" };

        var fromMarkdown = ReadIndexed(CreateIndexedDocument("a", "trip", "info", markdown, "object"), "trip", "info");
        var fromNotion = ReadIndexed(CreateIndexedDocument("b", "trip", "info", """{"open":true,"days":5,"price":12.50}""", "object"), "trip", "info");

        Assert.Equal(
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(fromMarkdown),
            Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(fromNotion));
    }

    [Fact]
    public void BuildDataIndex_ObjectArray_FromJsonAndList_BuildsListOfMaps()
    {
        var fromJson = ReadIndexed(CreateIndexedDocument("a", "trip", "stops", """[{"name":"A"},{"name":"B"}]""", "object[]"), "trip", "stops");
        var fromList = ReadIndexed(CreateIndexedDocument("b", "trip", "stops", new List<object?>
        {
            new Dictionary<string, object?> { ["name"] = "A" }
        }, "object[]"), "trip", "stops");

        var jsonList = Assert.IsType<List<object?>>(fromJson);
        Assert.Equal(2, jsonList.Count);
        Assert.Equal("B", Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(jsonList[1])["name"]);
        Assert.Single(Assert.IsType<List<object?>>(fromList));
    }

    [Theory]
    [InlineData("object")]
    [InlineData("object[]")]
    public void BuildDataIndex_OptionalEmptyObjectValue_BuildsEmptyContainer(string valueType)
    {
        var value = ReadIndexed(CreateIndexedDocument("trip", "trip", "info", "  ", valueType), "trip", "info");

        Assert.Empty(Assert.IsAssignableFrom<System.Collections.ICollection>(value));
    }

    [Theory]
    [InlineData("object", "")]
    [InlineData("object", "{}")]
    [InlineData("object[]", "[]")]
    [InlineData("object", "{\"title\":null,\"note\":\"  \",\"stops\":[]}")]
    [InlineData("object[]", "[{\"title\":null}]")]
    public void BuildDataIndex_RequiredEmptyObjectValue_Throws(string valueType, string value)
    {
        var source = CreateIndexedSource() with
        {
            DataIndex = CreateIndexedSource().DataIndex! with { RequiredKeys = ["trip.info"] }
        };

        var ex = Assert.Throws<ContentException>(() =>
            DataModuleBuilder.BuildDataIndex([CreateIndexedDocument("trip", "trip", "info", value, valueType)], [source]));

        Assert.Contains("required key 'trip.info' is missing or empty", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("object", "{\"days\":", "is not valid JSON")]
    [InlineData("object", "[{\"days\":5}]", "has value type 'object' but its value is not an object")]
    [InlineData("object", "\"text\"", "has value type 'object' but its value is not an object")]
    [InlineData("object[]", "\"text\"", "has value type 'object[]' but its value is not a list of objects")]
    [InlineData("object", "{\"\":1}", "value has a blank object key")]
    [InlineData("object", "{\"a\":{\" \":1}}", "value at 'a' has a blank object key")]
    [InlineData("object[]", "[{\"days\":5},\"x\"]", "has value type 'object[]' but its value at '[1]' is not an object")]
    [InlineData("object", "{\"a\":1,\"a\":2}", "value at 'a' is a duplicate key")]
    [InlineData("object", "{\"a\":{\"b\":1e400}}", "value at 'a.b' is not a finite number")]
    public void BuildDataIndex_InvalidObjectJson_ThrowsWithSourceContentAndKey(string valueType, string json, string problem)
    {
        var ex = Assert.Throws<ContentException>(() =>
            DataModuleBuilder.BuildDataIndex([CreateIndexedDocument("row-7", "trip", "info", json, valueType)], [CreateIndexedSource()]));

        Assert.Contains("Data index source 'settings' content 'row-7' key 'trip.info'", ex.Message, StringComparison.Ordinal);
        Assert.Contains(problem, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDataIndex_ObjectDeeperThanMaxDepth_Throws()
    {
        var atLimit = string.Concat(Enumerable.Repeat("{\"a\":", 8)) + "1" + new string('}', 8);
        var tooDeep = string.Concat(Enumerable.Repeat("{\"a\":", 9)) + "1" + new string('}', 9);

        ReadIndexed(CreateIndexedDocument("ok", "trip", "info", atLimit, "object"), "trip", "info");
        var ex = Assert.Throws<ContentException>(() =>
            DataModuleBuilder.BuildDataIndex([CreateIndexedDocument("deep", "trip", "info", tooDeep, "object")], [CreateIndexedSource()]));

        Assert.Contains("exceeds the maximum nesting depth of 8", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDataIndex_ObjectArrayGivenSingleMapping_IsListOfOne()
    {
        var fromJson = ReadIndexed(CreateIndexedDocument("a", "trip", "stops", """{"name":"A"}""", "object[]"), "trip", "stops");
        var fromMap = ReadIndexed(CreateIndexedDocument("b", "trip", "stops",
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["name"] = "B" }, "object[]"), "trip", "stops");

        Assert.Equal("A", Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(Assert.Single(Assert.IsType<List<object?>>(fromJson)))["name"]);
        Assert.Equal("B", Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(Assert.Single(Assert.IsType<List<object?>>(fromMap)))["name"]);
    }

    [Fact]
    public void BuildDataIndex_ObjectArrayGivenSingleMappingAtMaxDepth_Throws()
    {
        // Eight levels on its own; the list it is wrapped in makes nine.
        var atLimit = string.Concat(Enumerable.Repeat("{\"a\":", 8)) + "1" + new string('}', 8);

        var ex = Assert.Throws<ContentException>(() =>
            DataModuleBuilder.BuildDataIndex([CreateIndexedDocument("deep", "trip", "stops", atLimit, "object[]")], [CreateIndexedSource()]));

        Assert.Contains("exceeds the maximum nesting depth of 8 as a list of one object", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDataIndex_ObjectFromGenericOnlyDictionary_IsReadAsMap()
    {
        var value = new GenericOnlyMap(new Dictionary<string, object?> { ["days"] = 5, ["inner"] = new GenericOnlyMap(new() { ["x"] = "y" }) });

        var info = ReadIndexed(CreateIndexedDocument("trip", "trip", "info", value, "object"), "trip", "info");

        var map = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(info);
        Assert.Equal(5L, map["days"]);
        Assert.Equal("y", Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(map["inner"])["x"]);
    }

    [Fact]
    public void BuildDataIndex_TextTypeGivenGenericOnlyDictionary_ReportsObject()
    {
        var value = new GenericOnlyMap(new Dictionary<string, object?> { ["days"] = "5" });

        var ex = Assert.Throws<ContentException>(() =>
            DataModuleBuilder.BuildDataIndex([CreateIndexedDocument("map", "trip", "info", value, "text")], [CreateIndexedSource()]));

        Assert.Contains("but its value is an object", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDataIndex_TextTypeGivenNumberOrDate_FormatsInvariantly()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

            var number = ReadIndexed(CreateIndexedDocument("n", "trip", "price", 12.5d, "text"), "trip", "price");
            var date = ReadIndexed(CreateIndexedDocument("d", "trip", "start", new DateTime(2026, 11, 23), "text"), "trip", "start");

            Assert.Equal("12.5", number);
            Assert.Equal(new DateTime(2026, 11, 23).ToString(CultureInfo.InvariantCulture), date);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void BuildDataIndex_NonFiniteNumberInMap_Throws()
    {
        var value = new Dictionary<string, object?> { ["ratio"] = double.NaN };

        var ex = Assert.Throws<ContentException>(() =>
            DataModuleBuilder.BuildDataIndex([CreateIndexedDocument("nan", "trip", "info", value, "object")], [CreateIndexedSource()]));

        Assert.Contains("value at 'ratio' is not a finite number", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("multiline")]
    [InlineData("email")]
    [InlineData("phone")]
    [InlineData("url")]
    public void BuildDataIndex_TextTypeGivenObjectOrList_Throws(string valueType)
    {
        var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["days"] = "5" };
        var list = new List<object?> { "a", "b" };

        var mapError = Assert.Throws<ContentException>(() =>
            DataModuleBuilder.BuildDataIndex([CreateIndexedDocument("map", "trip", "info", map, valueType)], [CreateIndexedSource()]));
        var listError = Assert.Throws<ContentException>(() =>
            DataModuleBuilder.BuildDataIndex([CreateIndexedDocument("list", "trip", "info", list, valueType)], [CreateIndexedSource()]));

        Assert.Contains($"content 'map' key 'trip.info' has value type '{valueType}' but its value is an object", mapError.Message, StringComparison.Ordinal);
        Assert.Contains($"content 'list' key 'trip.info' has value type '{valueType}' but its value is a list", listError.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Collections", mapError.Message, StringComparison.Ordinal);
    }

    private static object ReadIndexed(ContentDocument document, string scope, string key)
    {
        var result = DataModuleBuilder.BuildDataIndex([document], [CreateIndexedSource()]);
        var source = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(result!["settings"]);
        var values = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(source[scope]);
        return values[key];
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
        object value,
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

    /// <summary>A map that implements only the generic read-only dictionary, not <see cref="IDictionary"/>.</summary>
    private sealed class GenericOnlyMap(Dictionary<string, object?> values) : IReadOnlyDictionary<string, object?>
    {
        public object? this[string key] => values[key];
        public IEnumerable<string> Keys => values.Keys;
        public IEnumerable<object?> Values => values.Values;
        public int Count => values.Count;
        public bool ContainsKey(string key) => values.ContainsKey(key);
        public bool TryGetValue(string key, [MaybeNullWhen(false)] out object? value) => values.TryGetValue(key, out value);
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
