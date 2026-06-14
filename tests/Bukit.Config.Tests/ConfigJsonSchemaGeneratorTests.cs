using System.Text.Json;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class ConfigJsonSchemaGeneratorTests
{
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
    }
}
