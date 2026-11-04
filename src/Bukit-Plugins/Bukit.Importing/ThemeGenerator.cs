using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bukit.Importing;

internal static partial class ThemeGenerator
{
    internal static ImportResult Generate(
        HtmlDemoImportOptions options,
        List<DiscoveredPage> pages,
        LayoutExtractor.LayoutInfo layout,
        List<string> warnings,
        Dictionary<string, string> pathMappings,
        RouteMapConfig? routeMap = null,
        PageTemplatePlan? plan = null)
    {
        var themeDir = HtmlDemoImporter.GetThemeDir(options);

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

        if (!string.IsNullOrWhiteSpace(layout.Nav) && !layout.HeaderContainsNav)
        {
            WritePartial(themeDir, "nav.html", layout.Nav, pathMappings);
            partialCount++;
        }

        if (!string.IsNullOrWhiteSpace(layout.Footer))
        {
            WritePartial(themeDir, "footer.html", layout.Footer, pathMappings);
            partialCount++;
        }

        // base.html holds structure only; every asset is written by the page that declares it.
        WriteBaseLayout(themeDir, layout);

        plan ??= PageTemplatePlan.Build(pages, routeMap);
        var templateCount = 0;
        foreach (var page in pages)
        {
            var templatePath = Path.Combine(themeDir, "layouts", "pages", plan.NameFor(page) + ".html");
            WritePageTemplate(templatePath, page, pathMappings);
            templateCount++;
        }

        var pageTypes = new HashSet<PageType>();
        foreach (var page in pages)
            pageTypes.Add(page.Type);

        if (!pageTypes.Contains(PageType.Home))
        {
            WriteIndexTemplate(themeDir, pages, pathMappings);
            templateCount++;
        }

        templateCount += WriteFallbackTemplates(themeDir, pageTypes);

        string? ListName(PageType type)
            => pages.FirstOrDefault(p => p.Type == type) is { } list ? plan.NameFor(list) : null;
        WriteThemeYaml(themeDir, options.ThemeName, pageTypes,
            ListName(PageType.PostList), ListName(PageType.CompanyList), ListName(PageType.ServiceList));

        return new ImportResult
        {
            ThemePath = themeDir,
            SitePath = HtmlDemoImporter.GetSiteDir(options),
            PagesFound = pages.Count,
            TemplatesGenerated = templateCount,
            PartialsGenerated = partialCount,
            AssetsCopied = 0,
            SiteYamlCreated = false,
            TemplatesSynced = false,
            Warnings = warnings,
            PageTypes = pageTypes,
            PostListSlug = pages.FirstOrDefault(p => p.Type == PageType.PostList)?.Slug
        };
    }

    /// <summary>
    /// Generic templates Core uses when content has no template of its own. They are
    /// written from fixed bodies and never carry a specific page's markup or assets.
    /// </summary>
    private static int WriteFallbackTemplates(string themeDir, HashSet<PageType> pageTypes)
    {
        var count = 0;
        void Write(string name, PageType bodyType)
        {
            File.WriteAllText(
                Path.Combine(themeDir, "layouts", "pages", name + ".html"),
                LayoutDirective + Environment.NewLine + GetDefaultTemplateBody(bodyType));
            count++;
        }

        Write("page", PageType.Page);
        Write("list", PageType.PostList);
        if (pageTypes.Contains(PageType.PostDetail) || pageTypes.Contains(PageType.PostList))
            Write("post", PageType.PostDetail);
        if (pageTypes.Contains(PageType.CompanyDetail) || pageTypes.Contains(PageType.CompanyList))
            Write("company", PageType.CompanyDetail);
        if (pageTypes.Contains(PageType.ServiceDetail) || pageTypes.Contains(PageType.ServiceList))
            Write("service", PageType.ServiceDetail);
        return count;
    }

    private const string LayoutDirective = "{% layout \"layouts/base.html\" %}";

    // Page templates render before base.html, so they set base_url themselves.
    private const string BaseUrlPrelude =
        "{{ base_url = site.base_url }}{{ if base_url == \"/\" }}{{ base_url = \"\" }}{{ end }}";

    internal static string SanitizeTemplateName(string name)
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

