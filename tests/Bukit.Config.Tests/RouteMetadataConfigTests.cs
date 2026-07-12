using System.Text.Json;
using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class RouteMetadataConfigTests
{
    [Fact]
    public void Load_RouteMetadata_ParsesAllFields()
    {
        var config = Load("""
            content:
              sources:
                - type: markdown
                  name: page_meta
                  mode: data
                  markdown:
                    dir: data/page-meta
              routeMetadata:
                source: page_meta
                routeField: route_path
                titleField: page_title
                summaryField: page_summary
                seoTitleField: seo_title_text
                seoDescriptionField: seo_description_text
                requiredRoutes:
                  - /
                  - /insights/
            """);

        var metadata = Assert.IsType<RouteMetadataConfig>(config.Content.RouteMetadata);
        Assert.Equal("page_meta", metadata.Source);
        Assert.Equal("route_path", metadata.RouteField);
        Assert.Equal("page_title", metadata.TitleField);
        Assert.Equal("page_summary", metadata.SummaryField);
        Assert.Equal("seo_title_text", metadata.SeoTitleField);
        Assert.Equal("seo_description_text", metadata.SeoDescriptionField);
        Assert.Equal(["/", "/insights/"], metadata.RequiredRoutes);
    }

    [Fact]
    public void Load_RouteMetadata_UsesFieldDefaults()
    {
        var config = Load("""
            content:
              sources:
                - type: markdown
                  name: page_meta
                  mode: data
                  markdown:
                    dir: data/page-meta
              routeMetadata:
                source: page_meta
            """);

        var metadata = Assert.IsType<RouteMetadataConfig>(config.Content.RouteMetadata);
        Assert.Equal("route", metadata.RouteField);
        Assert.Equal("title", metadata.TitleField);
        Assert.Equal("summary", metadata.SummaryField);
        Assert.Equal("seo_title", metadata.SeoTitleField);
        Assert.Equal("seo_description", metadata.SeoDescriptionField);
        Assert.Empty(metadata.RequiredRoutes);
    }

    [Fact]
    public void Load_RouteMetadataWithoutSource_Throws()
    {
        var ex = Assert.Throws<ConfigException>(() => Load("""
            content:
              sources:
                - type: markdown
                  name: page_meta
                  mode: data
                  markdown:
                    dir: data/page-meta
              routeMetadata:
                routeField: route
            """));

        Assert.Contains("content.routeMetadata.source", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("source", "page-meta")]
    [InlineData("routeField", "Route")]
    [InlineData("titleField", "page-title")]
    [InlineData("summaryField", "summary.value")]
    [InlineData("seoTitleField", "9seo_title")]
    [InlineData("seoDescriptionField", "seo description")]
    public void Validate_RouteMetadataWithUnsafeIdentifier_Throws(string field, string value)
    {
        var sourceName = field == "source" ? value : "page_meta";
        var fieldOverride = field == "source" ? string.Empty : $"{field}: {value}";
        var config = Load($$"""
            content:
              sources:
                - type: markdown
                  name: {{sourceName}}
                  mode: data
                  markdown:
                    dir: data/page-meta
              routeMetadata:
                source: {{sourceName}}
                {{fieldOverride}}
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains($"content.routeMetadata.{field}", ex.Message, StringComparison.Ordinal);
        Assert.Contains("^[a-z][a-z0-9_]*$", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("source", " page_meta")]
    [InlineData("source", "page_meta ")]
    [InlineData("routeField", " route")]
    [InlineData("routeField", "route ")]
    public void Validate_RouteMetadataWithIdentifierBoundaryWhitespace_Throws(string field, string value)
    {
        var source = field == "source" ? value : "page_meta";
        var fieldOverride = field == "source" ? string.Empty : $"{field}: \"{value}\"";
        var config = Load($$"""
            content:
              sources:
                - type: markdown
                  name: page_meta
                  mode: data
                  markdown:
                    dir: data/page-meta
              routeMetadata:
                source: "{{source}}"
                {{fieldOverride}}
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains($"content.routeMetadata.{field}", ex.Message, StringComparison.Ordinal);
        Assert.Contains("^[a-z][a-z0-9_]*$", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RouteMetadataWithDuplicateRequiredRoute_Throws()
    {
        var config = LoadWithRequiredRoutes("- /insights/\n      - /insights/");

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("requiredRoutes contains duplicate route '/insights/'", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("insights/")]
    [InlineData("/insights")]
    public void Validate_RouteMetadataWithInvalidRequiredRoute_Throws(string route)
    {
        var config = LoadWithRequiredRoutes($"- {route}");

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("requiredRoutes values must start and end with '/'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RouteMetadataWithUnknownSource_Throws()
    {
        var config = Load("""
            content:
              sources:
                - type: markdown
                  name: settings
                  mode: data
                  markdown:
                    dir: data/settings
              routeMetadata:
                source: page_meta
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("references unknown data source 'page_meta'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RouteMetadataSourceInContentMode_Throws()
    {
        var config = Load("""
            content:
              sources:
                - type: markdown
                  name: page_meta
                  mode: content
                  markdown:
                    dir: content/pages
              routeMetadata:
                source: page_meta
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("must reference a source with mode: data", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RouteMetadataSourceWithDataIndex_Throws()
    {
        var config = Load("""
            content:
              sources:
                - type: markdown
                  name: page_meta
                  mode: data
                  markdown:
                    dir: data/page-meta
                  dataIndex:
                    scopeField: scope
                    keyField: key
                    valueField: value
                    valueTypeField: value_type
              routeMetadata:
                source: page_meta
            """);

        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(config));

        Assert.Contains("must not declare dataIndex", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_RouteMetadataWithUnknownField_Throws()
    {
        var ex = Assert.Throws<ConfigException>(() => Load("""
            content:
              sources:
                - type: markdown
                  name: page_meta
                  mode: data
                  markdown:
                    dir: data/page-meta
              routeMetadata:
                source: page_meta
                unknownField: value
            """));

        Assert.Contains("content.routeMetadata.unknownField", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_RouteMetadataSchema_ContainsContract()
    {
        using var document = JsonDocument.Parse(ConfigJsonSchemaGenerator.Generate());
        var routeMetadata = document.RootElement
            .GetProperty("properties")
            .GetProperty("content")
            .GetProperty("properties")
            .GetProperty("routeMetadata");
        var properties = routeMetadata.GetProperty("properties");

        Assert.Contains("source", routeMetadata.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal("string", properties.GetProperty("source").GetProperty("type").GetString());
        Assert.Equal("string", properties.GetProperty("routeField").GetProperty("type").GetString());
        Assert.Equal("string", properties.GetProperty("titleField").GetProperty("type").GetString());
        Assert.Equal("string", properties.GetProperty("summaryField").GetProperty("type").GetString());
        Assert.Equal("string", properties.GetProperty("seoTitleField").GetProperty("type").GetString());
        Assert.Equal("string", properties.GetProperty("seoDescriptionField").GetProperty("type").GetString());
        Assert.Equal("array", properties.GetProperty("requiredRoutes").GetProperty("type").GetString());
        Assert.False(routeMetadata.GetProperty("additionalProperties").GetBoolean());
    }

    private static AppConfig LoadWithRequiredRoutes(string requiredRoutes)
        => Load($$"""
            content:
              sources:
                - type: markdown
                  name: page_meta
                  mode: data
                  markdown:
                    dir: data/page-meta
              routeMetadata:
                source: page_meta
                requiredRoutes:
                  {{requiredRoutes}}
            """);

    private static AppConfig Load(string contentYaml)
    {
        var yaml = $$"""
            site:
              name: myblog
              title: My Blog
            {{contentYaml}}
            """;
        var path = Path.Combine(Path.GetTempPath(), $"bukit-route-metadata-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        try
        {
            return ConfigLoader.Load(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
