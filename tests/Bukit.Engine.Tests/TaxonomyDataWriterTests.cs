using System.Text.Json;
using Xunit;
using Bukit.Engine.Plugins.BuiltIn;

namespace Bukit.Engine.Tests;

/// <summary>
/// Tests for TaxonomyDataWriter data building and JSON serialization.
/// </summary>
public sealed class TaxonomyDataWriterTests
{
    private static TaxonomyTerm MakeTerm(string displayName, string slug, int weight = 0, string? description = null, string? image = null, string? parent = null, IReadOnlyList<string>? aliases = null)
    {
        var term = new TaxonomyTerm(displayName, slug)
        {
            Weight = weight,
            Description = description,
            Image = image,
            ParentSlug = parent,
            Aliases = aliases
        };
        return term;
    }

    private static TaxonomyPage MakePage(string id, string title, string url, string? summary = null, IReadOnlyDictionary<string, object>? extra = null)
        => new(
            Id: id,
            Title: title,
            Url: url,
            PublishAt: new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Summary: summary,
            Extra: extra,
            IsPinned: false,
            PinOrder: null);

    // ── BuildKindData ───────────────────────────────────────────────

    [Fact]
    public void BuildKindData_WithTerms_BuildsCompleteStructure()
    {
        var terms = new Dictionary<string, TaxonomyTerm>
        {
            ["news"] = new TaxonomyTerm("News", "news")
            {
                Weight = 2,
                Description = "Latest news",
                Aliases = ["current"],
                Pages = [MakePage("p1", "Post 1", "/p1/", "Summary 1")]
            }
        };

        var data = TaxonomyDataWriter.BuildKindData("tags", "tags", "Tags", terms);

        Assert.Equal("tags", data["key"]);
        Assert.Equal("tags", data["kind"]);
        Assert.Equal("Tags", data["title"]);
        Assert.Equal("/tags", data["route_prefix"]);
        Assert.Equal("/tags/", data["url"]);
        Assert.Single((System.Collections.IEnumerable)data["terms"]);
        Assert.True(((Dictionary<string, object>)data["items_by_term"]).ContainsKey("news"));
    }

    [Fact]
    public void BuildKindData_WithRoutePrefix_UsesNormalizedPrefix()
    {
        var terms = new Dictionary<string, TaxonomyTerm>
        {
            ["news"] = new TaxonomyTerm("News", "news")
            {
                Pages = [MakePage("p1", "P", "/p1/")]
            }
        };

        var data = TaxonomyDataWriter.BuildKindData("tags", "tags", "Tags", terms, routePrefix: "/topics");

        Assert.Equal("/topics", data["route_prefix"]);
        Assert.Equal("/topics/", data["url"]);
    }

    // ── WriteKind ───────────────────────────────────────────────────

    [Fact]
    public void WriteKind_SerializesValidJson()
    {
        var terms = new Dictionary<string, TaxonomyTerm>
        {
            ["news"] = new TaxonomyTerm("News", "news")
            {
                Weight = 1,
                Pages = [MakePage("p1", "Post 1", "/p1/")]
            }
        };

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            TaxonomyDataWriter.WriteKind(writer, "", "tags", "tags", "Tags", terms);
        }

        using var doc = JsonDocument.Parse(stream.ToArray());
        var root = doc.RootElement;
        Assert.Equal("tags", root.GetProperty("key").GetString());
        Assert.Equal("Tags", root.GetProperty("title").GetString());
        Assert.Equal("/tags/", root.GetProperty("url").GetString());
        Assert.Equal(1, root.GetProperty("terms").GetArrayLength());
        Assert.True(root.GetProperty("itemsByTerm").TryGetProperty("news", out _));
    }

    [Fact]
    public void WriteKind_WithBaseUrl_PrefixesUrls()
    {
        var terms = new Dictionary<string, TaxonomyTerm>
        {
            ["news"] = new TaxonomyTerm("News", "news")
            {
                Pages = [MakePage("p1", "Post 1", "/p1/")]
            }
        };

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            TaxonomyDataWriter.WriteKind(writer, "/blog", "tags", "tags", "Tags", terms);
        }

        using var doc = JsonDocument.Parse(stream.ToArray());
        Assert.StartsWith("/blog", doc.RootElement.GetProperty("url").GetString());
    }

    // ── WriteExtraJson ──────────────────────────────────────────────

    [Fact]
    public void WriteExtraJson_Null_NoOutput()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            TaxonomyDataWriter.WriteExtraJson(writer, null);
            writer.WriteEndObject();
        }

        using var doc = JsonDocument.Parse(stream.ToArray());
        Assert.Empty(doc.RootElement.EnumerateObject());
    }

    [Fact]
    public void WriteExtraJson_SkipsReservedKeys()
    {
        var extra = new Dictionary<string, object>
        {
            ["title"] = "Reserved",
            ["url"] = "/reserved/",
            ["custom"] = "value"
        };

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            TaxonomyDataWriter.WriteExtraJson(writer, extra);
            writer.WriteEndObject();
        }

        using var doc = JsonDocument.Parse(stream.ToArray());
        Assert.False(doc.RootElement.TryGetProperty("title", out _));
        Assert.True(doc.RootElement.TryGetProperty("custom", out _));
    }

    // ── WriteJsonValue ──────────────────────────────────────────────

    [Fact]
    public void WriteJsonValue_AllTypes_SerializedCorrectly()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            TaxonomyDataWriter.WriteJsonValue(writer, "str", "hello");
            TaxonomyDataWriter.WriteJsonValue(writer, "bool", true);
            TaxonomyDataWriter.WriteJsonValue(writer, "int", 42);
            TaxonomyDataWriter.WriteJsonValue(writer, "long", 42L);
            TaxonomyDataWriter.WriteJsonValue(writer, "double", 3.5);
            TaxonomyDataWriter.WriteJsonValue(writer, "decimal", 3.5m);
            TaxonomyDataWriter.WriteJsonValue(writer, "date", new DateTime(2024, 1, 1));
            TaxonomyDataWriter.WriteJsonValue(writer, "dto", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
            TaxonomyDataWriter.WriteJsonValue(writer, "list", new List<object> { "a", "b" });
            TaxonomyDataWriter.WriteJsonValue(writer, "other", new object());
            writer.WriteEndObject();
        }

        using var doc = JsonDocument.Parse(stream.ToArray());
        Assert.Equal("hello", doc.RootElement.GetProperty("str").GetString());
        Assert.True(doc.RootElement.GetProperty("bool").GetBoolean());
        Assert.Equal(42, doc.RootElement.GetProperty("int").GetInt32());
        Assert.Equal(42L, doc.RootElement.GetProperty("long").GetInt64());
        Assert.Equal(3.5, doc.RootElement.GetProperty("double").GetDouble());
        Assert.Equal(2, doc.RootElement.GetProperty("list").GetArrayLength());
    }

    [Fact]
    public void WriteJsonValue_ListWithNulls_SkipsNulls()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            TaxonomyDataWriter.WriteJsonValue(writer, "list", new List<object?> { "a", null, "b" });
            writer.WriteEndObject();
        }

        using var doc = JsonDocument.Parse(stream.ToArray());
        Assert.Equal(2, doc.RootElement.GetProperty("list").GetArrayLength());
    }
}
