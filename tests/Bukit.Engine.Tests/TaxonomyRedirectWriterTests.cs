using System.Text;
using Bukit.Engine.Output;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Shared;
using Xunit;
using Xunit.Sdk;

namespace Bukit.Engine.Tests;

public sealed class TaxonomyRedirectWriterTests : IDisposable
{
    private readonly string _tempDir;

    public TaxonomyRedirectWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"bukit-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void WriteRedirects_WithAliases_GeneratesRedirectPages()
    {
        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase)
        {
            ["tech"] = new TaxonomyTerm("Tech", "tech")
            {
                Aliases = new[] { "technology", "it" }
            }
        };

        var generatedPaths = new List<string> { "existing.txt" };
        TaxonomyRedirectWriter.WriteRedirects(_tempDir, "tags", terms, generatedPaths: generatedPaths);

        var aliasPath = Path.Combine(_tempDir, "tags", "technology", "index.html");
        Assert.True(File.Exists(aliasPath));

        var content = File.ReadAllText(aliasPath);
        Assert.Contains("http-equiv=\"refresh\"", content, StringComparison.Ordinal);
        Assert.Contains("url=/tags/tech/", content, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_tempDir, "tags", "it", "index.html")));
        Assert.Equal(["existing.txt", "tags/technology/index.html", "tags/it/index.html"], generatedPaths);
        Assert.Equal(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(content)), File.ReadAllBytes(aliasPath));
    }

    [Fact]
    public void WriteRedirects_NoAliases_DoesNotCreateFiles()
    {
        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase)
        {
            ["tech"] = new TaxonomyTerm("Tech", "tech")
        };

        TaxonomyRedirectWriter.WriteRedirects(_tempDir, "tags", terms);

        Assert.False(Directory.Exists(Path.Combine(_tempDir, "tags", "tech")));
    }

    [Fact]
    public void WriteRedirects_AliasEqualsSlug_SkipSelfAlias()
    {
        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase)
        {
            ["tech"] = new TaxonomyTerm("Tech", "tech")
            {
                Aliases = new[] { "tech", "technology" }
            }
        };

        TaxonomyRedirectWriter.WriteRedirects(_tempDir, "tags", terms);

        Assert.True(File.Exists(Path.Combine(_tempDir, "tags", "technology", "index.html")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "tags", "tech", "index.html")));
    }

    [Fact]
    public void WriteRedirects_WithRoutePrefix_WritesAliasUnderConfiguredTaxonomyRoute()
    {
        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase)
        {
            ["market"] = new TaxonomyTerm("Market", "market")
            {
                Aliases = new[] { "market-watch" }
            }
        };

        TaxonomyRedirectWriter.WriteRedirects(_tempDir, "category", terms, "/insights/category");

        var aliasPath = Path.Combine(_tempDir, "insights", "category", "market-watch", "index.html");
        Assert.True(File.Exists(aliasPath));
        Assert.False(File.Exists(Path.Combine(_tempDir, "category", "market-watch", "index.html")));

        var content = File.ReadAllText(aliasPath);
        Assert.Contains("url=/insights/category/market/", content, StringComparison.Ordinal);
        Assert.Contains("href=\"/insights/category/market/\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteRedirects_EmptyTerms_DoesNotThrow()
    {
        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase);

        TaxonomyRedirectWriter.WriteRedirects(_tempDir, "tags", terms);
    }

    [Theory]
    [Trait("Defect", "KD-004")]
    [InlineData("../../escape")]
    [InlineData("/absolute-escape")]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("valid/../../escape")]
    [InlineData("  valid/../escape  ")]
    [InlineData(@"..\..\escape")]
    [InlineData(@"\rooted")]
    [InlineData(@"C:\escape")]
    [InlineData("C:/escape")]
    [InlineData("C:escape")]
    [InlineData(@"\\server\share")]
    [InlineData("bad\0alias")]
    public void WriteRedirects_AliasTraversal_ThrowsAndWritesNothing(string alias)
    {
        var outputDir = Path.Combine(_tempDir, "output");
        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase)
        {
            ["tech"] = new TaxonomyTerm("Tech", "tech")
            {
                Aliases = new[] { alias }
            }
        };

        var failure = Record.Exception(() =>
            TaxonomyRedirectWriter.WriteRedirects(outputDir, "tags", terms));

        Assert.Empty(Directory.EnumerateFiles(_tempDir, "index.html", SearchOption.AllDirectories));
        Assert.IsType<ConfigException>(failure);
        Assert.Equal(DiagnosticCode.BuildOutputUnsafe, ((ConfigException)failure).Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteRedirects_LateUnsafeAlias_PreservesAllFilesAndGeneratedPaths(bool separateTerm)
    {
        var outputDir = Path.Combine(_tempDir, "output");
        var existingPath = Path.Combine(outputDir, "tags", "technology", "index.html");
        Directory.CreateDirectory(Path.GetDirectoryName(existingPath)!);
        byte[] original = [0, 255, 128, 10];
        File.WriteAllBytes(existingPath, original);
        var before = Directory.GetFileSystemEntries(_tempDir, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        var terms = new Dictionary<string, TaxonomyTerm>
        {
            ["tech"] = new("Tech", "tech")
            {
                Aliases = separateTerm ? ["technology", "new-alias"] : ["technology", "new-alias", "../../escape"]
            }
        };
        if (separateTerm)
        {
            terms["later"] = new("Later", "later") { Aliases = ["../../escape"] };
        }
        var generatedPaths = new List<string> { "existing.txt" };

        var failure = Record.Exception(() => TaxonomyRedirectWriter.WriteRedirects(
            outputDir, "tags", terms, generatedPaths: generatedPaths));

        Assert.Equal(before, Directory.GetFileSystemEntries(_tempDir, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal).ToArray());
        Assert.Equal(original, File.ReadAllBytes(existingPath));
        Assert.Equal(["existing.txt"], generatedPaths);
        Assert.Equal(DiagnosticCode.BuildOutputUnsafe, Assert.IsType<ConfigException>(failure).Code);
    }

    [Theory]
    [InlineData("  old/topic  ", "old/topic")]
    [InlineData("新闻/技术", "新闻/技术")]
    public void WriteRedirects_SafeHierarchyAndBlankAliases_PreserveNormalOutput(string alias, string relative)
    {
        var terms = new Dictionary<string, TaxonomyTerm>
        {
            ["tech"] = new("Tech", "tech") { Aliases = ["", "  ", " TECH ", alias] }
        };
        var generatedPaths = new List<string>();

        TaxonomyRedirectWriter.WriteRedirects(_tempDir, "tags", terms, "/topics", generatedPaths);

        var path = Path.Combine(_tempDir, "topics", relative, "index.html");
        Assert.Contains("url=/topics/tech/", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.Equal([$"topics/{relative}/index.html"], generatedPaths);
        Assert.Single(Directory.EnumerateFiles(_tempDir, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void WriteRedirects_UnsafeSelfAlias_IsRejected()
    {
        var terms = new Dictionary<string, TaxonomyTerm>
        {
            ["unsafe"] = new("Unsafe", "..") { Aliases = [".."] }
        };

        var failure = Record.Exception(() => TaxonomyRedirectWriter.WriteRedirects(_tempDir, "tags", terms));

        Assert.Empty(Directory.EnumerateFileSystemEntries(_tempDir));
        Assert.Equal(DiagnosticCode.BuildOutputUnsafe, Assert.IsType<ConfigException>(failure).Code);
    }

    [Fact]
    public void WriteRedirects_LateInvalidSelfAlias_PreservesExistingOutput()
    {
        var existingPath = Path.Combine(_tempDir, "tags", "technology", "index.html");
        Directory.CreateDirectory(Path.GetDirectoryName(existingPath)!);
        byte[] original = [0, 255, 128, 10];
        File.WriteAllBytes(existingPath, original);
        var before = Directory.GetFileSystemEntries(_tempDir, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();
        var terms = new Dictionary<string, TaxonomyTerm>
        {
            ["tech"] = new("Tech", "tech") { Aliases = ["technology"] },
            ["later"] = new("Later", "bad\0alias") { Aliases = ["bad\0alias"] }
        };
        var generatedPaths = new List<string> { "existing.txt" };

        var failure = Record.Exception(() => TaxonomyRedirectWriter.WriteRedirects(
            _tempDir, "tags", terms, generatedPaths: generatedPaths));

        Assert.Equal(original, File.ReadAllBytes(existingPath));
        Assert.Equal(before, Directory.GetFileSystemEntries(_tempDir, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal).ToArray());
        Assert.Equal(["existing.txt"], generatedPaths);
        var configError = Assert.IsType<ConfigException>(failure);
        Assert.Equal(DiagnosticCode.BuildOutputUnsafe, configError.Code);
        Assert.IsAssignableFrom<ArgumentException>(configError.InnerException);
    }

    [Fact]
    public void WriteRedirects_EscapingRoutePrefix_IsRejectedByExistingPathPolicy()
    {
        var outputDir = Path.Combine(_tempDir, "project", "output");
        Directory.CreateDirectory(outputDir);
        var terms = new Dictionary<string, TaxonomyTerm>
        {
            ["tech"] = new("Tech", "tech") { Aliases = ["alias"] }
        };
        var generatedPaths = new List<string>();

        var failure = Record.Exception(() => TaxonomyRedirectWriter.WriteRedirects(
            outputDir, "tags", terms, "/../../escape", generatedPaths));

        Assert.Empty(Directory.EnumerateFiles(_tempDir, "*", SearchOption.AllDirectories));
        var configError = Assert.IsType<ConfigException>(failure);
        Assert.Equal(DiagnosticCode.BuildOutputUnsafe, configError.Code);
        Assert.IsType<OutputPathSecurityException>(configError.InnerException);
        Assert.Empty(generatedPaths);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteRedirects_ExistingEscapingSymlink_PreservesExternalBytes(bool fileLink)
    {
        var outputDir = Path.Combine(_tempDir, "output");
        var externalDir = Path.Combine(_tempDir, "external");
        var aliasDir = Path.Combine(outputDir, "tags", "alias");
        Directory.CreateDirectory(externalDir);
        Directory.CreateDirectory(Path.GetDirectoryName(aliasDir)!);
        var externalPath = Path.Combine(externalDir, "index.html");
        byte[] original = [0, 255, 128, 10];
        File.WriteAllBytes(externalPath, original);
        try
        {
            if (fileLink)
            {
                Directory.CreateDirectory(aliasDir);
                File.CreateSymbolicLink(Path.Combine(aliasDir, "index.html"), externalPath);
            }
            else
            {
                Directory.CreateSymbolicLink(aliasDir, externalDir);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            throw SkipException.ForSkip($"Symlinks are unavailable: {ex.GetType().Name}");
        }
        var terms = new Dictionary<string, TaxonomyTerm>
        {
            ["tech"] = new("Tech", "tech") { Aliases = ["alias"] }
        };
        var generatedPaths = new List<string> { "existing.txt" };

        var failure = Record.Exception(() => TaxonomyRedirectWriter.WriteRedirects(
            outputDir, "tags", terms, generatedPaths: generatedPaths));

        Assert.Equal(original, File.ReadAllBytes(externalPath));
        Assert.Equal(["existing.txt"], generatedPaths);
        var configError = Assert.IsType<ConfigException>(failure);
        Assert.Equal(DiagnosticCode.BuildOutputUnsafe, configError.Code);
        Assert.IsType<OutputPathSecurityException>(configError.InnerException);
    }
}
