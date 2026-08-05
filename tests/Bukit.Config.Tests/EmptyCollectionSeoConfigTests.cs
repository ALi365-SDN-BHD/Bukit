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

    [Fact]
    public void Load_IndexPolicyOmitted_DefaultsToMinimumZeroAndIndex()
    {
        var config = ConfigLoader.Load(WriteConfig(string.Empty));

        var collection = Assert.IsType<CollectionConfig>(config.Site.Collections!["companies"]);
        Assert.NotNull(collection.IndexPolicy);
        Assert.Equal(0, collection.IndexPolicy.MinimumItems);
        Assert.Equal("index", collection.IndexPolicy.BelowMinimum);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Load_IndexPolicyMinimumItems_BindsValue(int minimumItems)
    {
        var config = ConfigLoader.Load(WriteConfigRaw(
            $"      indexPolicy:\n        minimumItems: {minimumItems}\n"));

        var collection = Assert.IsType<CollectionConfig>(config.Site.Collections!["companies"]);
        Assert.Equal(minimumItems, collection.IndexPolicy.MinimumItems);
        Assert.Equal("index", collection.IndexPolicy.BelowMinimum);
    }

    [Fact]
    public void Load_IndexPolicyBelowMinimumNoindexFollow_BindsValue()
    {
        var config = ConfigLoader.Load(WriteConfigRaw(
            "      indexPolicy:\n        minimumItems: 3\n        belowMinimum: noindex-follow\n"));

        var collection = Assert.IsType<CollectionConfig>(config.Site.Collections!["companies"]);
        Assert.Equal(3, collection.IndexPolicy.MinimumItems);
        Assert.Equal("noindex-follow", collection.IndexPolicy.BelowMinimum);
    }

    [Fact]
    public void Load_IndexPolicyNegativeMinimumItems_Throws()
    {
        var path = WriteConfigRaw("      indexPolicy:\n        minimumItems: -1\n");

        var error = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Contains("minimumItems", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_IndexPolicyNonIntegerMinimumItems_Throws()
    {
        var path = WriteConfigRaw("      indexPolicy:\n        minimumItems: three\n");

        var error = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Contains("minimumItems", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_IndexPolicyUnknownBelowMinimum_Throws()
    {
        var path = WriteConfigRaw("      indexPolicy:\n        belowMinimum: drop\n");

        var error = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Contains("belowMinimum", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_IndexPolicyUnknownNestedField_Throws()
    {
        var path = WriteConfigRaw("      indexPolicy:\n        minimumItems: 3\n        softLimit: true\n");

        var error = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Contains("softLimit", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_NoindexWhenEmptyAndIndexPolicyTogether_Throws()
    {
        var path = WriteConfigRaw("      noindexWhenEmpty: true\n      indexPolicy:\n        minimumItems: 1\n");

        var error = Assert.Throws<ConfigException>(() => ConfigLoader.Load(path));

        Assert.Contains("noindexWhenEmpty", error.Message, StringComparison.Ordinal);
        Assert.Contains("indexPolicy", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Schema_CollectionIndexPolicy_DefinesBoundsAndEnum()
    {
        using var schema = JsonDocument.Parse(ConfigJsonSchemaGenerator.Generate());

        var collectionProperties = schema.RootElement
            .GetProperty("properties")
            .GetProperty("site")
            .GetProperty("properties")
            .GetProperty("collections")
            .GetProperty("additionalProperties")
            .GetProperty("properties");

        var indexPolicy = collectionProperties.GetProperty("indexPolicy").GetProperty("properties");
        var minimumItems = indexPolicy.GetProperty("minimumItems");
        Assert.Equal("integer", minimumItems.GetProperty("type").GetString());
        Assert.Equal(0, minimumItems.GetProperty("minimum").GetInt32());
        Assert.Equal(
            ["index", "noindex-follow"],
            indexPolicy.GetProperty("belowMinimum").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
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

    private string WriteConfigRaw(string collectionBlock)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"bukit-index-policy-{Guid.NewGuid():N}.yaml");
        var yaml = "site:\n" +
                   "  name: test\n" +
                   "  title: Test\n" +
                   "  collections:\n" +
                   "    companies:\n" +
                   "      permalink: /companies/:slug/\n" +
                   "      listRoute: /companies/\n" +
                   collectionBlock +
                   "content:\n" +
                   "  sources:\n" +
                   "    - type: markdown\n" +
                   "      markdown:\n" +
                   "        dir: content\n";
        File.WriteAllText(path, yaml);
        _tempFiles.Add(path);
        return path;
    }
}
