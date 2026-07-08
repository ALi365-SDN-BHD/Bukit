using System.Text.Json;
using Bukit.Engine.Plugins.BuiltIn;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class TaxonomyDataWriterTests
{
    [Fact]
    public void BuildKindData_WithRoutePrefix_UsesConfiguredTaxonomyUrls()
    {
        var terms = CreateTerms();

        var data = TaxonomyDataWriter.BuildKindData(
            key: "categories",
            kind: "category",
            title: "Categories",
            terms,
            routePrefix: "/insights/category");

        Assert.Equal("/insights/category", data["route_prefix"]);
        Assert.Equal("/insights/category", data["routePrefix"]);
        Assert.Equal("/insights/category/", data["url"]);
        var termsValue = Assert.IsType<List<object>>(data["terms"]);
        var term = Assert.IsType<Dictionary<string, object>>(termsValue[0]);
        Assert.Equal("/insights/category/market/", term["url"]);
    }

    [Fact]
    public void WriteKind_WithRoutePrefix_WritesConfiguredTaxonomyUrls()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            TaxonomyDataWriter.WriteKind(
                writer,
                baseUrl: "/docs",
                key: "categories",
                kind: "category",
                title: "Categories",
                CreateTerms(),
                routePrefix: "/insights/category");
        }

        using var doc = JsonDocument.Parse(stream.ToArray());
        Assert.Equal("/insights/category", doc.RootElement.GetProperty("route_prefix").GetString());
        Assert.Equal("/docs/insights/category/", doc.RootElement.GetProperty("url").GetString());
        var term = doc.RootElement.GetProperty("terms")[0];
        Assert.Equal("/docs/insights/category/market/", term.GetProperty("url").GetString());
    }

    private static Dictionary<string, TaxonomyTerm> CreateTerms()
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["market"] = new TaxonomyTerm("Market", "market")
        };
}
