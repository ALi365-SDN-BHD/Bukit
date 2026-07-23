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

public sealed class AliasPluginTests
{
    private static ContentDocument Item(string id, string title, string slug, Dictionary<string, object>? fieldValues = null)
    {
        fieldValues ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        return ContentDocument.Create(id, title, slug, DateTimeOffset.UtcNow, "<p>x</p>", ContentFieldReader.ToFieldMap(fieldValues));
    }

    private static RouteInfo Route(string url) => new(url, $"out{url}index.html", "pages/post.html");

    [Fact]
    public void DerivePages_AliasesAsList_GeneratesRedirectPages()
    {
        var fieldValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["aliases"] = new[] { "/old-url/", "/another-old/" }
        };
        var (ctx, config) = CreateContext(new List<(ContentDocument, RouteInfo)>
        {
            (Item("p1", "Post", "post", fieldValues), Route("/post/"))
        });

        var derived = new AliasPlugin(config).DerivePages(ctx);

        Assert.Equal(2, derived.Count);
        Assert.Contains(derived, x => x.Route.Url == "/old-url/");
        Assert.Contains(derived, x => x.Route.Url == "/another-old/");
    }

    [Fact]
    public void DerivePages_AliasAsString_GeneratesRedirectPage()
    {
        var fieldValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["aliases"] = "/legacy/"
        };
        var (ctx, config) = CreateContext(new List<(ContentDocument, RouteInfo)>
        {
            (Item("p1", "Post", "post", fieldValues), Route("/post/"))
        });

        var derived = new AliasPlugin(config).DerivePages(ctx);

        Assert.Single(derived);
        Assert.Equal("/legacy/", derived[0].Route.Url);
    }

    [Fact]
    public void DerivePages_NoAliases_ReturnsEmpty()
    {
        var (ctx, config) = CreateContext(new List<(ContentDocument, RouteInfo)>
        {
            (Item("p1", "Post", "post"), Route("/post/"))
        });

        var derived = new AliasPlugin(config).DerivePages(ctx);
        Assert.Empty(derived);
    }

    [Fact]
    public void DerivePages_RedirectHtml_ContainsCanonicalAndRefresh()
    {
        var fieldValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["aliases"] = new[] { "/old/" }
        };
        var (ctx, config) = CreateContext(new List<(ContentDocument, RouteInfo)>
        {
            (Item("p1", "Post", "post", fieldValues), Route("/post/"))
        });

        var derived = new AliasPlugin(config).DerivePages(ctx);

        var html = derived[0].Document.Body.Html;
        Assert.Contains("http-equiv=\"refresh\"", html);
        Assert.Contains("rel=\"canonical\"", html);
        Assert.Contains("/post/", html);
    }

    [Fact]
    public void DerivePages_AliasWithTrailingSlash_Normalized()
    {
        var fieldValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["aliases"] = new[] { "old-post" }
        };
        var (ctx, config) = CreateContext(new List<(ContentDocument, RouteInfo)>
        {
            (Item("p1", "Post", "post", fieldValues), Route("/post/"))
        });

        var derived = new AliasPlugin(config).DerivePages(ctx);

        Assert.Equal("/old-post/", derived[0].Route.Url);
    }

    [Fact]
    public void DerivePages_AliasItemHasRedirectType()
    {
        var fieldValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["aliases"] = new[] { "/old/" }
        };
        var (ctx, config) = CreateContext(new List<(ContentDocument, RouteInfo)>
        {
            (Item("p1", "Post", "post", fieldValues), Route("/post/"))
        });

        var derived = new AliasPlugin(config).DerivePages(ctx);

        Assert.Equal("redirect", ContentFieldReader.GetText(derived[0].Document.CustomFields, "type"));
    }

    private static (BuildContext Context, AppConfig Config) CreateContext(
        List<(ContentDocument, RouteInfo)> routed)
    {
        var config = new AppConfig
        {
            Site = new SiteConfig { Name = "t", Title = "t" },
            Content = TestContent.Markdown()
        };
        var context = new BuildContext
        {
            RootDir = "/t",
            OutputDir = "/t/out",
            BaseUrl = "/",
            LayoutsDir = "/t/l",
            RoutedDocuments = routed.ToRoutedDocuments(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        return (context, config);
    }
}
