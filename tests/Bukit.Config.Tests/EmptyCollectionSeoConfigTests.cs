using System.Text.Json;
using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class EmptyCollectionSeoConfigTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    [Fact]
    public void Load_NoindexWhenEmptyTrue_BindsCollectionSeoPolicy()
    {
        var config = ConfigLoader.Load(WriteConfig(
            """
                noindexWhenEmpty: true
            """));

        var collection = Assert.IsType<CollectionConfig>(config.Site.Collections!["companies"]);
        var property = typeof(CollectionConfig).GetProperty("NoindexWhenEmpty");
        Assert.NotNull(property);
        Assert.True(Assert.IsType<bool>(property.GetValue(collection)));
    }

    [Fact]
    public void Load_OmittedNoindexWhenEmpty_DefaultsFalse()
    {
        var config = ConfigLoader.Load(WriteConfig(string.Empty));

        var collection = Assert.IsType<CollectionConfig>(config.Site.Collections!["companies"]);
        var property = typeof(CollectionConfig).GetProperty("NoindexWhenEmpty");
        Assert.NotNull(property);
        Assert.False(Assert.IsType<bool>(property.GetValue(collection)));
    }

    [Fact]
    public void Schema_CollectionNoindexWhenEmpty_IsBoolean()
    {
        using var schema = JsonDocument.Parse(ConfigJsonSchemaGenerator.Generate());

        var collectionProperties = schema.RootElement
            .GetProperty("properties")
            .GetProperty("site")
            .GetProperty("properties")
            .GetProperty("collections")
            .GetProperty("additionalProperties")
            .GetProperty("properties");

        Assert.Equal(
            "boolean",
            collectionProperties.GetProperty("noindexWhenEmpty").GetProperty("type").GetString());
    }

    [Fact]
    public void Load_NoindexWhenEmpty_IsAcceptedByStrictValidation()
    {
        var config = ConfigLoader.Load(WriteConfig(
            """
                noindexWhenEmpty: true
            """));

        Assert.NotNull(config.Site.Collections);
    }

    [Fact]
    public void Load_UnknownCollectionSibling_RemainsRejected()
    {
        var path = WriteConfig(
            """
                noindexWhenEmpty: true
                noindexWhenEmptyTypo: true
            """);

        var error = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Contains("noindexWhenEmptyTypo", error.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
            }
        }
    }

    private string WriteConfig(string collectionFields)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"bukit-empty-collection-seo-{Guid.NewGuid():N}.yaml");
        var indentedCollectionFields = string.IsNullOrWhiteSpace(collectionFields)
            ? string.Empty
            : "\n" + string.Join(
                "\n",
                collectionFields
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(static line => "      " + line.Trim()));
        var yaml = $$"""
            site:
              name: test
              title: Test
              collections:
                companies:
                  permalink: /companies/:slug/
                  listRoute: /companies/{{indentedCollectionFields}}
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """;
        File.WriteAllText(path, yaml);
        _tempFiles.Add(path);
        return path;
    }
}
