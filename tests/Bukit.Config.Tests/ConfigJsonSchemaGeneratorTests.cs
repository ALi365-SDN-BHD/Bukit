using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class ConfigJsonSchemaGeneratorTests
{
    private static readonly IReadOnlySet<string> AllowedOpenMapObjectSchemaPaths = new HashSet<string>(StringComparer.Ordinal)
    {
        "$/properties/site/properties/permalinks",
        "$/properties/site/properties/collections",
        "$/properties/site/properties/plugins",
        "$/properties/theme/properties/params",
        "$/properties/theme/properties/shortcodes",
        "$/properties/theme/properties/components",
        "$/properties/theme/properties/components/additionalProperties/properties/props",
        "$/properties/site/properties/menus",
        "$/properties/taxonomy/properties/pinFieldBySource",
        "$/properties/taxonomy/properties/pinOrderFieldBySource",
        "$/properties/content/properties/modelSchema/properties/fieldScopes"
    };

    [Fact]
    public void Generate_ReturnsObjectSchemaForAppConfigWithRequiredRoots()
    {
        var json = ConfigJsonSchemaGenerator.Generate();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("https://json-schema.org/draft/2020-12/schema", root.GetProperty("$schema").GetString());
        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.Contains("site", root.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("content", root.GetProperty("required").EnumerateArray().Select(x => x.GetString()));

        var properties = root.GetProperty("properties");
        Assert.True(properties.TryGetProperty("site", out var site));
        Assert.True(site.GetProperty("properties").TryGetProperty("title", out var title));
        Assert.Equal("string", title.GetProperty("type").GetString());
        Assert.True(properties.TryGetProperty("build", out var build));
        Assert.Equal("boolean", build.GetProperty("properties").GetProperty("clean").GetProperty("type").GetString());
    }

    [Fact]
    public void Generate_StableConfigContractFields_MatchLoaderAndStrictValidatorNames()
    {
        var json = ConfigJsonSchemaGenerator.Generate();
        using var doc = JsonDocument.Parse(json);
        var properties = doc.RootElement.GetProperty("properties");

        var geo = properties
            .GetProperty("site")
            .GetProperty("properties")
            .GetProperty("seo")
            .GetProperty("properties")
            .GetProperty("geo")
            .GetProperty("properties");
        Assert.True(geo.TryGetProperty("enabled", out _));
        Assert.True(geo.TryGetProperty("llmsTxtMaxArticles", out _));
        Assert.True(geo.TryGetProperty("aiBotMode", out _));
        Assert.True(geo.TryGetProperty("aiBotAllowList", out _));
        Assert.True(geo.TryGetProperty("aiBotBlockList", out _));
        Assert.True(geo.TryGetProperty("llmsTxtOptionalLinks", out _));
        Assert.False(geo.TryGetProperty("faqSchema", out _));

        var collectionPagination = properties
            .GetProperty("site")
            .GetProperty("properties")
            .GetProperty("collections")
            .GetProperty("additionalProperties")
            .GetProperty("properties")
            .GetProperty("pagination")
            .GetProperty("properties");
        Assert.True(collectionPagination.TryGetProperty("enabled", out _));
        Assert.True(collectionPagination.TryGetProperty("pageSize", out _));
        Assert.True(collectionPagination.TryGetProperty("urlPattern", out _));
        Assert.True(collectionPagination.TryGetProperty("firstPageUsesListRoute", out _));
        Assert.Equal(1, collectionPagination.GetProperty("pageSize").GetProperty("minimum").GetInt32());
        Assert.Equal("boolean", collectionPagination.GetProperty("firstPageUsesListRoute").GetProperty("type").GetString());

        var filteredListItem = properties
            .GetProperty("site")
            .GetProperty("properties")
            .GetProperty("collections")
            .GetProperty("additionalProperties")
            .GetProperty("properties")
            .GetProperty("filteredLists")
            .GetProperty("items");
        var filteredList = filteredListItem.GetProperty("properties");
        Assert.True(filteredList.TryGetProperty("field", out _));
        Assert.True(filteredList.TryGetProperty("operator", out _));
        Assert.True(filteredList.TryGetProperty("value", out _));
        Assert.True(filteredList.TryGetProperty("values", out _));
        Assert.True(filteredList.TryGetProperty("listRoute", out _));
        Assert.True(filteredList.TryGetProperty("listTemplate", out _));
        Assert.True(filteredList.TryGetProperty("pageSize", out _));
        Assert.True(filteredList.TryGetProperty("urlPattern", out _));
        Assert.True(filteredList.TryGetProperty("emptyBehavior", out _));
        Assert.Equal(new[] { "field", "listRoute" }, filteredListItem.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(new[] { "equals", "contains", "in" }, filteredList.GetProperty("operator").GetProperty("enum").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(new[] { "render", "skip" }, filteredList.GetProperty("emptyBehavior").GetProperty("enum").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(1, filteredList.GetProperty("pageSize").GetProperty("minimum").GetInt32());

        var scss = properties
            .GetProperty("theme")
            .GetProperty("properties")
            .GetProperty("scss")
            .GetProperty("properties");
        Assert.True(scss.TryGetProperty("outputDir", out _));
        Assert.False(scss.TryGetProperty("outDir", out _));
        Assert.False(scss.TryGetProperty("includePaths", out _));

        var propertyMap = properties
            .GetProperty("content")
            .GetProperty("properties")
            .GetProperty("sources")
            .GetProperty("items")
            .GetProperty("properties")
            .GetProperty("notion")
            .GetProperty("properties")
            .GetProperty("propertyMap")
            .GetProperty("properties");
        Assert.True(propertyMap.TryGetProperty("Title", out _));
        Assert.True(propertyMap.TryGetProperty("PublishAt", out _));
        Assert.True(propertyMap.TryGetProperty("SeoDescription", out _));
        Assert.True(propertyMap.TryGetProperty("Canonical", out _));
        Assert.False(propertyMap.TryGetProperty("lang", out _));

        var taxonomy = properties
            .GetProperty("taxonomy")
            .GetProperty("properties");
        Assert.True(taxonomy.TryGetProperty("outputMode", out _));
        Assert.True(taxonomy.TryGetProperty("itemFields", out _));
        Assert.True(taxonomy.TryGetProperty("pageSize", out _));
        Assert.True(taxonomy.TryGetProperty("indexEnabled", out _));
        Assert.True(taxonomy.TryGetProperty("pinField", out _));
        Assert.True(taxonomy.TryGetProperty("pinOrderField", out _));
        Assert.True(taxonomy.TryGetProperty("pinFieldBySource", out _));
        Assert.True(taxonomy.TryGetProperty("pinOrderFieldBySource", out _));

        var taxonomyKind = taxonomy
            .GetProperty("kinds")
            .GetProperty("items")
            .GetProperty("properties");
        Assert.True(taxonomyKind.TryGetProperty("routePrefix", out _));
    }

    [Fact]
    public void Generate_AllObjectSchemasDeclareAdditionalProperties()
    {
        var json = ConfigJsonSchemaGenerator.Generate();
        using var doc = JsonDocument.Parse(json);

        AssertObjectSchemasDeclareAdditionalProperties(doc.RootElement, "$");
    }

    [Fact]
    public void Generate_ExplicitlyAllowsDynamicMapLocations()
    {
        var json = ConfigJsonSchemaGenerator.Generate();
        using var doc = JsonDocument.Parse(json);
        var properties = doc.RootElement.GetProperty("properties");

        var site = properties.GetProperty("site").GetProperty("properties");
        var permalinks = site.GetProperty("permalinks").GetProperty("additionalProperties");
        Assert.Equal("string", permalinks.GetProperty("type").GetString());

        var plugins = site.GetProperty("plugins").GetProperty("additionalProperties");
        Assert.Equal(JsonValueKind.True, plugins.ValueKind);

        var theme = properties.GetProperty("theme").GetProperty("properties");
        var themeParams = theme.GetProperty("params").GetProperty("additionalProperties");
        Assert.Equal(JsonValueKind.True, themeParams.ValueKind);

        var shortcodes = theme.GetProperty("shortcodes").GetProperty("additionalProperties");
        Assert.Equal("string", shortcodes.GetProperty("type").GetString());

        var componentProps = theme.GetProperty("components").GetProperty("additionalProperties")
            .GetProperty("properties").GetProperty("props")
            .GetProperty("additionalProperties");
        Assert.Equal("string", componentProps.GetProperty("type").GetString());

        var menus = site.GetProperty("menus").GetProperty("additionalProperties");
        Assert.Equal("array", menus.GetProperty("type").GetString());
        Assert.Equal("object", menus.GetProperty("items").GetProperty("type").GetString());

        var taxonomy = properties.GetProperty("taxonomy").GetProperty("properties");
        Assert.Equal("string", taxonomy.GetProperty("pinFieldBySource").GetProperty("additionalProperties").GetProperty("type").GetString());
        Assert.Equal("string", taxonomy.GetProperty("pinOrderFieldBySource").GetProperty("additionalProperties").GetProperty("type").GetString());

        var fieldScopes = properties
            .GetProperty("content").GetProperty("properties")
            .GetProperty("modelSchema").GetProperty("properties")
            .GetProperty("fieldScopes");
        var fieldScopesAddl = fieldScopes.GetProperty("additionalProperties");
        Assert.Equal("array", fieldScopesAddl.GetProperty("type").GetString());
        Assert.Equal("object", fieldScopesAddl.GetProperty("items").GetProperty("type").GetString());
    }

    private static void AssertObjectSchemasDeclareAdditionalProperties(JsonElement node, string path)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            if (node.TryGetProperty("type", out var type) &&
                type.ValueKind == JsonValueKind.String &&
                type.GetString() == "object")
            {
                Assert.True(node.TryGetProperty("additionalProperties", out var additionalProperties), $"Object schema at {path} must declare additionalProperties");

                if (!AllowedOpenMapObjectSchemaPaths.Contains(path) &&
                    additionalProperties.ValueKind != JsonValueKind.False)
                {
                    Assert.Fail($"Object schema at {path} must be additionalProperties=false");
                }
            }

            foreach (var property in node.EnumerateObject())
            {
                AssertObjectSchemasDeclareAdditionalProperties(property.Value, $"{path}/{property.Name}");
            }

            return;
        }

        if (node.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in node.EnumerateArray())
            {
                AssertObjectSchemasDeclareAdditionalProperties(item, $"{path}[{index}]");
                index++;
            }
        }
    }
}
