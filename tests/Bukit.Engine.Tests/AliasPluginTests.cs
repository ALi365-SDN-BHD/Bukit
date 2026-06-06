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
    private static ContentItem Item(string id, string title, string slug, Dictionary<string, object>? meta = null)
    {
        meta ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase);
        if (meta.TryGetValue("aliases", out var aliases))
        {
            fields["aliases"] = new("list", aliases);
        }

        return new ContentItem(
            id,
            title,
            slug,
            DateTimeOffset.UtcNow,
            "<p>x</p>",
            fields);
    }

    private static RouteInfo Route(string url) => new(url, $"out{url}index.html", "pages/post.html");

    [Fact]
    public void DerivePages_AliasesAsList_GeneratesRedirectPages()
    {
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["aliases"] = new[] { "/old-url/", "/another-old/" }
        };
        var ctx = CreateContext(new List<(ContentItem, RouteInfo)>
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
        var ctx = CreateContext(new List<(ContentItem, RouteInfo)>
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
        var ctx = CreateContext(new List<(ContentItem, RouteInfo)>
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
        var ctx = CreateContext(new List<(ContentItem, RouteInfo)>
        {
            (Item("p1", "Post", "post", meta), Route("/post/"))
        });

        var derived = new AliasPlugin().DerivePages(ctx);

        var html = derived[0].Item.ContentHtml;
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
        var ctx = CreateContext(new List<(ContentItem, RouteInfo)>
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
        var ctx = CreateContext(new List<(ContentItem, RouteInfo)>
        {
            (Item("p1", "Post", "post", meta), Route("/post/"))
        });

        var derived = new AliasPlugin().DerivePages(ctx);

        Assert.StartsWith("[Redirect]", derived[0].Item.Title, StringComparison.Ordinal);
    }

    private static BuildContext CreateContext(List<(ContentItem, RouteInfo)> routed)
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
            Routed = routed,
            Logger = new ConsoleLogger(LogLevel.Error)
        };
    }
}
