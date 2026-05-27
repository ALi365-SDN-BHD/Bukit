using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SitemapGeneratorTests
{
    [Fact]
    public void Generate_BasicSitemap_HasCorrectXmlStructure()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outDir);

        var routes = new List<(RouteInfo Route, DateTimeOffset LastModified)>
        {
            (new RouteInfo("/", "index.html", "pages/index.html"), new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero)),
            (new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html"), new DateTimeOffset(2024, 6, 2, 0, 0, 0, TimeSpan.Zero)),
            (new RouteInfo("/pages/about/", "pages/about/index.html", "pages/page.html"), new DateTimeOffset(2024, 6, 3, 0, 0, 0, TimeSpan.Zero)),
        };

        SitemapGenerator.Generate(outDir, "https://example.com", "/", routes);

        var sitemap = File.ReadAllText(Path.Combine(outDir, "sitemap.xml"));
        Assert.Contains("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", sitemap, StringComparison.Ordinal);
        Assert.Contains("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">", sitemap, StringComparison.Ordinal);
        Assert.Contains("<loc>https://example.com/</loc>", sitemap, StringComparison.Ordinal);
        Assert.Contains("<loc>https://example.com/blog/post-1/</loc>", sitemap, StringComparison.Ordinal);
        Assert.Contains("<loc>https://example.com/pages/about/</loc>", sitemap, StringComparison.Ordinal);
        Assert.Contains("<lastmod>2024-06-01</lastmod>", sitemap, StringComparison.Ordinal);
        Assert.Contains("<lastmod>2024-06-02</lastmod>", sitemap, StringComparison.Ordinal);
        Assert.Contains("<lastmod>2024-06-03</lastmod>", sitemap, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_EmptyRoutes_ProducesEmptyUrlset()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outDir);

        var routes = Array.Empty<(RouteInfo Route, DateTimeOffset LastModified)>();
        SitemapGenerator.Generate(outDir, "https://example.com", "/", routes);

        var sitemap = File.ReadAllText(Path.Combine(outDir, "sitemap.xml"));
        Assert.DoesNotContain("<url>", sitemap, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_SubpathBaseUrl_HandlesCorrectly()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outDir);

        var routes = new List<(RouteInfo Route, DateTimeOffset LastModified)>
        {
            (new RouteInfo("/blog/post-1/", "blog/post-1/index.html", "pages/post.html"), new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero)),
        };

        SitemapGenerator.Generate(outDir, "https://example.com", "/my-repo", routes);

        var sitemap = File.ReadAllText(Path.Combine(outDir, "sitemap.xml"));
        Assert.Contains("<loc>https://example.com/my-repo/blog/post-1/</loc>", sitemap, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_XmlSpecialChars_Escaped()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outDir);

        SitemapGenerator.Generate(outDir, "https://example.com", "/", Array.Empty<(RouteInfo, DateTimeOffset)>());

        var sitemap = File.ReadAllText(Path.Combine(outDir, "sitemap.xml"));
        Assert.Contains("</urlset>", sitemap, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildAbsoluteUrl_RootBaseUrl()
    {
        var url = SitemapGenerator.BuildAbsoluteUrl("https://example.com", "/", "/blog/hello/");
        Assert.Equal("https://example.com/blog/hello/", url);
    }

    [Fact]
    public void BuildAbsoluteUrl_SubpathBaseUrl()
    {
        var url = SitemapGenerator.BuildAbsoluteUrl("https://example.com", "/my-repo", "/blog/hello/");
        Assert.Equal("https://example.com/my-repo/blog/hello/", url);
    }

    [Fact]
    public void BuildAbsoluteUrl_UrlMissingLeadingSlash()
    {
        var url = SitemapGenerator.BuildAbsoluteUrl("https://example.com", "/", "blog/hello/");
        Assert.Equal("https://example.com/blog/hello/", url);
    }

    [Fact]
    public void BuildAbsoluteUrl_SiteUrlTrailingSlash_ProducesDoubleSlash()
    {
        var url = SitemapGenerator.BuildAbsoluteUrl("https://example.com/", "/", "/blog/hello/");
        Assert.Equal("https://example.com/blog/hello/", url);
    }

    [Fact]
    public void BuildAbsoluteUrl_NormalizesTrailingSlashBaseUrl()
    {
        var url = SitemapGenerator.BuildAbsoluteUrl("https://example.com/", "/docs/", "/blog/hello/");
        Assert.Equal("https://example.com/docs/blog/hello/", url);
    }

    [Fact]
    public void GenerateAbsolute_CreatesSitemapFromAbsoluteUrls()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outDir);

        var entries = new List<(string AbsoluteUrl, DateTimeOffset LastModified)>
        {
            ("https://example.com/", new DateTimeOffset(2024, 7, 1, 0, 0, 0, TimeSpan.Zero)),
            ("https://example.com/blog/a/", new DateTimeOffset(2024, 7, 2, 0, 0, 0, TimeSpan.Zero)),
        };

        SitemapGenerator.GenerateAbsolute(outDir, entries);

        var sitemap = File.ReadAllText(Path.Combine(outDir, "sitemap.xml"));
        Assert.Contains("<loc>https://example.com/</loc>", sitemap, StringComparison.Ordinal);
        Assert.Contains("<loc>https://example.com/blog/a/</loc>", sitemap, StringComparison.Ordinal);
        Assert.Contains("<lastmod>2024-07-01</lastmod>", sitemap, StringComparison.Ordinal);
        Assert.Contains("<lastmod>2024-07-02</lastmod>", sitemap, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateAbsoluteWithAlternates_HasXmlnsXhtml()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outDir);

        var entries = new List<SitemapGenerator.UrlEntry>
        {
            new("https://example.com/", new DateTimeOffset(2024, 8, 1, 0, 0, 0, TimeSpan.Zero),
                new[] { new SitemapGenerator.Alternate("en", "https://example.com/en/") }),
        };

        SitemapGenerator.GenerateAbsoluteWithAlternates(outDir, entries);

        var sitemap = File.ReadAllText(Path.Combine(outDir, "sitemap.xml"));
        Assert.Contains("xmlns:xhtml=\"http://www.w3.org/1999/xhtml\"", sitemap, StringComparison.Ordinal);
        Assert.Contains("<xhtml:link rel=\"alternate\"", sitemap, StringComparison.Ordinal);
        Assert.Contains("hreflang=\"en\"", sitemap, StringComparison.Ordinal);
        Assert.Contains("https://example.com/en/", sitemap, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateAbsoluteWithAlternates_NoAlternates_OmitsXmlnsXhtml()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outDir);

        var entries = new List<SitemapGenerator.UrlEntry>
        {
            new("https://example.com/", new DateTimeOffset(2024, 8, 1, 0, 0, 0, TimeSpan.Zero), null),
        };

        SitemapGenerator.GenerateAbsoluteWithAlternates(outDir, entries);

        var sitemap = File.ReadAllText(Path.Combine(outDir, "sitemap.xml"));
        Assert.DoesNotContain("xhtml", sitemap, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateIndex_CreatesSitemapIndex()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        var outDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outDir);

        var urls = new[] { "https://example.com/en/sitemap.xml", "https://example.com/zh/sitemap.xml" };
        SitemapGenerator.GenerateIndex(outDir, urls);

        var sitemap = File.ReadAllText(Path.Combine(outDir, "sitemap.xml"));
        Assert.Contains("<sitemapindex xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">", sitemap, StringComparison.Ordinal);
        Assert.Contains("<loc>https://example.com/en/sitemap.xml</loc>", sitemap, StringComparison.Ordinal);
        Assert.Contains("<loc>https://example.com/zh/sitemap.xml</loc>", sitemap, StringComparison.Ordinal);
    }
}
