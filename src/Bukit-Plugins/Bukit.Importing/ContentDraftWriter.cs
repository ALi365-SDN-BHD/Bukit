using System.Text;

namespace Bukit.Importing;

internal static class ContentDraftWriter
{
    internal static void Write(HtmlDemoImportOptions options, ExtractedContent content)
    {
        if (IsNotionBuildSource(options))
        {
            Console.WriteLine("  Content draft 已跳过（--build-source notion）");
            return;
        }

        var siteDir = HtmlDemoImporter.GetSiteDir(options);
        var contentDir = Path.Combine(siteDir, "content");
        Directory.CreateDirectory(contentDir);

        foreach (var page in content.Pages.Where(IsBuildPageDraft))
            WritePageDraft(contentDir, page, options.Overwrite);

        foreach (var post in content.Posts)
            WriteCollectionDraft(Path.Combine(contentDir, "posts"), post.Title, post.Slug, "post", post.Summary, post.Content, options.Overwrite);

        foreach (var company in content.Companies)
            WriteCollectionDraft(Path.Combine(contentDir, "companies"), company.Title, company.Slug, "company", company.Summary, company.Content, options.Overwrite);

        foreach (var service in content.Services)
            WriteCollectionDraft(Path.Combine(contentDir, "services"), service.Title, service.Slug, "service", service.Summary, service.Content, options.Overwrite);
    }

    private static bool IsNotionBuildSource(HtmlDemoImportOptions options)
        => options.BuildSource.Equals("notion", StringComparison.OrdinalIgnoreCase);

    private static void WritePageDraft(string contentDir, PageRecord page, bool overwrite)
    {
        var slug = string.IsNullOrWhiteSpace(page.Slug) ? "index" : page.Slug;
        var dir = page.Type.Equals("Home", StringComparison.OrdinalIgnoreCase)
            ? contentDir
            : Path.Combine(contentDir, "pages");
        WriteCollectionDraft(dir, page.Title, slug, "page", page.Summary, page.Content, overwrite);
    }

    private static bool IsBuildPageDraft(PageRecord page)
        => page.Type.Equals("Home", StringComparison.OrdinalIgnoreCase) ||
           page.Template.Equals("page", StringComparison.OrdinalIgnoreCase);

    private static void WriteCollectionDraft(
        string dir,
        string title,
        string slug,
        string collection,
        string? summary,
        string? content,
        bool overwrite)
    {
        Directory.CreateDirectory(dir);

        var safeSlug = string.IsNullOrWhiteSpace(slug) ? "index" : slug;
        var path = Path.Combine(dir, $"{safeSlug}.md");
        if (File.Exists(path) && !overwrite) return;

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"title: \"{EscapeYaml(title)}\"");
        sb.AppendLine($"slug: \"{EscapeYaml(safeSlug)}\"");
        sb.AppendLine($"collection: \"{collection}\"");
        if (!string.IsNullOrWhiteSpace(summary))
            sb.AppendLine($"summary: \"{EscapeYaml(summary)}\"");
        sb.AppendLine("published: true");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(content?.Trim() ?? "");
        File.WriteAllText(path, sb.ToString());
    }

    private static string EscapeYaml(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
