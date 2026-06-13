using System.Collections;
using System.Text.Json.Nodes;
using Bukit.Engine.Plugins.Protocol;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ProtocolJsonHelperTests
{
    [Fact]
    public void ToJsonNode_Null_ReturnsNull()
    {
        var result = ProtocolJsonHelper.ToJsonNode(null);

        Assert.Null(result);
    }

    [Fact]
    public void ToJsonNode_JsonNode_ReturnsDeepClone()
    {
        var original = new JsonObject
        {
            ["key"] = "value",
            ["nested"] = new JsonObject { ["inner"] = 42 }
        };

        var result = ProtocolJsonHelper.ToJsonNode(original);

        Assert.NotNull(result);
        Assert.IsType<JsonObject>(result);
        Assert.Equal("value", (string?)result!["key"]);
        Assert.NotSame(original, result);
    }

    [Fact]
    public void ToJsonNode_IReadOnlyDictionary_ReturnsJsonObject()
    {
        var dict = new Dictionary<string, object>
        {
            ["name"] = "test",
            ["count"] = 5,
            ["active"] = true
        };

        var result = ProtocolJsonHelper.ToJsonNode(dict);

        Assert.NotNull(result);
        Assert.IsType<JsonObject>(result);
        var obj = (JsonObject)result!;
        Assert.Equal("test", (string?)obj["name"]);
        Assert.Equal(5, (int?)obj["count"]);
        Assert.Equal(true, (bool?)obj["active"]);
    }

    [Fact]
    public void ToJsonNode_IDictionary_ReturnsJsonObject()
    {
        IDictionary dict = new Dictionary<string, object>
        {
            ["key"] = "value"
        };

        var result = ProtocolJsonHelper.ToJsonNode(dict);

        Assert.NotNull(result);
        Assert.IsType<JsonObject>(result);
    }

    [Fact]
    public void ToJsonNode_IEnumerable_ReturnsJsonArray()
    {
        var list = new List<object> { "a", "b", "c" };

        var result = ProtocolJsonHelper.ToJsonNode(list);

        Assert.NotNull(result);
        Assert.IsType<JsonArray>(result);
        var arr = (JsonArray)result!;
        Assert.Equal(3, arr.Count);
    }

    [Fact]
    public void ToJsonNode_Array_ReturnsJsonArray()
    {
        var array = new object[] { 1, 2, 3 };

        var result = ProtocolJsonHelper.ToJsonNode(array);

        Assert.NotNull(result);
        Assert.IsType<JsonArray>(result);
        var arr = (JsonArray)result!;
        Assert.Equal(3, arr.Count);
    }

    [Fact]
    public void ToJsonNode_String_ReturnsJsonValue()
    {
        var result = ProtocolJsonHelper.ToJsonNode("hello");

        Assert.NotNull(result);
        Assert.IsAssignableFrom<JsonValue>(result);
        Assert.Equal("hello", result!.GetValue<string>());
    }

    [Fact]
    public void ToJsonNode_Bool_ReturnsJsonValue()
    {
        var resultTrue = ProtocolJsonHelper.ToJsonNode(true);
        Assert.NotNull(resultTrue);
        Assert.IsAssignableFrom<JsonValue>(resultTrue);
        Assert.True(resultTrue!.GetValue<bool>());

        var resultFalse = ProtocolJsonHelper.ToJsonNode(false);
        Assert.NotNull(resultFalse);
        Assert.IsAssignableFrom<JsonValue>(resultFalse);
        Assert.False(resultFalse!.GetValue<bool>());
    }

    [Fact]
    public void ToJsonNode_Int_ReturnsJsonValue()
    {
        var result = ProtocolJsonHelper.ToJsonNode(42);

        Assert.NotNull(result);
        Assert.IsAssignableFrom<JsonValue>(result);
        Assert.Equal(42, result!.GetValue<int>());
    }

    [Fact]
    public void ToJsonNode_Long_ReturnsJsonValue()
    {
        var result = ProtocolJsonHelper.ToJsonNode(1234567890123L);

        Assert.NotNull(result);
        Assert.IsAssignableFrom<JsonValue>(result);
        Assert.Equal(1234567890123L, result!.GetValue<long>());
    }

    [Fact]
    public void ToJsonNode_Float_ReturnsJsonValue()
    {
        var result = ProtocolJsonHelper.ToJsonNode(3.14f);

        Assert.NotNull(result);
        Assert.IsAssignableFrom<JsonValue>(result);
    }

    [Fact]
    public void ToJsonNode_Double_ReturnsJsonValue()
    {
        var result = ProtocolJsonHelper.ToJsonNode(3.14159);

        Assert.NotNull(result);
        Assert.IsAssignableFrom<JsonValue>(result);
    }

    [Fact]
    public void ToJsonNode_Decimal_ReturnsJsonValue()
    {
        var result = ProtocolJsonHelper.ToJsonNode(99.99m);

        Assert.NotNull(result);
        Assert.IsAssignableFrom<JsonValue>(result);
    }

    [Fact]
    public void ToJsonNode_DateTime_ReturnsJsonValue()
    {
        var now = DateTime.UtcNow;
        var result = ProtocolJsonHelper.ToJsonNode(now);

        Assert.NotNull(result);
        Assert.IsAssignableFrom<JsonValue>(result);
        Assert.Equal(now, result!.GetValue<DateTime>());
    }

    [Fact]
    public void ToJsonNode_DateTimeOffset_ReturnsJsonValue()
    {
        var now = DateTimeOffset.UtcNow;
        var result = ProtocolJsonHelper.ToJsonNode(now);

        Assert.NotNull(result);
        Assert.IsAssignableFrom<JsonValue>(result);
        Assert.Equal(now, result!.GetValue<DateTimeOffset>());
    }

    [Fact]
    public void ToJsonNode_Guid_ReturnsJsonValue()
    {
        var guid = Guid.NewGuid();
        var result = ProtocolJsonHelper.ToJsonNode(guid);

        Assert.NotNull(result);
        Assert.IsAssignableFrom<JsonValue>(result);
        Assert.Equal(guid, result!.GetValue<Guid>());
    }

    [Fact]
    public void ToJsonNode_Enum_ReturnsJsonValue()
    {
        var result = ProtocolJsonHelper.ToJsonNode(DayOfWeek.Monday);

        Assert.NotNull(result);
        Assert.IsAssignableFrom<JsonValue>(result);
        Assert.Equal("Monday", result!.GetValue<string>());
    }

    [Fact]
    public void ToJsonNode_NestedList_ReturnsJsonArrayWithNodes()
    {
        var list = new List<object>
        {
            new Dictionary<string, object> { ["a"] = 1 },
            "hello",
            42
        };

        var result = ProtocolJsonHelper.ToJsonNode(list);

        Assert.NotNull(result);
        Assert.IsType<JsonArray>(result);
        var arr = (JsonArray)result!;
        Assert.Equal(3, arr.Count);
        Assert.IsType<JsonObject>(arr[0]);
        Assert.IsAssignableFrom<JsonValue>(arr[1]);
        Assert.IsAssignableFrom<JsonValue>(arr[2]);
    }

    [Fact]
    public void ToJsonNode_ByteArray_ReturnsJsonValue()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var result = ProtocolJsonHelper.ToJsonNode(bytes);

        Assert.NotNull(result);
        Assert.IsAssignableFrom<JsonValue>(result);
    }

    [Fact]
    public void ToJsonObject_EmptyDictionary_ReturnsEmptyObject()
    {
        var dict = new Dictionary<string, object>();

        var result = ProtocolJsonHelper.ToJsonObject(dict);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ToJsonObject_SingleEntry_ReturnsCorrectObject()
    {
        var dict = new Dictionary<string, object> { ["key"] = "value" };

        var result = ProtocolJsonHelper.ToJsonObject(dict);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("value", (string?)result["key"]);
    }

    [Fact]
    public void ToJsonObject_MultipleEntries_ReturnsCorrectObject()
    {
        var dict = new Dictionary<string, object>
        {
            ["string"] = "text",
            ["number"] = 42,
            ["boolean"] = false
        };

        var result = ProtocolJsonHelper.ToJsonObject(dict);

        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("text", (string?)result["string"]);
        Assert.Equal(42, (int?)result["number"]);
        Assert.Equal(false, (bool?)result["boolean"]);
    }

    [Fact]
    public void ToJsonObject_NestedDictionary_ReturnsNestedObject()
    {
        var dict = new Dictionary<string, object>
        {
            ["outer"] = new Dictionary<string, object>
            {
                ["inner"] = "deep",
                ["count"] = 1
            }
        };

        var result = ProtocolJsonHelper.ToJsonObject(dict);

        Assert.NotNull(result);
        Assert.Single(result);
        var inner = result["outer"] as JsonObject;
        Assert.NotNull(inner);
        Assert.Equal("deep", (string?)inner!["inner"]);
        Assert.Equal(1, (int?)inner["count"]);
    }

    [Fact]
    public void ToJsonNode_Char_ReturnsJsonValue()
    {
        var result = ProtocolJsonHelper.ToJsonNode('A');

        Assert.NotNull(result);
        Assert.IsAssignableFrom<JsonValue>(result);
    }

    [Fact]
    public void ToJsonNode_Short_ReturnsJsonValue()
    {
        var result = ProtocolJsonHelper.ToJsonNode((short)123);

        Assert.NotNull(result);
        Assert.IsAssignableFrom<JsonValue>(result);
    }

    [Fact]
    public void ToJsonNode_UInt_ReturnsJsonValue()
    {
        var result = ProtocolJsonHelper.ToJsonNode(42u);

        Assert.NotNull(result);
        Assert.IsAssignableFrom<JsonValue>(result);
    }
}
