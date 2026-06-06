using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class RouteSecurityValidatorTests
{
    [Theory]
    [InlineData("https://evil.com")]
    [InlineData("//evil.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,test")]
    [InlineData("vbscript:msgbox(1)")]
    public void ValidateInternalUrl_ExternalOrDangerousUrl_Throws(string url)
    {
        var ex = Assert.Throws<ConfigException>(() => RouteSecurityValidator.ValidateInternalUrl(url, "test"));

        Assert.Contains("test", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(url, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("..\\evil")]
    [InlineData("a/../../x")]
    [InlineData("CON")]
    [InlineData("aux")]
    [InlineData("")]
    public void ValidateOutputPath_UnsafeSegments_Throws(string value)
    {
        var ex = Assert.Throws<ConfigException>(() => RouteSecurityValidator.ValidateOutputPath(value, "test"));

        Assert.Contains("test", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateFinalRoutes_StaticRouteWithExternalUrl_Throws()
    {
        var route = new RouteInfo("https://evil.com", "safe/index.html", "pages/static.html");

        Assert.Throws<ConfigException>(() =>
            RouteInventoryValidator.ValidateFinalRoutes(
                Array.Empty<(ContentDocument Document, RouteInfo Route)>(),
                Array.Empty<(ContentItem Item, RouteInfo Route)>(),
                staticHtmlRoutes: new[] { route }));
    }

    [Fact]
    public void ValidateFinalRoutes_DerivedRouteWithTraversalOutputPath_Throws()
    {
        var item = new ContentItem(
            Id: "plugin",
            Title: "Plugin Page",
            Slug: "plugin-page",
            PublishAt: DateTimeOffset.UtcNow,
            ContentHtml: "",
            Meta: new Dictionary<string, object>());
        var route = new RouteInfo("/plugin-page/", "../evil/index.html", "pages/page.html");

        Assert.Throws<ConfigException>(() =>
            RouteInventoryValidator.ValidateFinalRoutes(
                Array.Empty<(ContentDocument Document, RouteInfo Route)>(),
                new[] { (item, route) }));
    }

    [Theory]
    [InlineData("safe")]
    [InlineData("safe-page")]
    [InlineData("safe_page")]
    [InlineData("安全")]
    public void ValidateSlugSegment_SafeSegments_Passes(string segment)
    {
        RouteSecurityValidator.ValidateSlugSegment(segment, "test");
    }
}
