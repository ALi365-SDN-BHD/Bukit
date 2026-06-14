using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

public sealed class ImportSeedRecordReaderTests : IDisposable
{
    private readonly string _tempDir;

    public ImportSeedRecordReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-labs-import-seed-reader-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Fact]
    public void ReadDirectory_ParsesJsonAndYaml_WithTypeNormalizationAndExtraFields()
    {
        File.WriteAllText(Path.Combine(_tempDir, "posts.json"), """
[
  {
    "title": "Hello",
    "type": "articles",
    "slug": "hello",
    "published": false,
    "language": "en",
    "seo_title": "SEO",
    "seo_description": "Description",
    "priority": 9,
    "rating": 4.5,
    "featured": true
  },
  {
    "slug": "ignored-without-title"
  }
]
""");

        File.WriteAllText(Path.Combine(_tempDir, "navigation.yaml"), """
- name: Main nav
  type: menus
  slug: main-nav
  summary: links
  sticky: true
  order: 2
""");

        var records = ImportSeedRecordReader.ReadDirectory(_tempDir);

        Assert.Equal(2, records.Count);

        var post = Assert.Single(records, r => r.Collection == "post");
        Assert.Equal("Hello", post.Title);
        Assert.False(post.Published);
        Assert.Equal("en", post.Language);
        Assert.Equal("SEO", post.SeoTitle);
        Assert.Equal("Description", post.SeoDescription);
        Assert.NotNull(post.ExtraFields);
        Assert.Equal(9L, post.ExtraFields!["priority"]);
        Assert.Equal(4.5d, Assert.IsType<double>(post.ExtraFields["rating"]));
        Assert.Equal(true, post.ExtraFields["featured"]);

        var nav = Assert.Single(records, r => r.Collection == "navigation");
        Assert.Equal("Main nav", nav.Title);
        Assert.Equal("main-nav", nav.Slug);
        Assert.Equal("links", nav.Summary);
        Assert.NotNull(nav.ExtraFields);
        Assert.Equal(true, nav.ExtraFields!["sticky"]);
        Assert.Equal(2L, nav.ExtraFields["order"]);
    }

    [Fact]
    public void ReadSeedFile_MissingOrUnsupportedExtension_ReturnsEmpty()
    {
        File.WriteAllText(Path.Combine(_tempDir, "pages.txt"), "not supported");

        Assert.Empty(ImportSeedRecordReader.ReadSeedFile(_tempDir, "pages.txt", "page"));
        Assert.Empty(ImportSeedRecordReader.ReadSeedFile(_tempDir, "missing.json", "page"));
    }
}
