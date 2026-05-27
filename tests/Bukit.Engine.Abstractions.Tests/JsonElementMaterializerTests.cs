using System.Reflection;
using System.Text.Json;
using Bukit.Engine.Abstractions.Plugins.Protocol;
using Xunit;

namespace Bukit.Engine.Abstractions.Tests;

public class JsonElementMaterializerTests
{
    private static T CallPrivate<T>(string methodName, params object[] args)
    {
        var type = typeof(JsonElementMaterializer);
        var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)!;
        return (T)method.Invoke(null, args)!;
    }

    private static JsonElement ParseElement(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    // MaterializeElement private method tests via reflection

    [Fact]
    public void MaterializeElement_String()
    {
        var element = ParseElement("\"hello\"");

        var result = CallPrivate<object?>("MaterializeElement", element);

        Assert.Equal("hello", result);
    }

    [Fact]
    public void MaterializeElement_True()
    {
        var element = ParseElement("true");

        var result = CallPrivate<object?>("MaterializeElement", element);

        Assert.Equal(true, result);
    }

    [Fact]
    public void MaterializeElement_False()
    {
        var element = ParseElement("false");

        var result = CallPrivate<object?>("MaterializeElement", element);

        Assert.Equal(false, result);
    }

    [Fact]
    public void MaterializeElement_Null()
    {
        var element = ParseElement("null");

        var result = CallPrivate<object?>("MaterializeElement", element);

        Assert.Null(result);
    }

    [Fact]
    public void MaterializeElement_Integer()
    {
        var element = ParseElement("42");

        var result = CallPrivate<object?>("MaterializeElement", element);

        Assert.Equal(42L, result);
    }

    [Fact]
    public void MaterializeElement_Double()
    {
        var element = ParseElement("3.14");

        var result = CallPrivate<object?>("MaterializeElement", element);

        Assert.Equal(3.14, result);
    }

    [Fact]
    public void MaterializeElement_Array_Nested()
    {
        var element = ParseElement("[[1,2],[3,4]]");

        var result = CallPrivate<List<object>>("MaterializeArray", element);

        Assert.Equal(2, result.Count);
        var inner = Assert.IsType<List<object>>(result[0]);
        Assert.Equal(1L, inner[0]);
        Assert.Equal(2L, inner[1]);
    }

    [Fact]
    public void MaterializeElement_Object_Nested()
    {
        var element = ParseElement("{\"a\":{\"b\":\"c\"}}");

        var result = CallPrivate<Dictionary<string, object>>("MaterializeObject", element);

        Assert.Single(result);
        var inner = Assert.IsType<Dictionary<string, object>>(result["a"]);
        Assert.Equal("c", inner["b"]);
    }

    [Fact]
    public void MaterializeElement_EmptyArray()
    {
        var element = ParseElement("[]");

        var result = CallPrivate<List<object>>("MaterializeArray", element);

        Assert.Empty(result);
    }

    [Fact]
    public void MaterializeElement_EmptyObject()
    {
        var element = ParseElement("{}");

        var result = CallPrivate<Dictionary<string, object>>("MaterializeObject", element);

        Assert.Empty(result);
    }

    // Materialize(object? value) internal method tests

    [Fact]
    public void MaterializeObject_JsonElement_ReturnsMaterialized()
    {
        var element = ParseElement("\"test\"");

        var result = JsonElementMaterializer.Materialize(element);

        Assert.Equal("test", result);
    }

    [Fact]
    public void MaterializeObject_JsonElementList_ReturnsMaterializedList()
    {
        var e1 = ParseElement("1");
        var e2 = ParseElement("\"two\"");
        IReadOnlyList<JsonElement> list = new List<JsonElement> { e1, e2 };

        var result = JsonElementMaterializer.Materialize(list);

        var resultList = Assert.IsType<List<object>>(result);
        Assert.Equal(2, resultList.Count);
        Assert.Equal(1L, resultList[0]);
        Assert.Equal("two", resultList[1]);
    }

    [Fact]
    public void MaterializeObject_PlainObject_ReturnsSameValue()
    {
        var value = "plain";

        var result = JsonElementMaterializer.Materialize(value);

        Assert.Same(value, result);
    }

    [Fact]
    public void MaterializeObject_Null_ReturnsNull()
    {
        var result = JsonElementMaterializer.Materialize(null);

        Assert.Null(result);
    }

    // Materialize(IReadOnlyDictionary<string, object>? dict) internal method tests

    [Fact]
    public void MaterializeDictionary_Null_ReturnsNull()
    {
        var result = JsonElementMaterializer.Materialize((IReadOnlyDictionary<string, object>?)null);

        Assert.Null(result);
    }

    [Fact]
    public void MaterializeDictionary_WithJsonElementValues_Materializes()
    {
        var dict = new Dictionary<string, object>
        {
            ["name"] = ParseElement("\"alice\""),
            ["count"] = ParseElement("5")
        };

        var result = JsonElementMaterializer.Materialize(dict);

        Assert.NotNull(result);
        Assert.Equal("alice", result!["name"]);
        Assert.Equal(5L, result!["count"]);
        Assert.IsNotType<JsonElement>(result!["name"]);
    }

    [Fact]
    public void MaterializeDictionary_WithJsonElementListValues_Materializes()
    {
        var dict = new Dictionary<string, object>
        {
            ["items"] = new List<JsonElement> { ParseElement("1"), ParseElement("2") }
        };

        var result = JsonElementMaterializer.Materialize(dict);

        Assert.NotNull(result);
        var items = Assert.IsType<List<object>>(result!["items"]);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void MaterializeDictionary_WithPlainValues_ReturnsSameReference()
    {
        var dict = new Dictionary<string, object>
        {
            ["key"] = "value"
        };

        var result = JsonElementMaterializer.Materialize(dict);

        Assert.Same(dict, result);
    }

    [Fact]
    public void MaterializeDictionary_MixedValues_MaterializesOnlyJsonElements()
    {
        var dict = new Dictionary<string, object>
        {
            ["plain"] = "hello",
            ["json"] = ParseElement("\"world\"")
        };

        var result = JsonElementMaterializer.Materialize(dict);

        Assert.NotNull(result);
        Assert.Equal("hello", result!["plain"]);
        Assert.Equal("world", result!["json"]);
    }
}
