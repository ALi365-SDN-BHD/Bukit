using System.Text;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
namespace Bukit.Engine;

internal static class SitemapGenerator
{
    public sealed record Alternate(string Hreflang, string Href);

    public sealed record UrlEntry(string AbsoluteUrl, DateTimeOffset LastModified, IReadOnlyList<Alternate>? Alternates);

    public static void Generate(
        string outputDir,
        string siteUrl,
        string baseUrl,
        IReadOnlyList<(RouteInfo Route, DateTimeOffset LastModified)> routes)
    {
        var normalizedSiteUrl = NormalizeSiteUrl(siteUrl);
        var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        foreach (var (route, lastModified) in routes)
        {
            var loc = BuildAbsoluteUrl(normalizedSiteUrl, normalizedBaseUrl, route.Url);
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{EscapeXml(loc)}</loc>");
            sb.AppendLine($"    <lastmod>{lastModified:yyyy-MM-dd}</lastmod>");
            sb.AppendLine("  </url>");
        }

        sb.AppendLine("</urlset>");

        FileWriter.WriteUtf8(outputDir, "sitemap.xml", sb.ToString());
    }

    public static void GenerateAbsolute(
        string outputDir,
        IReadOnlyList<(string AbsoluteUrl, DateTimeOffset LastModified)> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        foreach (var (absoluteUrl, lastModified) in entries)
        {
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{EscapeXml(absoluteUrl)}</loc>");
            sb.AppendLine($"    <lastmod>{lastModified:yyyy-MM-dd}</lastmod>");
            sb.AppendLine("  </url>");
        }

        sb.AppendLine("</urlset>");
        FileWriter.WriteUtf8(outputDir, "sitemap.xml", sb.ToString());
    }

    public static void GenerateAbsoluteWithAlternates(string outputDir, IReadOnlyList<UrlEntry> entries)
    {
        var hasAlternates = entries.Any(e => e.Alternates is { Count: > 0 });

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine(hasAlternates
            ? "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">"
            : "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        foreach (var entry in entries)
        {
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{EscapeXml(entry.AbsoluteUrl)}</loc>");
            sb.AppendLine($"    <lastmod>{entry.LastModified:yyyy-MM-dd}</lastmod>");

            if (hasAlternates && entry.Alternates is { Count: > 0 } alts)
            {
                foreach (var a in alts)
                {
                    sb.AppendLine($"    <xhtml:link rel=\"alternate\" hreflang=\"{EscapeXml(a.Hreflang)}\" href=\"{EscapeXml(a.Href)}\" />");
                }
            }

            sb.AppendLine("  </url>");
        }

        sb.AppendLine("</urlset>");
        FileWriter.WriteUtf8(outputDir, "sitemap.xml", sb.ToString());
    }

    public static void GenerateIndex(string outputDir, IReadOnlyList<string> sitemapAbsoluteUrls)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<sitemapindex xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        foreach (var url in sitemapAbsoluteUrls)
        {
            sb.AppendLine("  <sitemap>");
            sb.AppendLine($"    <loc>{EscapeXml(url)}</loc>");
            sb.AppendLine("  </sitemap>");
        }

        sb.AppendLine("</sitemapindex>");
        FileWriter.WriteUtf8(outputDir, "sitemap.xml", sb.ToString());
    }

    public static string BuildAbsoluteUrl(string siteUrl, string baseUrl, string url)
    {
        siteUrl = NormalizeSiteUrl(siteUrl);
        baseUrl = NormalizeBaseUrl(baseUrl);
        var u = url.StartsWith('/') ? url : "/" + url;
        var path = baseUrl == "/" ? u : $"{baseUrl}{u}";
        return siteUrl + path;
    }

    private static string NormalizeSiteUrl(string siteUrl)
    {
        var trimmed = siteUrl.Trim();
        if (trimmed.EndsWith('/'))
        {
            trimmed = trimmed.TrimEnd('/');
        }

        return trimmed;
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        var trimmed = baseUrl.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "/";
        }

        if (!trimmed.StartsWith('/'))
        {
            trimmed = "/" + trimmed;
        }

        if (trimmed.Length > 1 && trimmed.EndsWith('/'))
        {
            trimmed = trimmed.TrimEnd('/');
        }

        return trimmed;
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }
}
