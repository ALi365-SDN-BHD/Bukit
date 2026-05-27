using System.Text;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Plugins.BuiltIn;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class TaxonomyHierarchyBuilderTests
{
    [Fact]
    public void BuildHierarchy_FlatTerms_ReturnsEmptyChildrenAndAncestors()
    {
        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase)
        {
            ["tech"] = new TaxonomyTerm("Tech", "tech"),
            ["life"] = new TaxonomyTerm("Life", "life")
        };

        var result = TaxonomyHierarchyBuilder.BuildHierarchy(terms);

        Assert.Equal(2, result.Count);
        Assert.Empty(result["tech"].Children);
        Assert.Empty(result["tech"].Ancestors);
        Assert.Empty(result["life"].Children);
        Assert.Empty(result["life"].Ancestors);
    }

    [Fact]
    public void BuildHierarchy_WithParentSlug_BuildsChildrenAndAncestors()
    {
        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase)
        {
            ["tech"] = new TaxonomyTerm("Tech", "tech"),
            ["frontend"] = new TaxonomyTerm("Frontend", "frontend")
            {
                ParentSlug = "tech"
            },
            ["react"] = new TaxonomyTerm("React", "react")
            {
                ParentSlug = "frontend"
            }
        };

        var result = TaxonomyHierarchyBuilder.BuildHierarchy(terms);

        Assert.Single(result["tech"].Children);
        Assert.Contains("frontend", result["tech"].Children);
        Assert.Empty(result["tech"].Ancestors);

        Assert.Single(result["frontend"].Children);
        Assert.Contains("react", result["frontend"].Children);
        Assert.Single(result["frontend"].Ancestors);
        Assert.Contains("tech", result["frontend"].Ancestors);

        Assert.Empty(result["react"].Children);
        Assert.Equal(2, result["react"].Ancestors.Count);
        Assert.Equal("tech", result["react"].Ancestors[0]);
        Assert.Equal("frontend", result["react"].Ancestors[1]);
    }

    [Fact]
    public void BuildHierarchy_OrphanParentSlug_IgnoresMissingParent()
    {
        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.OrdinalIgnoreCase)
        {
            ["frontend"] = new TaxonomyTerm("Frontend", "frontend")
            {
                ParentSlug = "nonexistent"
            }
        };

        var result = TaxonomyHierarchyBuilder.BuildHierarchy(terms);

        Assert.Empty(result["frontend"].Ancestors);
    }
}