        var rewritten = MakeNavigationDataAware(
            AssetImporter.RewritePaths(PageAssetCollector.StripAssets(content), pathMappings));
        if (string.IsNullOrWhiteSpace(rewritten)) return;
        var fullPath = Path.Combine(themeDir, "layouts", "partials", fileName);
        File.WriteAllText(fullPath, LayoutExtractor.NormalizeBlock(rewritten));
    }

    private static string MakeNavigationDataAware(string html)
    {
        var candidate = NavigationMarkupExtractor.ExtractBest(html);
        if (candidate is null ||
            candidate.Kind == NavigationMarkupExtractor.CandidateKind.HeaderLinks)
            return html;

        var originalInner = candidate.InnerHtml.Trim();
        var sb = new StringBuilder();
        sb.AppendLine(candidate.OpeningTag);
        sb.AppendLine("  {{ if site.modules && site.modules.navigation }}");
        sb.AppendLine("    {{ for item in site.modules.navigation }}");
        sb.AppendLine("      {{ nav_url = \"/\" }}");
        sb.AppendLine("      {{ if item.fields && item.fields.link }}{{ nav_url = item.fields.link.value }}{{ end }}");
        sb.AppendLine(NavigationItemTemplate(candidate.TagName));
        sb.AppendLine("    {{ end }}");
        sb.AppendLine("  {{ else }}");
        sb.AppendLine(Indent(originalInner, "    "));
        sb.AppendLine("  {{ end }}");
        sb.AppendLine(candidate.ClosingTag);
        var replacement = sb.ToString();

        return html.Replace(candidate.Markup, replacement, StringComparison.Ordinal);
    }

    private static string NavigationItemTemplate(string tagName)
        => tagName.Equals("ul", StringComparison.OrdinalIgnoreCase) ||
           tagName.Equals("ol", StringComparison.OrdinalIgnoreCase)
            ? "      <li><a href=\"{{ nav_url }}\">{{ item.title }}</a></li>"
            : "      <a href=\"{{ nav_url }}\">{{ item.title }}</a>";

    private static string Indent(string text, string prefix)
        => string.Join(Environment.NewLine, text.Split(["\r\n", "\n"], StringSplitOptions.None)
            .Select(line => string.IsNullOrWhiteSpace(line) ? line : prefix + line));

    private static void WriteBaseLayout(string themeDir, LayoutExtractor.LayoutInfo layout)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"{{ site.language }}\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        sb.AppendLine("  " + BaseUrlPrelude);
        sb.AppendLine("  {{ if page.fields.seo_title }}");
        sb.AppendLine("  <title>{{ page.fields.seo_title.value }} | {{ site.title }}</title>");
        sb.AppendLine("  {{ else }}");
        sb.AppendLine("  <title>{{ page.title }} | {{ site.title }}</title>");
        sb.AppendLine("  {{ end }}");
        sb.AppendLine("  {{ if page.fields.seo_desc }}");
        sb.AppendLine("  <meta name=\"description\" content=\"{{ page.fields.seo_desc.value }}\" />");
        sb.AppendLine("  {{ end }}");
        sb.AppendLine("  {{ if page.seo }}");
        sb.AppendLine("  <link rel=\"canonical\" href=\"{{ page.seo.canonical }}\" />");
        sb.AppendLine("  {{ end }}");
        // Filled by the rendering page template; fallback templates leave it empty.
        sb.AppendLine("  {{ page_head }}");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        if (!string.IsNullOrWhiteSpace(layout.Header))
            sb.AppendLine("  {{ include 'partials/header.html' }}");

        if (!string.IsNullOrWhiteSpace(layout.Nav) && !layout.HeaderContainsNav)
            sb.AppendLine("  {{ include 'partials/nav.html' }}");

        sb.AppendLine("  {{ content }}");

        if (!string.IsNullOrWhiteSpace(layout.Footer))
            sb.AppendLine("  {{ include 'partials/footer.html' }}");

        sb.AppendLine("  {{ page_scripts }}");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        File.WriteAllText(
            Path.Combine(themeDir, "layouts", "layouts", "base.html"), sb.ToString());
    }

    private static void WritePageTemplate(string templatePath, DiscoveredPage page,
        Dictionary<string, string> pathMappings)
    {
        var assets = PageAssetCollector.Collect(page, pathMappings);
        var sb = new StringBuilder();
        sb.AppendLine(LayoutDirective);
        sb.AppendLine(BaseUrlPrelude);
        AppendCapture(sb, "page_head", assets.Head);
        AppendCapture(sb, "page_scripts", assets.Scripts);
        sb.AppendLine(TemplateBodyTransformer.Transform(page, pathMappings));
        File.WriteAllText(templatePath, sb.ToString());
    }

    // base.html prints these variables in <head> and before </body>.
    private static void AppendCapture(StringBuilder sb, string variable, IReadOnlyList<string> tags)
    {
        if (tags.Count == 0)
            return;

        sb.AppendLine($"{{{{ capture {variable} }}}}");
        foreach (var tag in tags)
            sb.AppendLine("  " + tag);
        sb.AppendLine("{{ end }}");
    }

    internal static string GetDefaultTemplateBody(PageType pageType)
    {
        return pageType switch
        {
            PageType.Home => WriteHomeTemplateBody(),
            PageType.PostList or PageType.CompanyList or PageType.ServiceList => """
<main>
  <h1>{{ page.title }}</h1>
  {{ if page.content }}<div class="content">{{ page.content }}</div>{{ end }}
  {{ for p in pages }}
  <article>
    <h2><a href="{{ p.url }}">{{ p.title }}</a></h2>
    {{ if p.summary }}<p>{{ p.summary }}</p>{{ end }}
  </article>
  {{ end }}
</main>
""",
            _ => """
<main>
  <article>
    <h1>{{ page.title }}</h1>
    {{ if page.summary }}<p>{{ page.summary }}</p>{{ end }}
    <div class="content">{{ page.content }}</div>
  </article>
</main>
"""
        };
    }

    private static string WriteHomeTemplateBody()
    {
        return """
<main>
  <h1>{{ page.title }}</h1>
  {{ if page.content }}
  <div class="content">{{ page.content }}</div>
  {{ end }}
  {{ for p in pages }}
  <article>
    <h2><a href="{{ p.url }}">{{ p.title }}</a></h2>
    {{ if p.summary }}<p>{{ p.summary }}</p>{{ end }}
  </article>
  {{ end }}
</main>
""";
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

    private static void WriteThemeYaml(string themeDir, string themeName, HashSet<PageType> pageTypes,
        string? postListName, string? companyListName, string? serviceListName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"name: {JsonSerializer.Serialize(themeName)}");
        sb.AppendLine("version: 1.0.0");
        sb.AppendLine("engine: bukit");
        sb.AppendLine("description: Theme generated from HTML import");
        sb.AppendLine("templates:");
        sb.AppendLine("  home:");
        sb.AppendLine("    template: pages/index.html");
        sb.AppendLine("    required: true");

        if (pageTypes.Contains(PageType.Page))
        {
            sb.AppendLine("  page:");
            sb.AppendLine("    template: pages/page.html");
            sb.AppendLine("    accepts:");
            sb.AppendLine("      collection: page");
        }

        if (pageTypes.Contains(PageType.PostDetail))
        {
            sb.AppendLine("  post:");
            sb.AppendLine("    template: pages/post.html");
            sb.AppendLine("    accepts:");
            sb.AppendLine("      collection: post");
        }

        if (pageTypes.Contains(PageType.PostList))
        {
            var listName = postListName ?? "insights";
            sb.AppendLine("  list:");
            sb.AppendLine($"    template: pages/{listName}.html");
            sb.AppendLine("    accepts:");
            sb.AppendLine("      kind: list");
        }

        if (pageTypes.Contains(PageType.CompanyDetail) || pageTypes.Contains(PageType.CompanyList))
        {
            sb.AppendLine("  company:");
            sb.AppendLine("    template: pages/company.html");
            sb.AppendLine("    accepts:");
            sb.AppendLine("      collection: company");
            if (pageTypes.Contains(PageType.CompanyList))
            {
                sb.AppendLine("  company-list:");
                sb.AppendLine($"    template: pages/{companyListName ?? "companies"}.html");
                sb.AppendLine("    accepts:");
                sb.AppendLine("      kind: list");
            }
        }

        if (pageTypes.Contains(PageType.ServiceDetail) || pageTypes.Contains(PageType.ServiceList))
        {
            sb.AppendLine("  service:");
            sb.AppendLine("    template: pages/service.html");
            sb.AppendLine("    accepts:");
            sb.AppendLine("      collection: service");
            if (pageTypes.Contains(PageType.ServiceList))
            {
                sb.AppendLine("  service-list:");
                sb.AppendLine($"    template: pages/{serviceListName ?? "services"}.html");
                sb.AppendLine("    accepts:");
                sb.AppendLine("      kind: list");
            }
        }

        File.WriteAllText(Path.Combine(themeDir, "theme.yaml"), sb.ToString());
    }
}
