using System.Text;
using System.Text.RegularExpressions;

namespace Bukit.Importing;

internal static partial class ThemeGenerator
{
    private static readonly Regex CssLinkRegex = CssLinkPattern();
    private static readonly Regex ScriptSrcRegex = ScriptSrcPattern();
    private static readonly Regex InlineStyleRegex = InlineStylePattern();

    internal static ImportResult Generate(
        HtmlDemoImportOptions options,
        List<DiscoveredPage> pages,
        LayoutExtractor.LayoutInfo layout,
        List<string> warnings,
        Dictionary<string, string> pathMappings)
    {
        var themeDir = Path.Combine(options.RootDir, "themes", options.ThemeName);

        if (Directory.Exists(themeDir) && options.Force)
            Directory.Delete(themeDir, recursive: true);

        Directory.CreateDirectory(Path.Combine(themeDir, "layouts", "layouts"));
        Directory.CreateDirectory(Path.Combine(themeDir, "layouts", "pages"));
        Directory.CreateDirectory(Path.Combine(themeDir, "layouts", "partials"));
        Directory.CreateDirectory(Path.Combine(themeDir, "assets"));
        Directory.CreateDirectory(Path.Combine(themeDir, "static"));

        var partialCount = 0;
        if (!string.IsNullOrWhiteSpace(layout.Header))
        {
            WritePartial(themeDir, "header.html", layout.Header, pathMappings);
            partialCount++;
        }

        if (!string.IsNullOrWhiteSpace(layout.Nav))
        {
            WritePartial(themeDir, "nav.html", layout.Nav, pathMappings);
            partialCount++;
        }

        if (!string.IsNullOrWhiteSpace(layout.Footer))
        {
            WritePartial(themeDir, "footer.html", layout.Footer, pathMappings);
            partialCount++;
        }

        var (cssLinks, scriptTags) = ExtractHeadAssets(layout.HeadExtras, pages);
        WriteBaseLayout(themeDir, layout, cssLinks, scriptTags, pathMappings);

        var templateCount = 0;
        foreach (var page in pages)
        {
            var templateName = GetTemplateFileName(page);
            var templatePath = Path.Combine(themeDir, "layouts", "pages", templateName);
            var existingPage = pages.FirstOrDefault(p =>
                GetTemplateFileName(p) == templateName && p != page);

            if (existingPage != null)
            {
                templateName = SanitizeTemplateName(page.Slug) + ".html";
                if (templateName is "index.html" or "list.html")
                    templateName = "page-" + templateName;
                templatePath = Path.Combine(themeDir, "layouts", "pages", templateName);
            }

            WritePageTemplate(templatePath, page, pathMappings);
            templateCount++;
        }

        var indexExists = pages.Any(p =>
            GetTemplateFileName(p) == "index.html");
        if (!indexExists)
        {
            WriteIndexTemplate(themeDir, pages, pathMappings);
            templateCount++;
        }

        var listExists = pages.Any(p =>
            GetTemplateFileName(p) == "list.html");
        if (!listExists)
        {
            WriteListTemplate(themeDir, pathMappings);
            templateCount++;
        }

        return new ImportResult
        {
            ThemePath = themeDir,
            PagesFound = pages.Count,
            TemplatesGenerated = templateCount,
            PartialsGenerated = partialCount,
            AssetsCopied = 0,
            SiteYamlCreated = false,
            TemplatesSynced = false,
            Warnings = warnings
        };
    }

    private static (List<string> CssLinks, List<string> ScriptTags) ExtractHeadAssets(
        string headContent, List<DiscoveredPage> pages)
    {
        var cssLinks = new List<string>();
        var scriptTags = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddFromText(string text)
        {
            foreach (Match m in CssLinkRegex.Matches(text))
                if (seen.Add(m.Value)) cssLinks.Add(m.Value);
            foreach (Match m in ScriptSrcRegex.Matches(text))
                if (seen.Add(m.Value)) scriptTags.Add(m.Value);
        }

        AddFromText(headContent);
        foreach (var page in pages)
            AddFromText(page.HeadContent ?? "");

        return (cssLinks, scriptTags);
    }

    private static string GetTemplateFileName(DiscoveredPage page)
    {
        return page.Type switch
        {
            PageType.Home => "index.html",
            PageType.Page => "page.html",
            PageType.PostList => SanitizeTemplateName(page.Slug) + ".html",
            PageType.PostDetail => "article.html",
            PageType.CompanyList => "companies.html",
            PageType.CompanyDetail => "company.html",
            PageType.ServiceList => "services.html",
            PageType.ServiceDetail => "service.html",
            _ => SanitizeTemplateName(page.Slug) + ".html"
        };
    }

    private static string SanitizeTemplateName(string name)
    {
        var chars = name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray();
        var result = new string(chars);
        while (result.Contains("--"))
            result = result.Replace("--", "-");
        return result.Trim('-').ToLowerInvariant();
    }

