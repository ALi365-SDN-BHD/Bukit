using Bukit.Engine.Plugins.BuiltIn;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class TaxonomyMetadataLoaderTests
{
    [Fact]
    public void ParseSimpleFrontMatter_ValidYaml_ReturnsDictionary()
    {
        var input = "---\ndescription: A test description\nimage: /img/hero.png\nweight: 5\nparent: tech\n---\nBody content";
        var result = TaxonomyMetadataLoader.ParseSimpleFrontMatter(input);

        Assert.NotNull(result);
        Assert.Equal("A test description", result!["description"]);
        Assert.Equal("/img/hero.png", result["image"]);
        Assert.Equal("5", result["weight"]);
        Assert.Equal("tech", result["parent"]);
    }

    [Fact]
    public void ParseSimpleFrontMatter_NoFrontMatter_ReturnsNull()
    {
        var result = TaxonomyMetadataLoader.ParseSimpleFrontMatter("Just body content");

        Assert.Null(result);
    }

    [Fact]
    public void ParseSimpleFrontMatter_UnclosedFrontMatter_ReturnsNull()
    {
        var input = "---\ndescription: test\n";

        var result = TaxonomyMetadataLoader.ParseSimpleFrontMatter(input);

        Assert.Null(result);
    }

    [Fact]
    public void ParseSimpleFrontMatter_EmptyFrontMatter_ReturnsNull()
    {
        var input = "---\n---\nBody";

        var result = TaxonomyMetadataLoader.ParseSimpleFrontMatter(input);

        Assert.Null(result);
    }

    [Fact]
    public void LoadFromEnsureTerms_EnrichesExistingTerms()
    {
        var data = new Dictionary<string, object>();
        var ensure = new Dictionary<string, List<Dictionary<string, object>>>
        {
            ["tags"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["slug"] = "tech",
                    ["description"] = "Technology posts",
                    ["image"] = "/img/tech.png",
                    ["weight"] = 10,
                    ["parent"] = "root"
                }
            }
        };
        data["taxonomy_ensure_terms"] = ensure;

        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase)
        {
            ["tech"] = new TaxonomyTerm("Tech", "tech")
        };

        TaxonomyMetadataLoader.LoadFromEnsureTerms(data, "tags", terms);

        Assert.Equal("Technology posts", terms["tech"].Description);
        Assert.Equal("/img/tech.png", terms["tech"].Image);
        Assert.Equal(10, terms["tech"].Weight);
        Assert.Equal("root", terms["tech"].ParentSlug);
    }

    [Fact]
    public void LoadFromEnsureTerms_DoesNotOverwriteExistingMetadata()
    {
        var data = new Dictionary<string, object>();
        var ensure = new Dictionary<string, List<Dictionary<string, object>>>
        {
            ["tags"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["slug"] = "tech",
                    ["description"] = "New description"
                }
            }
        };
        data["taxonomy_ensure_terms"] = ensure;

        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase)
        {
            ["tech"] = new TaxonomyTerm("Tech", "tech")
            {
                Description = "Existing description",
                Weight = 99
            }
        };

        TaxonomyMetadataLoader.LoadFromEnsureTerms(data, "tags", terms);

        Assert.Equal("Existing description", terms["tech"].Description);
        Assert.Equal(99, terms["tech"].Weight);
    }

    [Fact]
    public void LoadFromEnsureTerms_MissingKind_DoesNotThrow()
    {
        var data = new Dictionary<string, object>();
        var ensure = new Dictionary<string, List<Dictionary<string, object>>>
        {
            ["tags"] = new List<Dictionary<string, object>>()
        };
        data["taxonomy_ensure_terms"] = ensure;

        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase)
        {
            ["tech"] = new TaxonomyTerm("Tech", "tech")
        };

        TaxonomyMetadataLoader.LoadFromEnsureTerms(data, "categories", terms);

        Assert.Null(terms["tech"].Description);
    }
}
