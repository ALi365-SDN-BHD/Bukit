using Xunit;
using YamlDotNet.RepresentationModel;

namespace Bukit.Importing.Tests;

[Collection("ImportingConsole")]
public sealed class ImportSeedContentWriterTests : IDisposable
{
    private readonly string _tempDir;

    public ImportSeedContentWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-importing-seed-writer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Fact]
    public void WriteMarkdown_WhenSlugMissing_UsesEffectiveSlugInFrontMatter()
    {
        var record = new ImportSeedRecord(
            Collection: "post",
            Title: "Hello Labs",
            Slug: "",
            Summary: "Summary",
            Content: "Body",
            Language: "en",
            Published: true,
            SeoTitle: null,
            SeoDescription: null);

        var written = ImportSeedContentWriter.WriteMarkdown(_tempDir, [record], overwrite: false);

        Assert.Single(written);

        var path = Path.Combine(_tempDir, "posts", "hello-labs.md");
        Assert.True(File.Exists(path));
        Assert.Equal(path, Assert.Single(written));

        var frontMatter = ReadFrontMatter(File.ReadAllText(path));
        Assert.Equal("hello-labs", Scalar(frontMatter, "slug"));
        Assert.Equal("false", Scalar(frontMatter, "draft"));
    }

    [Fact]
    public void WriteMarkdown_SerializesSequenceExtraFieldsAsYamlLists()
    {
        var record = new ImportSeedRecord(
            Collection: "post",
            Title: "Tagged",
            Slug: "tagged",
            Summary: null,
            Content: null,
            Language: null,
            Published: true,
            SeoTitle: null,
            SeoDescription: null,
            ExtraFields: new Dictionary<string, object?>
            {
                ["tags"] = new object?[] { "market", "china" }
            });

        Assert.Single(ImportSeedContentWriter.WriteMarkdown(_tempDir, [record], overwrite: false));

        var markdown = File.ReadAllText(Path.Combine(_tempDir, "posts", "tagged.md"));
        var root = ReadFrontMatter(markdown);
        var tags = Assert.IsType<YamlSequenceNode>(root.Children[new YamlScalarNode("tags")]);
        Assert.Equal(new[] { "market", "china" }, tags.Children.Cast<YamlScalarNode>().Select(node => node.Value));
        Assert.DoesNotContain("System.Object[]", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteMarkdown_SerializesScalarExtraFields_AndRespectsOverwriteFlag()
    {
        var record = new ImportSeedRecord(
            Collection: "service",
            Title: "Migration",
            Slug: "migration",
            Summary: null,
            Content: "Body v1",
            Language: null,
            Published: false,
            SeoTitle: "SEO",
            SeoDescription: "Desc",
            ExtraFields: new Dictionary<string, object?>
            {
                ["featured"] = true,
                ["weight"] = 3,
                ["tagline"] = "Fast \\ \"safe\""
            });

        Assert.Single(ImportSeedContentWriter.WriteMarkdown(_tempDir, [record], overwrite: false));
        Assert.Empty(ImportSeedContentWriter.WriteMarkdown(_tempDir, [record with { Content = "Body v2" }], overwrite: false));

        var markdown = File.ReadAllText(Path.Combine(_tempDir, "services", "migration.md"));
        var root = ReadFrontMatter(markdown);
        Assert.Equal("true", Scalar(root, "featured"));
        Assert.Equal("3", Scalar(root, "weight"));
        Assert.Equal("Fast \\ \"safe\"", Scalar(root, "tagline"));
        Assert.Contains("Body v1", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Body v2", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteMarkdown_UnpublishedRecord_WritesDraftAndSeoDesc()
    {
        var record = new ImportSeedRecord(
            Collection: "post",
            Title: "Hidden",
            Slug: "hidden",
            Summary: null,
            Content: "Body",
            Language: null,
            Published: false,
            SeoTitle: "SEO title",
            SeoDescription: "SEO description");

        Assert.Single(ImportSeedContentWriter.WriteMarkdown(_tempDir, [record], overwrite: false));

        var root = ReadFrontMatter(File.ReadAllText(Path.Combine(_tempDir, "posts", "hidden.md")));
        Assert.Equal("true", Scalar(root, "draft"));
        Assert.Equal("SEO description", Scalar(root, "seo_desc"));
        Assert.Null(root.Children.TryGetValue(new YamlScalarNode("published"), out _) ? "present" : null);
        Assert.Null(root.Children.TryGetValue(new YamlScalarNode("seo_description"), out _) ? "present" : null);
    }

    [Fact]
    public void WriteMarkdown_UnsafeKeys_ProduceParseableFrontMatter()
    {
        var record = new ImportSeedRecord(
            Collection: "post",
            Title: "Colon: title \"quoted\"",
            Slug: "unsafe",
            Summary: null,
            Content: "Body",
            Language: null,
            Published: true,
            SeoTitle: null,
            SeoDescription: null,
            ExtraFields: new Dictionary<string, object?>
            {
                ["key: with colon"] = "value: with colon",
                ["#hash"] = "- leading dash",
                ["draft"] = "should not duplicate"
            });

        Assert.Single(ImportSeedContentWriter.WriteMarkdown(_tempDir, [record], overwrite: false));

        var root = ReadFrontMatter(File.ReadAllText(Path.Combine(_tempDir, "posts", "unsafe.md")));
        Assert.Equal("Colon: title \"quoted\"", Scalar(root, "title"));
        Assert.Equal("value: with colon", Scalar(root, "key: with colon"));
        Assert.Equal("- leading dash", Scalar(root, "#hash"));
        Assert.Equal("false", Scalar(root, "draft"));
    }

    [Fact]
    public void WriteMarkdown_DuplicateNormalizedSlug_KeepsFirstRecord()
    {
        var first = new ImportSeedRecord(
            Collection: "post", Title: "Hello World", Slug: "hello world", Summary: null,
            Content: "First body", Language: null, Published: true, SeoTitle: null, SeoDescription: null);
        var second = first with { Content = "Second body" };

        var originalError = Console.Error;
        using var capture = new StringWriter();
        try
        {
            Console.SetError(capture);
            var written = ImportSeedContentWriter.WriteMarkdown(_tempDir, [first, second], overwrite: true);

            Assert.Single(written);
            var markdown = File.ReadAllText(Path.Combine(_tempDir, "posts", "hello-world.md"));
            Assert.Contains("First body", markdown, StringComparison.Ordinal);
            Assert.DoesNotContain("Second body", markdown, StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(originalError);
        }

        Assert.Contains("duplicate", capture.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static YamlMappingNode ReadFrontMatter(string markdown)
    {
        var trimmed = markdown.TrimStart();
        Assert.StartsWith("---", trimmed, StringComparison.Ordinal);
        var end = trimmed.IndexOf("\n---", 3, StringComparison.Ordinal);
        Assert.True(end > 0, "front matter is not terminated");
        var yaml = trimmed[3..end];

        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        return Assert.IsType<YamlMappingNode>(stream.Documents[0].RootNode);
    }

    private static string? Scalar(YamlMappingNode root, string key)
        => root.Children.TryGetValue(new YamlScalarNode(key), out var node)
            ? (node as YamlScalarNode)?.Value
            : null;
}
