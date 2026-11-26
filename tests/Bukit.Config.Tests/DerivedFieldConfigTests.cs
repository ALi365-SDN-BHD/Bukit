using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class DerivedFieldConfigTests
{
    [Fact]
    public void Load_DerivedFields_KeepsDeclarationOrderAndDefaults()
    {
        var config = Load("""
              derivedFields:
                city:
                  seo_title: { value: "{{ name }} guide" }
                  summary: { value: "{{ name }}: {{ summary }}", mode: override }
                route:
                  seo_desc: { value: "{{ summary | seo.describe 140 }}" }
            """);

        ConfigValidator.Validate(config);
        Assert.Collection(
            config.Content.DerivedFields,
            item => Assert.Equal(("city", "seo_title", "{{ name }} guide", "default"), (item.Collection, item.Field, item.Value, item.Mode)),
            item => Assert.Equal(("city", "summary", "override"), (item.Collection, item.Field, item.Mode)),
            item => Assert.Equal(("route", "seo_desc"), (item.Collection, item.Field)));
    }

    [Theory]
    [InlineData("seo_title: { mode: override }", "content.derivedFields.city.seo_title.value is required.")]
    [InlineData("seo_title: { value: x, when: y }", "Unknown config field 'content.derivedFields.city.seo_title.when'")]
    public void Load_InvalidRule_Throws(string rule, string message)
    {
        var exception = Assert.Throws<ConfigException>(() => Load($$"""
              derivedFields:
                city:
                  {{rule}}
            """));

        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("slug: { value: x }", "content.derivedFields.city.slug must be a field name other than")]
    [InlineData("url: { value: x }", "content.derivedFields.city.url must be a field name other than")]
    [InlineData("Permalink: { value: x }", "content.derivedFields.city.Permalink must be a field name other than")]
    [InlineData("'bad name': { value: x }", "content.derivedFields.city.bad name must be a field name")]
    [InlineData("seo_title: { value: x, mode: always }", "content.derivedFields.city.seo_title.mode must be default or override.")]
    public void Validate_InvalidRule_Throws(string rule, string message)
    {
        var exception = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(Load($$"""
              derivedFields:
                city:
                  {{rule}}
            """)));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("route", "cities_links")]
    [InlineData("city", "routes")]
    [InlineData("city", "routes_count")]
    public void Validate_FieldThatALinkWrites_Throws(string collection, string field)
    {
        var exception = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(Load($$"""
              links:
                - { collection: route, field: cities, target: { collection: city }, backref: { name: routes } }
              derivedFields:
                {{collection}}:
                  {{field}}: { value: x, mode: override }
            """)));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains($"content.derivedFields.{collection}.{field} is a field that content.links writes", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("customFields:\n    - { name: rating, fieldType: number }", "number")]
    [InlineData("fieldScopes:\n    City:\n      - { name: rating, fieldType: Bool }", "Bool")]
    [InlineData("customFields:\n    - { name: rating, fieldType: list }", "list")]
    public void Validate_FieldTheSchemaTypesAsNonText_Throws(string schema, string type)
    {
        var exception = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(Load($$"""
              modelSchema:
                {{schema.Replace("\n", "\n    ", StringComparison.Ordinal)}}
              derivedFields:
                city:
                  rating: { value: x }
            """)));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains($"content.derivedFields.city.rating writes text, but content.modelSchema declares 'rating' as {type}.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_FieldTheSchemaTypesAsTextOrInAnotherCollection_Passes()
    {
        ConfigValidator.Validate(Load("""
              modelSchema:
                customFields:
                  - { name: seo_title, fieldType: string }
                fieldScopes:
                  route:
                    - { name: rating, fieldType: number }
              derivedFields:
                city:
                  seo_title: { value: x }
                  rating: { value: x }
            """));
    }

    private static AppConfig Load(string contentYaml)
    {
        var yaml = $$"""
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            {{contentYaml}}
            """;
        var path = Path.Combine(Path.GetTempPath(), $"bukit-derived-{Guid.NewGuid():N}.yaml");
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
