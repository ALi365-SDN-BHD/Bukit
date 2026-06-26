using Bukit.Notion.Seed;
using Xunit;

namespace Bukit.Notion.Tests;

public sealed class NotionSeedValidatorTests : IDisposable
{
    private readonly string _projectRoot;

    public NotionSeedValidatorTests()
    {
        _projectRoot = Path.Combine(Path.GetTempPath(), "bukit-notion-seed-" + Guid.NewGuid().ToString("N"));
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
    public void Validate_ValidSeedPasses()
    {
        string seedDir = CreateSeedDir();
        WriteSeed(seedDir, "pages.json", """
[
  {
    "title": "Home",
    "slug": "home",
    "published": true,
    "tags": ["featured"]
  }
]
""");

        NotionSeedValidationResult result = NotionSeedValidator.Validate(_projectRoot, seedDir);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("seed-validation", Assert.Single(result.Artifacts).Type);
    }

    [Fact]
    public void Validate_MissingSeedDirFails()
    {
        NotionSeedValidationResult result = NotionSeedValidator.Validate(_projectRoot, "missing");

        Assert.False(result.Success);
        Assert.Equal("notion.seedDirNotFound", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Validate_SeedDirSymlinkOutsideProjectFails()
    {
        string outside = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "bukit-notion-seed-outside-" + Guid.NewGuid().ToString("N"))).FullName;
        string linked = Path.Combine(_projectRoot, "notion-seed");

        try
        {
            Directory.CreateSymbolicLink(linked, outside);
            NotionSeedValidationResult result = NotionSeedValidator.Validate(_projectRoot, linked);

            Assert.False(result.Success);
            Assert.Equal("notion.seedDirOutsideProject", Assert.Single(result.Diagnostics).Code);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return;
        }
        finally
        {
            if (Directory.Exists(linked))
            {
                Directory.Delete(linked);
            }

            if (Directory.Exists(outside))
            {
                Directory.Delete(outside, recursive: true);
            }
        }
    }

    [Fact]
    public void Validate_NoSeedFilesFails()
    {
        string seedDir = CreateSeedDir();

        NotionSeedValidationResult result = NotionSeedValidator.Validate(_projectRoot, seedDir);

        Assert.False(result.Success);
        Assert.Equal("notion.seedNoFiles", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Validate_InvalidJsonFails()
    {
        string seedDir = CreateSeedDir();
        WriteSeed(seedDir, "pages.json", "{");

        NotionSeedValidationResult result = NotionSeedValidator.Validate(_projectRoot, seedDir);

        Assert.False(result.Success);
        Assert.Equal("notion.seedInvalidJson", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Validate_RecordWithoutTitleOrNameFails()
    {
        string seedDir = CreateSeedDir();
        WriteSeed(seedDir, "pages.json", """
[
  {
    "slug": "home"
  }
]
""");

        NotionSeedValidationResult result = NotionSeedValidator.Validate(_projectRoot, seedDir);

        Assert.False(result.Success);
        Assert.Equal("notion.seedMissingTitle", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Validate_InvalidPublishedFails()
    {
        string seedDir = CreateSeedDir();
        WriteSeed(seedDir, "pages.json", """
[
  {
    "title": "Home",
    "published": "true"
  }
]
""");

        NotionSeedValidationResult result = NotionSeedValidator.Validate(_projectRoot, seedDir);

        Assert.False(result.Success);
        Assert.Equal("notion.seedInvalidPublished", Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData("[\"featured\"]")]
    [InlineData("\"featured\"")]
    public void Validate_TagsArrayOrStringPasses(string tagsJson)
    {
        string seedDir = CreateSeedDir();
        WriteSeed(seedDir, "posts.json", $$"""
[
  {
    "title": "Post",
    "tags": {{tagsJson}}
  }
]
""");

        NotionSeedValidationResult result = NotionSeedValidator.Validate(_projectRoot, seedDir);

        Assert.True(result.Success);
    }

    [Fact]
    public void Validate_InvalidTagsFails()
    {
        string seedDir = CreateSeedDir();
        WriteSeed(seedDir, "posts.json", """
[
  {
    "title": "Post",
    "tags": 123
  }
]
""");

        NotionSeedValidationResult result = NotionSeedValidator.Validate(_projectRoot, seedDir);

        Assert.False(result.Success);
        Assert.Equal("notion.seedInvalidTags", Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Validate_RecordArrayItemMustBeObject()
    {
        string seedDir = CreateSeedDir();
        WriteSeed(seedDir, "pages.json", """
[
  "not-object"
]
""");

        NotionSeedValidationResult result = NotionSeedValidator.Validate(_projectRoot, seedDir);

        Assert.False(result.Success);
        Assert.Equal("notion.seedInvalidRecord", Assert.Single(result.Diagnostics).Code);
    }

    private string CreateSeedDir()
        => Directory.CreateDirectory(Path.Combine(_projectRoot, "notion-seed")).FullName;

    private static void WriteSeed(string seedDir, string fileName, string json)
        => File.WriteAllText(Path.Combine(seedDir, fileName), json);
}
