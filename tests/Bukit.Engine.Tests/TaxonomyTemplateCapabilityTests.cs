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

        var (context, config) = CreateContext();
        var plugin = new TaxonomyPlugin(config);
        var derived = plugin.DerivePages(context);

        Assert.Contains(derived, x => x.Route.Url == "/tags/" && x.Route.Template == "pages/taxonomy-index.html");
        Assert.Contains(derived, x => x.Route.Url == "/tags/news/" && x.Route.Template == "pages/taxonomy-term.html");
    }

    private (BuildContext Context, AppConfig Config) CreateContext()
    {
        var item = ContentDocument.Create(
            id: "post-1",
            title: "Post",
            slug: "post",
            publishAt: new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero),
            contentHtml: "<p>Body</p>",
            fields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "post",
                ["tags"] = new[] { "News" }
            }));

        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "test", Title = "test" },
            Content = TestContent.Markdown()
        };
        var context = new BuildContext
        {
            RootDir = _rootDir,
            OutputDir = Path.Combine(_rootDir, "dist"),
            BaseUrl = "/",
            LayoutsDir = _layoutsDir,
            RoutedDocuments = new List<(ContentDocument Item, RouteInfo Route)>
            {
                (item, new RouteInfo("/blog/post/", Path.Combine("blog", "post", "index.html"), "pages/post.html"))
            }.ToRoutedDocuments(),
            TemplateResolver = kind => kind.Trim().ToLowerInvariant() switch
            {
                "taxonomy_index" => "pages/taxonomy-index.html",
                "taxonomy_term" => "pages/taxonomy-term.html",
                _ => throw new ConfigException($"Unexpected template kind: {kind}")
            },
            Logger = new ConsoleLogger(LogLevel.Error)
        };
        return (context, config);
    }
}