    private static void WritePartial(string themeDir, string fileName, string content,
        Dictionary<string, string> pathMappings)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        var rewritten = AssetImporter.RewritePaths(content, pathMappings);
        var fullPath = Path.Combine(themeDir, "layouts", "partials", fileName);
        File.WriteAllText(fullPath, LayoutExtractor.NormalizeBlock(rewritten));
    }

    private static void WriteBaseLayout(string themeDir, LayoutExtractor.LayoutInfo layout,
        List<string> cssLinks, List<string> scriptTags,
        Dictionary<string, string> pathMappings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"{{ site.language }}\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");

        foreach (var css in cssLinks)
            sb.AppendLine($"  {AssetImporter.RewritePaths(css.Trim(), pathMappings)}");

        sb.AppendLine("  {{ if page.seo_title }}");
        sb.AppendLine("  <title>{{ page.seo_title }} | {{ site.title }}</title>");
        sb.AppendLine("  {{ else }}");
        sb.AppendLine("  <title>{{ page.title }} | {{ site.title }}</title>");
        sb.AppendLine("  {{ end }}");
        sb.AppendLine("  {{ if page.seo_description }}");
        sb.AppendLine("  <meta name=\"description\" content=\"{{ page.seo_description }}\" />");
        sb.AppendLine("  {{ end }}");
        sb.AppendLine("  {{ if page.seo }}");
        sb.AppendLine("  <link rel=\"canonical\" href=\"{{ page.seo.canonical }}\" />");
        sb.AppendLine("  {{ end }}");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        if (!string.IsNullOrWhiteSpace(layout.Header))
            sb.AppendLine("  {{ include 'partials/header.html' }}");

        if (!string.IsNullOrWhiteSpace(layout.Nav))
            sb.AppendLine("  {{ include 'partials/nav.html' }}");

        sb.AppendLine("  {{ content }}");

        if (!string.IsNullOrWhiteSpace(layout.Footer))
            sb.AppendLine("  {{ include 'partials/footer.html' }}");

        foreach (var script in scriptTags)
            sb.AppendLine($"  {AssetImporter.RewritePaths(script.Trim(), pathMappings)}");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        File.WriteAllText(
            Path.Combine(themeDir, "layouts", "layouts", "base.html"), sb.ToString());
    }

    private static void WritePageTemplate(string templatePath, DiscoveredPage page,
        Dictionary<string, string> pathMappings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{% layout \"layouts/base.html\" %}");
        var body = AssetImporter.RewritePaths(page.UniqueBody.Trim(), pathMappings);
        sb.AppendLine(body);
        File.WriteAllText(templatePath, sb.ToString());
    }

    private static void WriteIndexTemplate(string themeDir, List<DiscoveredPage> pages,
        Dictionary<string, string> pathMappings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{% layout \"layouts/base.html\" %}");
        sb.AppendLine("<main>");
        sb.AppendLine("  <h1>{{ page.title }}</h1>");
        sb.AppendLine("  <ul>");

        foreach (var page in pages)
        {
            var slug = page.Slug;
            var href = string.IsNullOrEmpty(slug) ? "/" : $"/{slug}/";
            sb.AppendLine($"    <li><a href=\"{href}\">{System.Net.WebUtility.HtmlEncode(page.Title ?? page.Slug)}</a></li>");
        }

        sb.AppendLine("  </ul>");
        sb.AppendLine("</main>");
        var content = AssetImporter.RewritePaths(sb.ToString(), pathMappings);
        File.WriteAllText(
            Path.Combine(themeDir, "layouts", "pages", "index.html"), content);
    }

    private static void WriteListTemplate(string themeDir,
        Dictionary<string, string> pathMappings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{% layout \"layouts/base.html\" %}");
        sb.AppendLine("<main>");
        sb.AppendLine("  <h1>{{ page.title }}</h1>");
        sb.AppendLine("  {{ for p in pages }}");
        sb.AppendLine("  <article>");
        sb.AppendLine("    <h2><a href=\"{{ p.url }}\">{{ p.title }}</a></h2>");
        sb.AppendLine("  </article>");
        sb.AppendLine("  {{ end }}");
        sb.AppendLine("</main>");
        var content = AssetImporter.RewritePaths(sb.ToString(), pathMappings);
        File.WriteAllText(
            Path.Combine(themeDir, "layouts", "pages", "list.html"), content);
    }

    [GeneratedRegex(@"<link[^>]*rel=[""']stylesheet[""'][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex CssLinkPattern();

    [GeneratedRegex(@"<script[^>]*src=[""'][^""']*[""'][^>]*>\s*</script>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptSrcPattern();

    [GeneratedRegex(@"<style[^>]*>.*?</style>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InlineStylePattern();
}
