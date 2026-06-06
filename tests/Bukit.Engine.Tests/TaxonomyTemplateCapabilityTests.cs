using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class TaxonomyTemplateCapabilityTests : IDisposable
{
    private readonly string _rootDir;
    private readonly string _layoutsDir;

    public TaxonomyTemplateCapabilityTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-taxonomy-capability-" + Guid.NewGuid().ToString("N"));
        _layoutsDir = Path.Combine(_rootDir, "layouts");
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "pages"));
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "page.html"), "{{ page.content }}");
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "taxonomy-index.html"), "{{ page.content }}");
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "taxonomy-term.html"), "{{ page.content }}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public void DerivePages_UsesTaxonomyTemplates_WhenCapabilityDeclared()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "bukit.templates.yaml"), """
                                                                        templates:
                                                                          pages/taxonomy-index.html:
                                                                            capabilities:
                                                                              supports_taxonomy: true
                                                                          pages/taxonomy-term.html:
                                                                            capabilities:
                                                                              supports_taxonomy: true
                                                                        """);

        var plugin = new TaxonomyPlugin();
        var derived = plugin.DerivePages(CreateContext());

        Assert.Contains(derived, x => x.Route.Url == "/tags/" && x.Route.Template == "pages/taxonomy-index.html");
        Assert.Contains(derived, x => x.Route.Url == "/tags/news/" && x.Route.Template == "pages/taxonomy-term.html");
    }

    private BuildContext CreateContext()
    {
        var item = new ContentItem(
            Id: "post-1",
            Title: "Post",
            Slug: "post",
            PublishAt: new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero),
            ContentHtml: "<p>Body</p>",
            Fields: new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", "post"),
                ["tags"] = new("test", new[] { "News" })
            });
        var route = new RouteInfo("/blog/post/", Path.Combine("blog", "post", "index.html"), "pages/post.html");
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags"] = new("legacy-test", new object[] { "News" })
        };
        var document = new ContentDocument(
            new ContentRecord(
                new ContentIdentity(item.Id, item.Slug, item.Id, "post", "published"),
                new ContentPresentation(item.Title, null, item.ContentHtml, "en", []),
                new ContentClassification("post", "post", [], ["News"]),
                new ContentOwnership(null, null, null, null),
                new ContentLifecycle(item.PublishAt, null, null, null),
                new ProvenanceRecord(null, null, [], [], null),
                new TrustMetadata(null, "published", []),
                [],
                [],
                []),
            new ContentBodyRef(item.ContentHtml, null, null, null),
            new ContentRoutePolicy(null, null, null, null, "post"),
            new ContentPublishPolicy(false, false, false, false, false, false, false),
            fields,
            []);

        return new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "test", Title = "test" },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = _rootDir,
            OutputDir = Path.Combine(_rootDir, "dist"),
            BaseUrl = "/",
            LayoutsDir = _layoutsDir,
            Routed = new List<(ContentItem Item, RouteInfo Route)>
            {
                (item, route)
            },
            RoutedDocuments = new List<(ContentDocument Document, RouteInfo Route)>
            {
                (document, route)
            },
            TemplateResolver = kind => kind.Trim().ToLowerInvariant() switch
            {
                "taxonomy_index" => "pages/taxonomy-index.html",
                "taxonomy_term" => "pages/taxonomy-term.html",
                _ => throw new ConfigException($"Unexpected template kind: {kind}")
            },
            Logger = new ConsoleLogger(LogLevel.Error)
        };
    }
}
