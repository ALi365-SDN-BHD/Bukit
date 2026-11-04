using System.Text;

namespace Bukit.Importing;

internal static class ContentDraftWriter
{
    internal static void Write(HtmlDemoImportOptions options, ExtractedContent content)
    {
        if (IsNotionBuildSource(options))
        {
            Console.WriteLine("  Content draft skipped (--build-source notion)");
            return;
        }

        var siteDir = HtmlDemoImporter.GetSiteDir(options);
        var contentDir = Path.Combine(siteDir, "content");
        Directory.CreateDirectory(contentDir);

        foreach (var page in content.Pages.Where(IsBuildPageDraft))
            WritePageDraft(contentDir, page, options.Overwrite);

        foreach (var post in content.Posts)
            WriteCollectionDraft(Path.Combine(contentDir, "posts"), post.Title, post.Slug, "post", post.Summary, post.Content, post.SeoTitle, post.SeoDescription, options.Overwrite);

        foreach (var company in content.Companies)
            WriteCollectionDraft(Path.Combine(contentDir, "companies"), company.Title, company.Slug, "company", company.Summary, company.Content, company.SeoTitle, company.SeoDescription, options.Overwrite);

        foreach (var service in content.Services)
            WriteCollectionDraft(Path.Combine(contentDir, "services"), service.Title, service.Slug, "service", service.Summary, service.Content, service.SeoTitle, service.SeoDescription, options.Overwrite);
    }

    private static bool IsNotionBuildSource(HtmlDemoImportOptions options)
        => options.BuildSource.Equals("notion", StringComparison.OrdinalIgnoreCase);

    private static void WritePageDraft(string contentDir, PageRecord page, bool overwrite)
    {
        var slug = string.IsNullOrWhiteSpace(page.Slug) ? "index" : page.Slug;
        var dir = page.Type.Equals("Home", StringComparison.OrdinalIgnoreCase)
            ? contentDir
            : Path.Combine(contentDir, "pages");
        WriteCollectionDraft(dir, page.Title, slug, "page", page.Summary, page.Content,
            page.SeoTitle, page.SeoDescription, overwrite);
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
        string? seoTitle,
        string? seoDescription,
        bool overwrite)
    {
        Directory.CreateDirectory(dir);

        var safeSlug = string.IsNullOrWhiteSpace(slug) ? "index" : slug;
        var path = Path.Combine(dir, $"{safeSlug}.md");
        if (File.Exists(path) && !overwrite) return;

        var fields = new List<KeyValuePair<string, object?>>
        {
            new("title", title),
            new("slug", safeSlug),
            new("collection", collection),
            new("summary", summary),
            new("seo_title", seoTitle),
            new("seo_desc", seoDescription),
            new("draft", false)
        };

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.Append(YamlEmitter.Emit(fields));
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(HtmlToMarkdownConverter.Convert(content));
        File.WriteAllText(path, sb.ToString());
    }
}
