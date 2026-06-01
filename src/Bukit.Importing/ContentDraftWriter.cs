using System.Text;
using System.Text.RegularExpressions;

namespace Bukit.Importing;

internal static partial class ContentDraftWriter
{
    internal static void Write(HtmlDemoImportOptions options, List<DiscoveredPage> pages)
    {
        var siteDir = HtmlDemoImporter.GetSiteDir(options);
        var contentDir = Path.Combine(siteDir, "content");
        Directory.CreateDirectory(contentDir);

        foreach (var page in pages)
        {
            WritePageDraft(contentDir, page, options.Overwrite);
        }
    }

    private static void WritePageDraft(string contentDir, DiscoveredPage page, bool overwrite)
    {
        if (page.Type is PageType.PostList or PageType.CompanyList or PageType.ServiceList)
            return;

        var collection = page.Type switch
        {
            PageType.PostDetail => "posts",
            PageType.CompanyDetail => "companies",
            PageType.ServiceDetail => "services",
            PageType.Page => "pages",
            _ => ""
        };

        var slug = string.IsNullOrWhiteSpace(page.Slug)
            ? "index"
            : page.Slug;

        var dir = string.IsNullOrEmpty(collection)
            ? contentDir
            : Path.Combine(contentDir, collection);
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, $"{slug}.md");
        if (File.Exists(path) && !overwrite) return;

        var title = ExtractHeading(page.UniqueBody) ?? page.Title ?? slug;
        var summary = ExtractSummary(page.UniqueBody);
        var type = page.Type switch
        {
            PageType.PostDetail => "post",
            PageType.CompanyDetail => "company",
            PageType.ServiceDetail => "service",
            _ => "page"
        };

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"title: \"{EscapeYaml(title)}\"");
        sb.AppendLine($"slug: \"{EscapeYaml(slug)}\"");
        sb.AppendLine($"collection: \"{type}\"");
        if (!string.IsNullOrWhiteSpace(summary))
            sb.AppendLine($"summary: \"{EscapeYaml(summary)}\"");
        sb.AppendLine("published: true");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(NormalizeBody(page.UniqueBody));
        File.WriteAllText(path, sb.ToString());
    }

    private static string NormalizeBody(string html)
    {
        var body = html.Trim();
        if (string.IsNullOrWhiteSpace(body))
            return "";

        return body;
    }

    private static string? ExtractHeading(string html)
    {
        var match = HeadingPattern().Match(html);
        return match.Success ? StripHtml(match.Groups[1].Value).Trim() : null;
    }

    private static string? ExtractSummary(string html)
    {
        var match = ParagraphPattern().Match(html);
        if (!match.Success) return null;

        var text = StripHtml(match.Groups[1].Value).Trim();
        return text.Length > 200 ? text[..200] + "..." : text;
    }

    private static string StripHtml(string html)
    {
        return Regex.Replace(html, "<[^>]*>", "").Trim();
    }

    private static string EscapeYaml(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    [GeneratedRegex(@"<h1[^>]*>(.*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ParagraphPattern();
}
