using Xunit;

namespace Bukit.Importing.Tests;

public sealed class ImportSeedRecordReaderTests : IDisposable
{
    private readonly string _tempDir;

    public ImportSeedRecordReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-importing-seed-reader-" + Guid.NewGuid().ToString("N"));
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

    [Fact]
    public void ReadSeedFile_PreservesJsonArraysAndScalarTypes()
    {
        File.WriteAllText(Path.Combine(_tempDir, "posts.json"), """
[
  {
    "title": "Typed",
    "slug": "typed",
    "tags": ["market", "china"],
    "publish_at": "2026-07-11",
    "website": "https://example.com",
    "priority": 3,
    "featured": true
  }
]
""");

        var record = Assert.Single(ImportSeedRecordReader.ReadSeedFile(_tempDir, "posts.json", "post"));
        var fields = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(record.ExtraFields);

        Assert.Equal(["market", "china"], Assert.IsAssignableFrom<IReadOnlyList<object?>>(fields["tags"]));
        Assert.Equal("2026-07-11", fields["publish_at"]);
        Assert.Equal("https://example.com", fields["website"]);
        Assert.Equal(3L, fields["priority"]);
        Assert.Equal(true, fields["featured"]);
    }

    [Fact]
    public void ReadSeedFile_PreservesYamlScalarSequenceAsReadOnlyArray()
    {
        File.WriteAllText(Path.Combine(_tempDir, "posts.yaml"), """
- title: Typed YAML
  slug: typed-yaml
  tags:
    - market
    - china
    - 7
    - true
""");

        var record = Assert.Single(ImportSeedRecordReader.ReadSeedFile(_tempDir, "posts.yaml", "post"));
        var tags = Assert.IsAssignableFrom<IReadOnlyList<object?>>(record.ExtraFields!["tags"]);

        Assert.Equal(["market", "china", 7L, true], tags);
        Assert.False(tags is object?[]);
    }

    [Theory]
    [InlineData("tags:\n    - market\n    - nested:\n        value: invalid", "mapping")]
    [InlineData("tags:\n    - market\n    - - nested", "sequence")]
    public void ReadSeedFile_RejectsNestedYamlSequenceValues(string yamlField, string expectedKind)
    {
        File.WriteAllText(Path.Combine(_tempDir, "posts.yaml"), $"""
- title: Invalid YAML
  slug: invalid-yaml
  {yamlField}
""");

        var error = Assert.Throws<FormatException>(() =>
            ImportSeedRecordReader.ReadSeedFile(_tempDir, "posts.yaml", "post"));

        Assert.Contains("posts.yaml", error.Message, StringComparison.Ordinal);
        Assert.Contains("tags", error.Message, StringComparison.Ordinal);
        Assert.Contains(expectedKind, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadSeedFile_QuotedYamlScalarsRemainStrings_AndPlainScalarsUseInvariantTypes()
    {
        File.WriteAllText(Path.Combine(_tempDir, "posts.yaml"), """
- title: YAML Scalars
  slug: yaml-scalars
  quoted_true: "true"
  quoted_code: '00123'
  quoted_decimal: "1.25"
  tagged_true: !!str true
  plain_null: null
  plain_true: true
  plain_integer: 123
  plain_decimal: 1.25
""");
        var previousCulture = System.Globalization.CultureInfo.CurrentCulture;
        var previousUiCulture = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            var culture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            System.Globalization.CultureInfo.CurrentCulture = culture;
            System.Globalization.CultureInfo.CurrentUICulture = culture;

            var record = Assert.Single(ImportSeedRecordReader.ReadSeedFile(_tempDir, "posts.yaml", "post"));
            var fields = record.ExtraFields!;

            Assert.Equal("true", fields["quoted_true"]);
            Assert.Equal("00123", fields["quoted_code"]);
            Assert.Equal("1.25", fields["quoted_decimal"]);
            Assert.Equal("true", fields["tagged_true"]);
            Assert.Null(fields["plain_null"]);
            Assert.Equal(true, fields["plain_true"]);
            Assert.Equal(123L, fields["plain_integer"]);
            Assert.Equal(1.25d, Assert.IsType<double>(fields["plain_decimal"]));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previousCulture;
            System.Globalization.CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }
}
