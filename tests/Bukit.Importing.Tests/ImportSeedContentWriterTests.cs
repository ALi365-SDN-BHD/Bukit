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
