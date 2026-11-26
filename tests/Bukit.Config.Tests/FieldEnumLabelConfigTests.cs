using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class FieldEnumLabelConfigTests
{
    [Fact]
    public void Load_EnumItemsWithLabels_KeepOrderAndLabels()
    {
        var field = Load("""
                  customFields:
                    - name: audience
                      enum:
                        - { value: family, label: 亲子 }
                        - couple
                        - { value: solo }
            """).Content.ModelSchema!.CustomFields![0];

        Assert.Equal(["family", "couple", "solo"], field.Enum);
        Assert.Equal("亲子", Assert.Single(field.EnumLabels!).Value);
    }

    [Fact]
    public void Load_PlainEnum_HasNoLabels()
        => Assert.Null(Load("""
                  customFields:
                    - { name: audience, enum: [family, couple] }
            """).Content.ModelSchema!.CustomFields![0].EnumLabels);

    [Theory]
    [InlineData("[{ label: x }]", "enum[0].value is required.")]
    [InlineData("[{ value: a, title: x }]", "Unknown config field 'content.modelSchema.customFields[0].enum[0].title'")]
    [InlineData("[a, { value: a, label: A }]", "enum lists 'a' more than once.")]
    [InlineData("[[a]]", "enum[0] must be a value or a mapping with value and label.")]
    public void Load_InvalidEnum_Throws(string items, string message)
    {
        var ex = Assert.Throws<ConfigException>(() => Load($$"""
                  customFields:
                    - { name: audience, enum: {{items}} }
            """));

        Assert.Contains(message, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_EnumFieldNamedScopes_Throws()
    {
        var ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(Load("""
                  customFields:
                    - { name: scopes, enum: [a] }
            """)));

        Assert.Contains("cannot be named 'scopes'", ex.Message, StringComparison.Ordinal);
    }

    private static AppConfig Load(string modelSchemaYaml)
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
              modelSchema:
            {{modelSchemaYaml}}
            """;
        var path = Path.Combine(Path.GetTempPath(), $"bukit-enum-{Guid.NewGuid():N}.yaml");
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
