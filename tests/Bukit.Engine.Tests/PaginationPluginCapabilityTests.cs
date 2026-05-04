using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class PaginationPluginCapabilityTests : IDisposable
{
    private readonly string _rootDir;
    private readonly string _layoutsDir;

    public PaginationPluginCapabilityTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-pagination-capability-" + Guid.NewGuid().ToString("N"));
        _layoutsDir = Path.Combine(_rootDir, "layouts");
        Directory.CreateDirectory(Path.Combine(_layoutsDir, "pages"));
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "page.html"), "{{ page.content }}");
        File.WriteAllText(Path.Combine(_layoutsDir, "pages", "pagination.html"), "{{ page.content }}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public void DerivePages_UsesPaginationTemplate_WhenCapabilityDeclared()
    {
        File.WriteAllText(Path.Combine(_layoutsDir, "bukit.templates.yaml"), """
                                                                        templates:
                                                                          pages/pagination.html:
                                                                            capabilities:
                                                                              supports_pagination: true
                                                                        """);

        var plugin = new PaginationPlugin();
        var derived = plugin.DerivePages(CreateContext());

        Assert.NotEmpty(derived);
        Assert.All(derived, x => Assert.Equal("pages/pagination.html", x.Route.Template));
    }

    private BuildContext CreateContext()
    {
        var routed = new List<(ContentItem Item, RouteInfo Route)>();
        for (var i = 1; i <= 12; i++)
        {
            routed.Add((
                new ContentItem(
                    Id: $"post-{i}",
                    Title: $"Post {i}",
                    Slug: $"post-{i}",
                    PublishAt: new DateTimeOffset(2024, 01, i, 0, 0, 0, TimeSpan.Zero),
                    ContentHtml: $"<p>{i}</p>",
                    Meta: new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["type"] = "post"
                    },
                    Fields: null),
                new RouteInfo($"/blog/post-{i}/", Path.Combine("blog", $"post-{i}", "index.html"), "pages/post.html")));
        }

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
            Routed = routed,
            Logger = new ConsoleLogger(LogLevel.Error)
        };
    }
}
