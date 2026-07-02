namespace Bukit.Labs.Cli.Commands;

internal static class CloneFidelityGenerator
{
    internal sealed record FidelityResult(
        int TemplateCount,
        int PartialCount,
        int AssetCount,
        int PageCount,
        List<string> Warnings);

    internal static FidelityResult Generate(string rootDir, string htmlDir, string themeName)
    {
        var warnings = new List<string>();
        var htmlFiles = Directory.GetFiles(htmlDir, "*.html", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (htmlFiles.Count == 0)
        {
            throw new InvalidOperationException($"No .html files found in {htmlDir}");
        }

        var pages = htmlFiles.Select(f => new FidelityPage(f, htmlDir)).ToList();

        var themeDir = Path.Combine(rootDir, "themes", themeName);
        Directory.CreateDirectory(Path.Combine(themeDir, "layouts", "layouts"));
        Directory.CreateDirectory(Path.Combine(themeDir, "layouts", "pages"));
        Directory.CreateDirectory(Path.Combine(themeDir, "layouts", "partials"));
        Directory.CreateDirectory(Path.Combine(themeDir, "assets"));
        Directory.CreateDirectory(Path.Combine(themeDir, "static"));

        var commonBlocks = CloneFidelityCommonBlocks.ExtractCommonBlocks(pages, warnings);

        WritePartial(themeDir, "partials/header.html", commonBlocks.Header);
        WritePartial(themeDir, "partials/nav.html", commonBlocks.Nav);
        WritePartial(themeDir, "partials/footer.html", commonBlocks.Footer);

        var baseLayout = BuildLayout(commonBlocks);
        File.WriteAllText(Path.Combine(themeDir, "layouts", "layouts", "base.html"), baseLayout);

        var pageCount = 0;
        foreach (var page in pages)
        {
            var pageTemplate = BuildPageTemplate(page);
            var pageName = Path.GetFileNameWithoutExtension(page.FilePath);
            var safeName = SanitizeTemplateName(pageName);
            if (safeName is "index" or "list")
            {
                safeName = "page-" + safeName;
            }

            var templatePath = Path.Combine(themeDir, "layouts", "pages", $"{safeName}.html");
            File.WriteAllText(templatePath, pageTemplate);
            pageCount++;
        }

        var indexTemplate = BuildIndexTemplate(pages);
        File.WriteAllText(Path.Combine(themeDir, "layouts", "pages", "index.html"), indexTemplate);
        File.WriteAllText(Path.Combine(themeDir, "layouts", "pages", "list.html"), BuildListTemplate());

        CopyAssets(rootDir, htmlDir, themeDir, pages, out var assetCount);
        CopyStaticFiles(rootDir, htmlDir, themeDir, pages);

        return new FidelityResult(
            TemplateCount: 3 + pageCount,
            PartialCount: (string.IsNullOrEmpty(commonBlocks.Header) ? 0 : 1) +
                          (string.IsNullOrEmpty(commonBlocks.Nav) ? 0 : 1) +
                          (string.IsNullOrEmpty(commonBlocks.Footer) ? 0 : 1),
            AssetCount: assetCount,
            PageCount: pageCount,
            Warnings: warnings);
    }

    private static string BuildLayout(CommonBlocks blocks)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"{{ site.language }}\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        sb.AppendLine("  <title>{{ page.title }} | {{ site.title }}</title>");
        sb.AppendLine("  {{ if page.seo }}");
        sb.AppendLine("  <link rel=\"canonical\" href=\"{{ page.seo.canonical }}\" />");
        sb.AppendLine("  {{ end }}");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        if (!string.IsNullOrWhiteSpace(blocks.Header))
        {
            sb.AppendLine("  {{ include 'partials/header.html' }}");
        }

        if (!string.IsNullOrWhiteSpace(blocks.Nav))
        {
            sb.AppendLine("  {{ include 'partials/nav.html' }}");
        }

        sb.AppendLine("  {{ content }}");

        if (!string.IsNullOrWhiteSpace(blocks.Footer))
        {
            sb.AppendLine("  {{ include 'partials/footer.html' }}");
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static string BuildPageTemplate(FidelityPage page)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{{% layout \"layouts/base.html\" %}}");
        sb.AppendLine(page.UniqueBody.Trim());
        return sb.ToString();
    }

    private static string BuildIndexTemplate(List<FidelityPage> pages)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("{% layout \"layouts/base.html\" %}");
        sb.AppendLine("<main>");
        sb.AppendLine("  <h1>{{ page.title }}</h1>");
        sb.AppendLine("  <ul>");

        foreach (var page in pages)
        {
            sb.AppendLine($"    <li><a href=\"/{page.Slug}/\">{System.Net.WebUtility.HtmlEncode(page.Title)}</a></li>");
        }

        sb.AppendLine("  </ul>");
        sb.AppendLine("</main>");
        return sb.ToString();
    }

    private static string BuildListTemplate()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("{% layout \"layouts/base.html\" %}");
        sb.AppendLine("<main>");
        sb.AppendLine("  <h1>{{ page.title }}</h1>");
        sb.AppendLine("  {{ for p in pages }}");
        sb.AppendLine("  <article>");
        sb.AppendLine("    <h2><a href=\"{{ p.url }}\">{{ p.title }}</a></h2>");
        sb.AppendLine("  </article>");
        sb.AppendLine("  {{ end }}");
        sb.AppendLine("</main>");
        return sb.ToString();
    }

