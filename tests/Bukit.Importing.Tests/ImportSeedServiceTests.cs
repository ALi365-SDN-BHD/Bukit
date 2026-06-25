using Bukit.Importing.Seed;
using Xunit;

namespace Bukit.Importing.Tests;

public sealed class ImportSeedServiceTests : IDisposable
{
    private readonly string _projectRoot;

    public ImportSeedServiceTests()
    {
        _projectRoot = Path.Combine(Path.GetTempPath(), "bukit-import-seed-service-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectRoot);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_projectRoot, recursive: true);
    }

    [Fact]
    public void Import_WhenSeedDirectoryMissing_ReturnsFailureDiagnostic()
    {
        var service = new ImportSeedService();

        ImportSeedResult result = service.Import(new ImportSeedOptions(
            ProjectRoot: _projectRoot,
            SeedDirectory: Path.Combine(_projectRoot, "missing-seed"),
            OutputDirectory: Path.Combine(_projectRoot, "content"),
            Force: false));

        Assert.False(result.Success);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "import.seedDirNotFound");
        Assert.Empty(result.Artifacts);
    }

    [Fact]
    public void Import_WhenOutputDirectoryMissing_ReturnsFailureDiagnostic()
    {
        string seedDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "seed")).FullName;
        var service = new ImportSeedService();

        ImportSeedResult result = service.Import(new ImportSeedOptions(
            ProjectRoot: _projectRoot,
            SeedDirectory: seedDir,
            OutputDirectory: "",
            Force: false));

        Assert.False(result.Success);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "import.missingOutput");
    }

    [Fact]
    public void Import_WhenOutputDirectoryEscapesProjectRoot_ReturnsFailureDiagnostic()
    {
        string seedDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "seed")).FullName;
        string outsideOutput = Path.Combine(Path.GetTempPath(), "bukit-import-seed-outside-" + Guid.NewGuid().ToString("N"));
        var service = new ImportSeedService();

        ImportSeedResult result = service.Import(new ImportSeedOptions(
            ProjectRoot: _projectRoot,
            SeedDirectory: seedDir,
            OutputDirectory: outsideOutput,
            Force: false));

        Assert.False(result.Success);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "import.outputOutsideProject");
    }

    [Fact]
    public void Import_WhenSeedDirectoryIsSymlinkOutsideProject_ReturnsFailureDiagnostic()
    {
        string outsideSeedDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "bukit-import-seed-outside-" + Guid.NewGuid().ToString("N"))).FullName;
        string linkedSeedDir = Path.Combine(_projectRoot, "seed");
        Directory.CreateSymbolicLink(linkedSeedDir, outsideSeedDir);
        File.WriteAllText(Path.Combine(outsideSeedDir, "pages.json"), """
[
  {
    "title": "Outside Seed",
    "slug": "outside-seed",
    "content": "Outside seed content."
  }
]
""");
        var service = new ImportSeedService();

        try
        {
            ImportSeedResult result = service.Import(new ImportSeedOptions(
                ProjectRoot: _projectRoot,
                SeedDirectory: linkedSeedDir,
                OutputDirectory: Path.Combine(_projectRoot, "content"),
                Force: false));

            Assert.False(result.Success);
            Assert.Equal(2, result.ExitCode);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "import.seedDirInvalid");
        }
        finally
        {
            TestCleanup.DeleteDirectory(outsideSeedDir, recursive: true);
        }
    }

    [Fact]
    public void Import_WhenOutputDirectoryIsSymlinkOutsideProject_ReturnsFailureDiagnostic()
    {
        string seedDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "seed")).FullName;
        File.WriteAllText(Path.Combine(seedDir, "pages.json"), """
[
  {
    "title": "Inside Seed",
    "slug": "inside-seed",
    "content": "Inside seed content."
  }
]
""");
        string outsideOutput = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "bukit-import-output-outside-" + Guid.NewGuid().ToString("N"))).FullName;
        string linkedOutput = Path.Combine(_projectRoot, "content");
        Directory.CreateSymbolicLink(linkedOutput, outsideOutput);
        var service = new ImportSeedService();

        try
        {
            ImportSeedResult result = service.Import(new ImportSeedOptions(
                ProjectRoot: _projectRoot,
                SeedDirectory: seedDir,
                OutputDirectory: linkedOutput,
                Force: true));

            Assert.False(result.Success);
            Assert.Equal(2, result.ExitCode);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "import.outputOutsideProject");
            Assert.False(File.Exists(Path.Combine(outsideOutput, "pages", "inside-seed.md")));
        }
        finally
        {
            TestCleanup.DeleteDirectory(outsideOutput, recursive: true);
        }
    }

    [Fact]
    public void Import_WhenSeedDirectoryHasNoKnownRecords_ReturnsNoRecordsDiagnostic()
    {
        string seedDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "seed")).FullName;
        File.WriteAllText(Path.Combine(seedDir, "readme.txt"), "not a known seed file");
        var service = new ImportSeedService();

        ImportSeedResult result = service.Import(new ImportSeedOptions(
            ProjectRoot: _projectRoot,
            SeedDirectory: seedDir,
            OutputDirectory: Path.Combine(_projectRoot, "content"),
            Force: false));

        Assert.False(result.Success);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "import.seedNoRecords");
        Assert.Empty(result.Artifacts);
        Assert.False(Directory.Exists(Path.Combine(_projectRoot, "content")));
    }

    [Fact]
    public void Import_WhenOutputDirectoryExistsAndForceFalse_ReturnsFailureDiagnostic()
    {
        string seedDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "seed")).FullName;
        string outputDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "content")).FullName;
        File.WriteAllText(Path.Combine(outputDir, "existing.md"), "existing");
        var service = new ImportSeedService();

        ImportSeedResult result = service.Import(new ImportSeedOptions(
            ProjectRoot: _projectRoot,
            SeedDirectory: seedDir,
            OutputDirectory: outputDir,
            Force: false));

        Assert.False(result.Success);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "import.outputAlreadyExists");
    }

    [Fact]
    public void Import_WhenOutputDirectoryExistsAndForceTrue_WritesMarkdownAndArtifacts()
    {
        string seedDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "seed")).FullName;
        string outputDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "content")).FullName;
        File.WriteAllText(Path.Combine(outputDir, "existing.md"), "existing");
        File.WriteAllText(Path.Combine(seedDir, "posts.json"), """
[
  {
    "title": "Hello Seed",
    "slug": "hello-seed",
    "summary": "Imported summary",
    "content": "Imported body.",
    "language": "en",
    "published": false,
    "seo_title": "SEO title",
    "seo_description": "SEO description",
    "featured": true,
    "weight": 3
  }
]
""");
        var service = new ImportSeedService();

        ImportSeedResult result = service.Import(new ImportSeedOptions(
            ProjectRoot: _projectRoot,
            SeedDirectory: seedDir,
            OutputDirectory: outputDir,
            Force: true));

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Diagnostics);

        string markdownPath = Path.Combine(outputDir, "posts", "hello-seed.md");
        Assert.True(File.Exists(markdownPath));
        string markdown = File.ReadAllText(markdownPath);
        Assert.Contains("title: \"Hello Seed\"", markdown, StringComparison.Ordinal);
        Assert.Contains("slug: \"hello-seed\"", markdown, StringComparison.Ordinal);
        Assert.Contains("type: \"post\"", markdown, StringComparison.Ordinal);
        Assert.Contains("featured: true", markdown, StringComparison.Ordinal);
        Assert.Contains("weight: 3", markdown, StringComparison.Ordinal);
        Assert.Contains("Imported body.", markdown, StringComparison.Ordinal);

        ImportSeedArtifact artifact = Assert.Single(result.Artifacts);
        Assert.Equal("markdown", artifact.Type);
        Assert.Equal("content/posts/hello-seed.md", artifact.Path);
        Assert.DoesNotContain('\\', artifact.Path);
    }

    [Fact]
    public void Import_WhenSeedRecordInvalid_ReturnsStableDiagnostic()
    {
        string seedDir = Directory.CreateDirectory(Path.Combine(_projectRoot, "seed")).FullName;
        File.WriteAllText(Path.Combine(seedDir, "pages.json"), "{ not json");
        var service = new ImportSeedService();

        ImportSeedResult result = service.Import(new ImportSeedOptions(
            ProjectRoot: _projectRoot,
            SeedDirectory: seedDir,
            OutputDirectory: Path.Combine(_projectRoot, "content"),
            Force: false));

        Assert.False(result.Success);
        Assert.Equal(2, result.ExitCode);
        ImportSeedDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("import.seedRecordInvalid", diagnostic.Code);
        Assert.Equal("error", diagnostic.Severity);
        Assert.Equal("seed/pages.json", diagnostic.Path);
    }
}
