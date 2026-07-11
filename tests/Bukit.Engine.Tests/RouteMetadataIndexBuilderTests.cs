using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.RouteMetadata;
using Bukit.Rendering;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class RouteMetadataIndexBuilderTests
{
    [Fact]
    public void Build_UsesConfiguredAliasesNormalizesRoutesAndAllowsBlankSeoFields()
    {
        var config = CreateConfig(requiredRoutes: ["/", "/insights/"]) with
        {
            RouteField = "route_path",
            TitleField = "display_title",
            SummaryField = "page_summary",
            SeoTitleField = "seo_title",
            SeoDescriptionField = "seo_description"
        };
        var data = CreateSourceData(
            Row("home", new()
            {
                ["routePath"] = "/",
                ["displayTitle"] = "Home",
                ["pageSummary"] = "Home summary",
                ["seoTitle"] = "",
                ["seoDescription"] = " "
            }),
            Row("insights", new()
            {
                ["route_path"] = "insights",
                ["display_title"] = "Insights",
                ["page_summary"] = "Insights summary",
                ["seo_title"] = "Insights SEO",
                ["seo_description"] = "Insights SEO description"
            }));

        var result = RouteMetadataIndexBuilder.Build(config, data);

        Assert.Equal(["/", "/insights/"], result.Keys);
        Assert.Equal(new RouteMetadataEntry("/", "Home", "Home summary", null, null), result["/"]);
        Assert.Equal("Insights SEO", result["/insights/"].SeoTitle);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("//insights/")]
    [InlineData("/insights//archive/")]
    [InlineData("/insights/?page=2")]
    [InlineData("/insights/#latest")]
    [InlineData("/insights\\archive/")]
    public void Build_InvalidRouteSyntax_ThrowsWithSourceRowAndRouteValue(string route)
    {
        var ex = Assert.Throws<ContentException>(() => RouteMetadataIndexBuilder.Build(
            CreateConfig(),
            CreateSourceData(Row("invalid-route", Fields(route, "Invalid", "Invalid summary")))));

        Assert.Contains("page_meta", ex.Message, StringComparison.Ordinal);
        Assert.Contains("invalid-route", ex.Message, StringComparison.Ordinal);
        Assert.Contains(route, ex.Message, StringComparison.Ordinal);
        Assert.Contains("route", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_RootRoute_RemainsValid()
    {
        var result = RouteMetadataIndexBuilder.Build(
            CreateConfig(requiredRoutes: ["/"]),
            CreateSourceData(Row("home", Fields("/", "Home", "Home summary"))));

        Assert.Equal("/", result["/"].Route);
    }

    [Fact]
    public void Build_MissingCustomTitleField_ThrowsWithoutCanonicalFallback()
    {
        var fields = Fields("/about/", "Ignored", "About summary");
        fields.Remove("title");
        var config = CreateConfig() with { TitleField = "display_title" };

        var ex = Assert.Throws<ContentException>(() => RouteMetadataIndexBuilder.Build(
            config,
            CreateSourceData(Row("about", fields, canonicalTitle: "Canonical About"))));

        AssertDiagnostic(ex, "page_meta", "about", "/about/");
        Assert.Contains("display_title", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_MissingDefaultTitleField_UsesCanonicalTitle()
    {
        var fields = Fields("/about/", "Ignored", "About summary");
        fields.Remove("title");

        var result = RouteMetadataIndexBuilder.Build(
            CreateConfig(),
            CreateSourceData(Row("about", fields, canonicalTitle: "Canonical About")));

        Assert.Equal("Canonical About", result["/about/"].Title);
    }

    [Fact]
    public void Build_DuplicateNormalizedRoute_ThrowsWithSourceRowAndRoute()
    {
        var data = CreateSourceData(
            Row("first", Fields("insights", "First", "First summary")),
            Row("second", Fields("/insights/", "Second", "Second summary")));

        var ex = Assert.Throws<ContentException>(() => RouteMetadataIndexBuilder.Build(CreateConfig(), data));

        AssertDiagnostic(ex, "page_meta", "second", "/insights/");
        Assert.Contains("duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("title")]
    [InlineData("summary")]
    public void Build_EmptyRequiredText_ThrowsWithSourceRowAndRoute(string field)
    {
        var fields = Fields("/about/", "About", "About summary");
        fields[field] = " ";

        var ex = Assert.Throws<ContentException>(() =>
            RouteMetadataIndexBuilder.Build(CreateConfig(), CreateSourceData(Row("about", fields))));

        AssertDiagnostic(ex, "page_meta", "about", "/about/");
        Assert.Contains(field, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_MissingRequiredRoute_ThrowsWithSourceAndRoute()
    {
        var config = CreateConfig(requiredRoutes: ["/", "/companies/"]);

        var ex = Assert.Throws<ContentException>(() => RouteMetadataIndexBuilder.Build(
            config,
            CreateSourceData(Row("home", Fields("/", "Home", "Home summary")))));

        Assert.Contains("page_meta", ex.Message, StringComparison.Ordinal);
        Assert.Contains("/companies/", ex.Message, StringComparison.Ordinal);
        Assert.Contains("required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_InvalidRequiredRoute_UsesSameRouteSyntaxRule()
    {
        var config = CreateConfig(requiredRoutes: ["/insights/?page=2"]);

        var ex = Assert.Throws<ContentException>(() => RouteMetadataIndexBuilder.Build(
            config,
            CreateSourceData(Row("home", Fields("/", "Home", "Home summary")))));

        Assert.Contains("page_meta", ex.Message, StringComparison.Ordinal);
        Assert.Contains("/insights/?page=2", ex.Message, StringComparison.Ordinal);
        Assert.Contains("invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_NonObjectRow_ThrowsWithSourceAndRowIndex()
    {
        var data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["page_meta"] = new object[] { "not-an-object" }
        };

        var ex = Assert.Throws<ContentException>(() => RouteMetadataIndexBuilder.Build(CreateConfig(), data));

        Assert.Contains("page_meta", ex.Message, StringComparison.Ordinal);
        Assert.Contains("row 0", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("object", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_UnavailableSource_ThrowsWithSourceContext()
    {
        var ex = Assert.Throws<ContentException>(() => RouteMetadataIndexBuilder.Build(
            CreateConfig(),
            new Dictionary<string, object>()));

        Assert.Contains("page_meta", ex.Message, StringComparison.Ordinal);
        Assert.Contains("unavailable", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_ReturnsReadOnlyIndex()
    {
        var result = RouteMetadataIndexBuilder.Build(
            CreateConfig(),
            CreateSourceData(Row("home", Fields("/", "Home", "Home summary"))));

        var mutable = Assert.IsAssignableFrom<IDictionary<string, RouteMetadataEntry>>(result);
        Assert.Throws<NotSupportedException>(() => mutable.Add(
            "/about/", new RouteMetadataEntry("/about/", "About", "About summary", null, null)));
    }

    [Fact]
    public void Build_AmbiguousConfiguredAliases_ThrowsWithSourceRowAndRoute()
    {
        var fields = Fields("/about/", "About", "About summary");
        fields.Remove("summary");
        fields["pageSummary"] = "First";
        fields["page_summary"] = "Second";
        var config = CreateConfig() with { SummaryField = "page__summary" };

        var ex = Assert.Throws<ContentException>(() => RouteMetadataIndexBuilder.Build(
            config,
            CreateSourceData(Row("about", fields))));

        AssertDiagnostic(ex, "page_meta", "about", "/about/");
        Assert.Contains("ambiguous", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static RouteMetadataConfig CreateConfig(IReadOnlyList<string>? requiredRoutes = null) => new()
    {
        Source = "page_meta",
        RequiredRoutes = requiredRoutes ?? Array.Empty<string>()
    };

    private static IReadOnlyDictionary<string, object> CreateSourceData(params object[] rows) =>
        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["page_meta"] = rows };

    private static ModuleInfo Row(
        string id,
        Dictionary<string, object> fields,
        string? canonicalTitle = null) => new()
    {
        Id = id,
        Slug = id,
        Title = canonicalTitle ?? id,
        Content = string.Empty,
        Fields = ContentFieldReader.ToFieldMap(fields)
    };

    private static Dictionary<string, object> Fields(string route, string title, string summary) => new()
    {
        ["route"] = route,
        ["title"] = title,
        ["summary"] = summary
    };

    private static void AssertDiagnostic(ContentException exception, string source, string row, string route)
    {
        Assert.Contains(source, exception.Message, StringComparison.Ordinal);
        Assert.Contains(row, exception.Message, StringComparison.Ordinal);
        Assert.Contains(route, exception.Message, StringComparison.Ordinal);
    }
}
