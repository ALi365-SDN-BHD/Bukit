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
    private static ContentDocument Item(string id, string title, string slug, Dictionary<string, object>? meta = null)
    {
        meta ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        return ContentDocument.Create(id, title, slug, DateTimeOffset.UtcNow, "<p>x</p>", ContentFieldReader.ToFieldMap(meta));
    }

    private static RouteInfo Route(string url) => new(url, $"out{url}index.html", "pages/post.html");

    [Fact]
    public void DerivePages_AliasesAsList_GeneratesRedirectPages()
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["aliases"] = new[] { "/old-url/", "/another-old/" }
        };
        var ctx = CreateContext(new List<(ContentDocument, RouteInfo)>
        {
            (Item("p1", "Post", "post", meta), Route("/post/"))
        });

        var derived = new AliasPlugin().DerivePages(ctx);

        Assert.Equal(2, derived.Count);
        Assert.Contains(derived, x => x.Route.Url == "/old-url/");
        Assert.Contains(derived, x => x.Route.Url == "/another-old/");
    }

    [Fact]
    public void DerivePages_AliasAsString_GeneratesRedirectPage()
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["aliases"] = "/legacy/"
        };
        var ctx = CreateContext(new List<(ContentDocument, RouteInfo)>
        {
            (Item("p1", "Post", "post", meta), Route("/post/"))
        });

        var derived = new AliasPlugin().DerivePages(ctx);

        Assert.Single(derived);
        Assert.Equal("/legacy/", derived[0].Route.Url);
    }

    [Fact]
    public void DerivePages_NoAliases_ReturnsEmpty()
    {
        var ctx = CreateContext(new List<(ContentDocument, RouteInfo)>
        {
            (Item("p1", "Post", "post"), Route("/post/"))
        });

        var derived = new AliasPlugin().DerivePages(ctx);
        Assert.Empty(derived);
    }

    [Fact]
    public void DerivePages_RedirectHtml_ContainsCanonicalAndRefresh()
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["aliases"] = new[] { "/old/" }
        };
        var ctx = CreateContext(new List<(ContentDocument, RouteInfo)>
        {
            (Item("p1", "Post", "post", meta), Route("/post/"))
        });

        var derived = new AliasPlugin().DerivePages(ctx);

        var html = derived[0].Document.Body.Html;
        Assert.Contains("http-equiv=\"refresh\"", html);
        Assert.Contains("rel=\"canonical\"", html);
        Assert.Contains("/post/", html);
    }

    [Fact]
    public void DerivePages_AliasWithTrailingSlash_Normalized()
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["aliases"] = new[] { "old-post" }
        };
        var ctx = CreateContext(new List<(ContentDocument, RouteInfo)>
        {
            (Item("p1", "Post", "post", meta), Route("/post/"))
        });

        var derived = new AliasPlugin().DerivePages(ctx);

        Assert.Equal("/old-post/", derived[0].Route.Url);
    }

    [Fact]
    public void DerivePages_AliasItemHasRedirectType()
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["aliases"] = new[] { "/old/" }
        };
        var ctx = CreateContext(new List<(ContentDocument, RouteInfo)>
        {
            (Item("p1", "Post", "post", meta), Route("/post/"))
        });

        var derived = new AliasPlugin().DerivePages(ctx);

        Assert.Equal("redirect", ContentFieldReader.GetText(derived[0].Document.CustomFields, "type"));
    }

    private static BuildContext CreateContext(List<(ContentDocument, RouteInfo)> routed)
    {
        return new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "t", Title = "t" },
                Content = new ContentConfig { Provider = "markdown" }
            },
            RootDir = "/t",
            OutputDir = "/t/out",
            BaseUrl = "/",
            LayoutsDir = "/t/l",
            RoutedDocuments = routed.ToRoutedDocuments(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };
    }
}
