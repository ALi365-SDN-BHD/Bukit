using Bukit.Engine.Plugins.BuiltIn;
using Xunit;

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

        TaxonomyRedirectWriter.WriteRedirects(_tempDir, "tags", terms);

        var aliasPath = Path.Combine(_tempDir, "tags", "technology", "index.html");
        Assert.True(File.Exists(aliasPath));

        var content = File.ReadAllText(aliasPath);
        Assert.Contains("http-equiv=\"refresh\"", content, StringComparison.Ordinal);
        Assert.Contains("url=/tags/tech/", content, StringComparison.Ordinal);
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
    public void WriteRedirects_EmptyTerms_DoesNotThrow()
    {
        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase);

        TaxonomyRedirectWriter.WriteRedirects(_tempDir, "tags", terms);
    }
}
