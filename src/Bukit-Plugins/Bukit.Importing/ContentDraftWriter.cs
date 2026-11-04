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
        var claimed = new HashSet<string>(ImportWriteGuard.PathComparer);

        foreach (var page in content.Pages.Where(IsBuildPageDraft))
            WritePageDraft(contentDir, page, claimed, options.Overwrite);

        foreach (var post in content.Posts)
            WriteCollectionDraft(Path.Combine(contentDir, "posts"), post.Title, post.Slug, "post", post.Summary, post.Content, post.SeoTitle, post.SeoDescription, post.TemplatePath, claimed, options.Overwrite);

        foreach (var company in content.Companies)
            WriteCollectionDraft(Path.Combine(contentDir, "companies"), company.Title, company.Slug, "company", company.Summary, company.Content, company.SeoTitle, company.SeoDescription, company.TemplatePath, claimed, options.Overwrite);

        foreach (var service in content.Services)
            WriteCollectionDraft(Path.Combine(contentDir, "services"), service.Title, service.Slug, "service", service.Summary, service.Content, service.SeoTitle, service.SeoDescription, service.TemplatePath, claimed, options.Overwrite);
    }

    private static bool IsNotionBuildSource(HtmlDemoImportOptions options)
        => options.BuildSource.Equals("notion", StringComparison.OrdinalIgnoreCase);

    private static void WritePageDraft(string contentDir, PageRecord page, HashSet<string> claimed, bool overwrite)
    {
        var slug = string.IsNullOrWhiteSpace(page.Slug) ? "index" : page.Slug;
        var dir = page.Type.Equals("Home", StringComparison.OrdinalIgnoreCase)
            ? contentDir
            : Path.Combine(contentDir, "pages");
        WriteCollectionDraft(dir, page.Title, slug, "page", page.Summary, page.Content,
            page.SeoTitle, page.SeoDescription, page.TemplatePath, claimed, overwrite);
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
        string? templatePath,
        HashSet<string> claimed,
        bool overwrite)
    {
        Directory.CreateDirectory(dir);

        var safeSlug = string.IsNullOrWhiteSpace(slug) ? "index" : slug;
        var path = Path.Combine(dir, $"{safeSlug}.md");
        if (!ImportWriteGuard.IsInside(path, dir))
        {
            Console.Error.WriteLine($"Skipped unsafe content slug '{safeSlug}' (record '{title}').");
            return;
        }

        if (!ImportWriteGuard.TryClaim(claimed, path, $"record '{title}'"))
            return;
        if (File.Exists(path) && !overwrite) return;

        var fields = new List<KeyValuePair<string, object?>>
        {
            new("title", title),
            new("slug", safeSlug),
            new("collection", collection),
            // The demo page's own template; without one Core falls back to the collection template.
            new("template", templatePath),
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
