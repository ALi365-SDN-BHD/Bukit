using System.Text;
using Bukit.Cli.Commands;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class DoctorTemplateAnalyzerTests : IDisposable
{
    private readonly string _layoutsDir;

    public DoctorTemplateAnalyzerTests()
    {
        _layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-template-analyzer-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_layoutsDir);
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "pages"));
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "partials"));
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_layoutsDir, recursive: true);
    }

    [Fact]
    public void CollectExplicitConfiguredTemplates_ReturnsDistinctSortedTemplates()
    {
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["post"] = new()
                    {
                        Permalink = "/blog/{slug}/",
                        Template = "pages/post.html",
                        ListTemplate = "pages/list.html",
                        FilteredLists =
                        [
                            new FilteredListConfig
                            {
                                Field = "tag",
                                Value = "news",
                                ListRoute = "/tags/news/",
                                ListTemplate = "pages/tag.html"
                            }
                        ]
                    }
                }
            },
            Content = new ContentConfig(),
            Theme = new ThemeConfig
            {
                StaticTemplate = "pages/static.html"
            },
            Taxonomy = new TaxonomyConfig
            {
                Kinds =
                [
                    new TaxonomyKindConfig
                    {
                        Key = "tag",
                        Template = "taxonomy/item.html",
                        IndexTemplate = "taxonomy/index.html",
                        TermTemplate = "taxonomy/term.html"
                    }
                ]
            }
        };

        var templates = DoctorTemplateAnalyzer.CollectExplicitConfiguredTemplates(config);

        Assert.Equal(
        [
            "pages/list.html",
            "pages/post.html",
            "pages/static.html",
            "pages/tag.html",
            "taxonomy/index.html",
            "taxonomy/item.html",
            "taxonomy/term.html"
        ], templates);
    }

    [Fact]
    public void CollectMissingUsedTemplates_ReturnsOnlyMissingNormalizedTemplates()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "present.html"), "<html></html>");
        var routed = new[]
        {
            new RoutedContentDocument(
                ContentDocument.Create("id", "title", "slug", DateTimeOffset.UtcNow, null),
                new RouteInfo("/present/", "present/index.html", @"pages\present.html"))
        };
        var listRoutes = new[]
        {
            new RouteInfo("/missing/", "missing/index.html", "pages/missing.html")
        };

        var missing = DoctorTemplateAnalyzer.CollectMissingUsedTemplates(
            _layoutsDir,
            routed,
            listRoutes,
            ["partials/missing.html", "pages/missing.html"]);

        Assert.Equal(["pages/missing.html", "partials/missing.html"], missing);
    }

    [Fact]
    public void AnalyzeTemplateChains_PrintsLayoutAndIncludeReferences()
    {
        var home = Path.Combine(_layoutsDir, "pages", "home.html");
        File.WriteAllText(home, """
            {% layout "layouts/base.html" %}
            {{ include "partials/card.html" }}
            """);

        var output = CaptureStdOut(() => DoctorTemplateAnalyzer.AnalyzeTemplateChains(_layoutsDir, [home]));

        Assert.Contains("pages/home.html", output, StringComparison.Ordinal);
        Assert.Contains("layout", output, StringComparison.Ordinal);
        Assert.Contains("include", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractDirectives_SupportsAllQuotedForms()
    {
        var text = """
            {% include "partials/a.html" %}
            {% include 'partials/b.html' %}
            {{ include "partials/c.html" }}
            {{ include 'partials/d.html' }}
            """;

        var includes = DoctorTemplateAnalyzer.ExtractDirectives(text, "include");

        Assert.Equal(["partials/a.html", "partials/b.html", "partials/c.html", "partials/d.html"], includes);
    }

    [Fact]
    public void CountOpenings_CountsOccurrences()
    {
        Assert.Equal(3, DoctorTemplateAnalyzer.CountOpenings("{{ a }} {{ b }} {{ c }}", "{{"));
    }

    [Fact]
    public void CheckTemplateVariables_ListAndTaxonomyPageTitle_ReturnsKnownContextSuccess()
    {
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "taxonomy"));
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "list.html"), "{{ page.title }}");
        File.WriteAllText(Path.Combine(_layoutsDir, "taxonomy", "term.html"), "{{ page.title }}");

        var output = CaptureStdOut(() => DoctorTemplateAnalyzer.CheckTemplateVariables(_layoutsDir));

        Assert.Contains("No invalid known-context template variables detected", output, StringComparison.Ordinal);
        Assert.DoesNotContain("this.title", output, StringComparison.Ordinal);
        Assert.DoesNotContain("term.title", output, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckTemplateVariables_ThisTitle_ReportsKnownContextWarning()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "list.html"), "{{ this.title }}");

        var output = CaptureStdOut(() => DoctorTemplateAnalyzer.CheckTemplateVariables(_layoutsDir));

        Assert.Contains("this.title", output, StringComparison.Ordinal);
        Assert.Contains("current template context", output, StringComparison.Ordinal);
    }

    private static string CaptureStdOut(Action action)
    {
        using var writer = new StringWriter(new StringBuilder());
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(writer);
            action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
