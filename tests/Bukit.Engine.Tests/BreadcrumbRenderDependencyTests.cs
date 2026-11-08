using Bukit.Config;
using Bukit.Engine.Abstractions.Routing;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class BreadcrumbRenderDependencyTests
{
    private const string BaseHash = "base";
    private static readonly RouteInfo Post = new("/2596/", "2596/index.html", "pages/post.html");

    [Fact]
    public void OptionsOff_KeepsInnerResolver()
    {
        var catalog = Catalog("最新動態");
        Func<RouteInfo, string> inner = _ => "inner";

        Assert.Same(inner, VariantRenderStage.WithBreadcrumbDependencies(new SeoSchemaConfig(), catalog, BaseHash, inner));
        Assert.Null(VariantRenderStage.WithBreadcrumbDependencies(new SeoSchemaConfig(), catalog, BaseHash, inner: null));
    }

    [Fact]
    public void OptionsOn_HashFollowsBreadcrumbNodes()
    {
        var schema = new SeoSchemaConfig { BreadcrumbParent = "categories" };
        string Hash(BreadcrumbDescriptorCatalog catalog, Func<RouteInfo, string>? inner = null)
            => VariantRenderStage.WithBreadcrumbDependencies(schema, catalog, BaseHash, inner)!(Post);

        Assert.NotEqual(BaseHash, Hash(Catalog("news")));
        Assert.NotEqual(Hash(Catalog("news")), Hash(Catalog("最新動態")));
        Assert.Equal(Hash(Catalog("最新動態")), Hash(Catalog("最新動態")));
        Assert.NotEqual(Hash(Catalog("最新動態")), Hash(Catalog("最新動態"), _ => "route-metadata"));
        var other = new RouteInfo("/no-breadcrumb/", "no-breadcrumb/index.html", "pages/post.html");
        Assert.Equal(BaseHash, VariantRenderStage.WithBreadcrumbDependencies(schema, Catalog("news"), BaseHash, null)!(other));
    }

    private static BreadcrumbDescriptorCatalog Catalog(string categoryName)
        => new(new Dictionary<string, BreadcrumbDescriptor>(StringComparer.OrdinalIgnoreCase)
        {
            ["/2596"] = new(
            [
                new BreadcrumbItemDescriptor(categoryName, "https://example.com/categories/news/"),
                new BreadcrumbItemDescriptor("文章", "https://example.com/2596/")
            ])
        });
}
