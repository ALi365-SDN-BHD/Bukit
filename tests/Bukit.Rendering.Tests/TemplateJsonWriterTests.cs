using System.Text.Json;
using Bukit.Rendering.Scriban;
using Scriban.Runtime;
using Xunit;

namespace Bukit.Rendering.Tests;

public sealed class TemplateJsonWriterTests
{
    [Fact]
    public void Serialize_WritesNumbersWithInvariantCulture()
    {
        var value = new ScriptObject
        {
            ["long"] = long.MaxValue,
            ["negative"] = -42L,
            ["double"] = 0.1 + 0.2,
            ["decimal"] = 1234.5600m,
            ["float"] = 1.1f,
            ["half"] = (Half)0.5,
            ["nan"] = double.NaN,
            ["infinity"] = double.PositiveInfinity
        };

        var json = TemplateJsonWriter.Serialize(value);

        Assert.Equal(
            """{"decimal":1234.5600,"double":0.30000000000000004,"float":1.1,"half":0.5,"infinity":null,"long":9223372036854775807,"nan":null,"negative":-42}""",
            json);
        using var _ = JsonDocument.Parse(json);
    }

    [Fact]
    public void Serialize_WritesNullDatesAndCollections()
    {
        var value = new Dictionary<string, object?>
        {
            ["z"] = null,
            ["date"] = new DateTimeOffset(2026, 11, 22, 8, 30, 0, TimeSpan.FromHours(8)),
            ["list"] = new List<object?> { "a", 1, null, new ScriptArray { "b" } },
            ["中文"] = "马来西亚"
        };

        Assert.Equal(
            """{"date":"2026-11-22T08:30:00.0000000+08:00","list":["a",1,null,["b"]],"z":null,"中文":"马来西亚"}""",
            TemplateJsonWriter.Serialize(value));
        Assert.Equal("null", TemplateJsonWriter.Serialize(null));
    }

    [Fact]
    public void Serialize_OrdersKeysOrdinallyRegardlessOfInsertionOrder()
    {
        var first = new ScriptObject { ["b"] = 1, ["@type"] = "X", ["a"] = 2 };
        var second = new ScriptObject { ["a"] = 2, ["b"] = 1, ["@type"] = "X" };

        Assert.Equal("""{"@type":"X","a":2,"b":1}""", TemplateJsonWriter.Serialize(first));
        Assert.Equal(TemplateJsonWriter.Serialize(first), TemplateJsonWriter.Serialize(second));
    }

    [Fact]
    public void Serialize_KeepsEnumerationOrderForKeysThatStringifyAlike()
    {
        var first = new Dictionary<object, object> { [1] = "int", ["1"] = "string" };
        var second = new Dictionary<object, object> { ["1"] = "string", [1] = "int" };

        Assert.Equal("""{"1":"int","1":"string"}""", TemplateJsonWriter.Serialize(first));
        Assert.Equal("""{"1":"string","1":"int"}""", TemplateJsonWriter.Serialize(second));
    }

    [Fact]
    public void Serialize_WritesStringValuedReadOnlyMapsAsObjects()
    {
        IReadOnlyDictionary<string, string> map = new StringMap(new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" });

        Assert.Equal("""{"a":"1","b":"2"}""", TemplateJsonWriter.Serialize(map));
    }

    [Fact]
    public void SerializeString_EscapesHtmlSensitiveAndInvalidCharacters()
    {
        var value = "a\"b\\c\n\u0001<>&\u2028\u2029😀";

        var json = TemplateJsonWriter.SerializeString(value);

        Assert.Equal("\"a\\\"b\\\\c\\n\\u0001\\u003c\\u003e\\u0026\\u2028\\u2029😀\"", json);
        Assert.Equal(value, JsonSerializer.Deserialize<string>(json));
        Assert.Equal("\"x\\ud800\"", TemplateJsonWriter.SerializeString("x\ud800"));
    }

    [Fact]
    public void Serialize_RejectsReferenceCycles()
    {
        var value = new ScriptObject();
        value["self"] = value;

        Assert.Throws<InvalidOperationException>(() => TemplateJsonWriter.Serialize(value));
    }

    // Not an IDictionary, like a hand-written read-only model map.
    private sealed class StringMap(Dictionary<string, string> inner) : IReadOnlyDictionary<string, string>
    {
        public string this[string key] => inner[key];
        public IEnumerable<string> Keys => inner.Keys;
        public IEnumerable<string> Values => inner.Values;
        public int Count => inner.Count;
        public bool ContainsKey(string key) => inner.ContainsKey(key);
        public bool TryGetValue(string key, out string value) => inner.TryGetValue(key, out value!);
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => inner.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
