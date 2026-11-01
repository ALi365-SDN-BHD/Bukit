using Xunit;

namespace Bukit.Importing.Tests;

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

        var markdown = File.ReadAllText(path);
        Assert.Contains("slug: \"hello-labs\"", markdown, StringComparison.Ordinal);
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
        var yaml = markdown.Split("---", StringSplitOptions.RemoveEmptyEntries)[0];
        var stream = new YamlDotNet.RepresentationModel.YamlStream();
        stream.Load(new StringReader(yaml));
        var root = Assert.IsType<YamlDotNet.RepresentationModel.YamlMappingNode>(stream.Documents[0].RootNode);
        var tags = Assert.IsType<YamlDotNet.RepresentationModel.YamlSequenceNode>(
            root.Children[new YamlDotNet.RepresentationModel.YamlScalarNode("tags")]);
        Assert.Equal(new[] { "market", "china" }, tags.Children.Cast<YamlDotNet.RepresentationModel.YamlScalarNode>().Select(node => node.Value));
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
        Assert.Contains("featured: true", markdown, StringComparison.Ordinal);
        Assert.Contains("weight: 3", markdown, StringComparison.Ordinal);
        Assert.Contains("tagline: \"Fast \\\\ \\\"safe\\\"\"", markdown, StringComparison.Ordinal);
        Assert.Contains("Body v1", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Body v2", markdown, StringComparison.Ordinal);
    }
}
