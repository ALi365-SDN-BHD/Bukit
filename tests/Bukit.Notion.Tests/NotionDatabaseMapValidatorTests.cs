using Bukit.Notion.Mapping;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class NotionDatabaseMapValidatorTests : IDisposable
{
    private readonly string _projectRoot;

    public NotionDatabaseMapValidatorTests()
    {
        _projectRoot = Path.Combine(Path.GetTempPath(), "bukit-notion-map-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_projectRoot))
        {
            Directory.Delete(_projectRoot, recursive: true);
        }
    }

    [Fact]
    public void Validate_ValidMapPasses()
    {
        string path = WriteMap(ValidMap());

        NotionDatabaseMapValidationResult result = NotionDatabaseMapValidator.Validate(_projectRoot, path);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("database-map-validation", Assert.Single(result.Artifacts).Type);
    }

    [Fact]
    public void Validate_MissingFileFails()
    {
        NotionDatabaseMapValidationResult result = NotionDatabaseMapValidator.Validate(_projectRoot, "missing.yaml");

        Assert.False(result.Success);
        Assert.Equal("notion.databaseMapNotFound", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Validate_InvalidYamlFails()
    {
        string path = WriteMap("databases: [");

        NotionDatabaseMapValidationResult result = NotionDatabaseMapValidator.Validate(_projectRoot, path);

        Assert.False(result.Success);
        Assert.Equal("notion.databaseMapInvalidYaml", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Validate_MissingDatabasesFails()
    {
        string path = WriteMap("version: 1");

        NotionDatabaseMapValidationResult result = NotionDatabaseMapValidator.Validate(_projectRoot, path);

        Assert.False(result.Success);
        Assert.Equal("notion.databaseMapMissingDatabases", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Validate_MissingDataSourceIdAndDatabaseIdFails()
    {
        string path = WriteMap("""
databases:
  pages:
    seed: pages.json
    collection: page
    uniqueField: Slug
    properties:
      Title:
        source: title
        type: title
      Slug:
        source: slug
        type: rich_text
""");

        NotionDatabaseMapValidationResult result = NotionDatabaseMapValidator.Validate(_projectRoot, path);

        Assert.False(result.Success);
        Assert.Equal("notion.databaseMapMissingDataSource", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Validate_LegacyDatabaseIdPasses()
    {
        string path = WriteMap("""
databases:
  pages:
    seed: pages.json
    collection: page
    databaseId: legacy-db
    uniqueField: Slug
    properties:
      Title:
        source: title
        type: title
      Slug:
        source: slug
        type: rich_text
""");

        NotionDatabaseMapValidationResult result = NotionDatabaseMapValidator.Validate(_projectRoot, path);

        Assert.True(result.Success);
    }

    [Fact]
    public void Validate_UnsupportedPropertyTypeFails()
    {
        string path = WriteMap("""
databases:
  pages:
    seed: pages.json
    collection: page
    dataSourceId: ds
    uniqueField: Slug
    properties:
      Title:
        source: title
        type: title
      Slug:
        source: slug
        type: rich_text
      Bad:
        source: bad
        type: unsupported
""");

        NotionDatabaseMapValidationResult result = NotionDatabaseMapValidator.Validate(_projectRoot, path);

        Assert.False(result.Success);
        Assert.Equal("notion.databaseMapInvalidProperty", Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData("seed", "notion.databaseMapMissingSeed")]
    [InlineData("collection", "notion.databaseMapMissingCollection")]
    [InlineData("uniqueField", "notion.databaseMapMissingUniqueField")]
    public void Validate_MissingRequiredEntryFieldFails(string omittedField, string expectedCode)
    {
        var lines = new List<string>
        {
            "databases:",
            "  pages:",
            "    dataSourceId: ds"
        };
        if (omittedField != "seed")
        {
            lines.Add("    seed: pages.json");
        }

        if (omittedField != "collection")
        {
            lines.Add("    collection: page");
        }

        if (omittedField != "uniqueField")
        {
            lines.Add("    uniqueField: Slug");
        }

        lines.AddRange(
        [
            "    properties:",
            "      Title:",
            "        source: title",
            "        type: title",
            "      Slug:",
            "        source: slug",
            "        type: rich_text"
        ]);

        string path = WriteMap(string.Join(Environment.NewLine, lines));

        NotionDatabaseMapValidationResult result = NotionDatabaseMapValidator.Validate(_projectRoot, path);

        Assert.False(result.Success);
        Assert.Equal(expectedCode, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Validate_ParentDirectorySymlinkOutsideProjectFails()
    {
        string outside = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "bukit-notion-map-parent-outside-" + Guid.NewGuid().ToString("N"))).FullName;
        string outsideSeed = Directory.CreateDirectory(Path.Combine(outside, "notion-seed")).FullName;
        string outsideMap = Path.Combine(outsideSeed, "notion-database-map.yaml");
        File.WriteAllText(outsideMap, ValidMap());
        string linkedParent = Path.Combine(_projectRoot, "linked-parent");

        try
        {
            Directory.CreateSymbolicLink(linkedParent, outside);
            string linkedMap = Path.Combine(linkedParent, "notion-seed", "notion-database-map.yaml");

            NotionDatabaseMapValidationResult result = NotionDatabaseMapValidator.Validate(_projectRoot, linkedMap);

            Assert.False(result.Success);
            Assert.Equal("notion.databaseMapOutsideProject", Assert.Single(result.Diagnostics).Code);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return;
        }
        finally
        {
            if (Directory.Exists(linkedParent))
            {
                Directory.Delete(linkedParent);
            }

            if (Directory.Exists(outside))
            {
                Directory.Delete(outside, recursive: true);
            }
        }
    }

    [Fact]
    public void Validate_MissingPropertiesFails()
    {
        string path = WriteMap("""
databases:
  pages:
    seed: pages.json
    collection: page
    dataSourceId: ds
    uniqueField: Slug
""");

        NotionDatabaseMapValidationResult result = NotionDatabaseMapValidator.Validate(_projectRoot, path);

        Assert.False(result.Success);
        Assert.Equal("notion.databaseMapMissingProperties", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Validate_MissingTitlePropertyFails()
    {
        string path = WriteMap("""
databases:
  pages:
    seed: pages.json
    collection: page
    dataSourceId: ds
    uniqueField: Slug
    properties:
      Slug:
        source: slug
        type: rich_text
""");

        NotionDatabaseMapValidationResult result = NotionDatabaseMapValidator.Validate(_projectRoot, path);

        Assert.False(result.Success);
        Assert.Equal("notion.databaseMapMissingTitleProperty", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Validate_UniqueFieldNotMappedFails()
    {
        string path = WriteMap("""
databases:
  pages:
    seed: pages.json
    collection: page
    dataSourceId: ds
    uniqueField: Slug
    properties:
      Title:
        source: title
        type: title
""");

        NotionDatabaseMapValidationResult result = NotionDatabaseMapValidator.Validate(_projectRoot, path);

        Assert.False(result.Success);
        Assert.Equal("notion.databaseMapUniqueFieldNotMapped", Assert.Single(result.Diagnostics).Code);
    }

    private string WriteMap(string yaml)
    {
        string path = Path.Combine(_projectRoot, "notion-database-map.yaml");
        File.WriteAllText(path, yaml);
        return path;
    }

    private static string ValidMap()
        => """
databases:
  pages:
    title: Pages
    seed: pages.json
    collection: page
    dataSourceId: ds
    uniqueField: Slug
    properties:
      Title:
        source: title
        type: title
      Slug:
        source: slug
        type: rich_text
      Published:
        source: published
        type: checkbox
""";
}