    private static void WritePartial(string themeDir, string relativePath, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        var fullPath = Path.Combine(themeDir, "layouts", relativePath);
        File.WriteAllText(fullPath, CloneFidelityCommonBlocks.NormalizeBlock(content));
    }

    private static void CopyAssets(string rootDir, string htmlDir, string themeDir, List<FidelityPage> pages, out int count)
    {
        count = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var page in pages)
        {
            foreach (var asset in page.Assets)
            {
                if (!seen.Add(asset)) continue;

                var sourcePath = Path.GetFullPath(Path.Combine(htmlDir, asset.TrimStart('/')));
                var ext = Path.GetExtension(asset).ToLowerInvariant();
                var isImage = ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".webp" or ".ico";
                var destSubDir = isImage ? "assets" : "static";
                var destPath = Path.Combine(themeDir, destSubDir, asset.TrimStart('/'));

                if (File.Exists(sourcePath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    File.Copy(sourcePath, destPath, overwrite: true);
                    count++;
                }
            }
        }

        var sourceAssetsDir = Path.Combine(htmlDir, "assets");
        if (Directory.Exists(sourceAssetsDir))
        {
            foreach (var file in Directory.GetFiles(sourceAssetsDir, "*.*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(sourceAssetsDir, file);
                var dest = Path.Combine(themeDir, "assets", rel);
                if (!File.Exists(dest))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Copy(file, dest);
                    count++;
                }
            }
        }
    }

    private static void CopyStaticFiles(string rootDir, string htmlDir, string themeDir, List<FidelityPage> pages)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var page in pages)
        {
            foreach (var asset in page.Assets)
            {
                if (!seen.Add(asset)) continue;

                var ext = Path.GetExtension(asset).ToLowerInvariant();
                if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".webp" or ".ico")
                    continue;

                var sourcePath = Path.GetFullPath(Path.Combine(htmlDir, asset.TrimStart('/')));
                if (File.Exists(sourcePath))
                {
                    var destPath = Path.Combine(themeDir, "static", asset.TrimStart('/'));
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    File.Copy(sourcePath, destPath, overwrite: true);
                }
            }
        }
    }

    private static string SanitizeTemplateName(string name)
    {
        var chars = name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray();
        var result = new string(chars);
        while (result.Contains("--"))
            result = result.Replace("--", "-");

        return result.Trim('-').ToLowerInvariant();
    }
}
