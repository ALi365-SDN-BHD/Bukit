using System.Text;

namespace Bukit.Importing;

internal static class SiteConfigGenerator
{
    internal static bool Generate(HtmlDemoImportOptions options, RouteMapConfig? routeMap = null)
    {
        var siteDir = HtmlDemoImporter.GetSiteDir(options);
        Directory.CreateDirectory(siteDir);
        var yamlPath = Path.Combine(siteDir, "site.yaml");
        if (File.Exists(yamlPath))
        {
            Console.WriteLine("  site.yaml 已存在，跳过生成。建议手动检查。");
            return false;
        }

        var contentPath = Path.Combine(siteDir, "content");
        var contentDir = Path.GetRelativePath(options.RootDir, contentPath)
            .Replace('\\', '/');
        if (contentDir.Equals("..", StringComparison.Ordinal) ||
            contentDir.StartsWith("../", StringComparison.Ordinal))
        {
            contentDir = "content";
        }

        var sb = new StringBuilder();
        sb.AppendLine("site:");
        sb.AppendLine($"  name: {options.ThemeName}");
        sb.AppendLine($"  title: {options.ThemeName}");
        sb.AppendLine("  url: https://example.com");
        sb.AppendLine("  description: Generated from HTML Demo");
        var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? "/" : options.BaseUrl;
        sb.AppendLine($"  baseUrl: {baseUrl}");
        sb.AppendLine($"  language: {options.Language}");
        sb.AppendLine("  seo:");
        sb.AppendLine("    renderMode: inject");
        sb.AppendLine("  collections:");
        sb.AppendLine("    page:");
        sb.AppendLine("      permalink: '/{slug}/'");
        sb.AppendLine("      template: 'pages/page.html'");
        sb.AppendLine("    post:");
        sb.AppendLine("      permalink: '/insights/{slug}/'");
        sb.AppendLine("      template: 'pages/article.html'");
        sb.AppendLine("      listRoute: '/insights/'");
        sb.AppendLine("      listTemplate: 'pages/insights.html'");
        sb.AppendLine("    company:");
        sb.AppendLine("      permalink: '/companies/{slug}/'");
        sb.AppendLine("      template: 'pages/company.html'");
        sb.AppendLine("      listRoute: '/companies/'");
        sb.AppendLine("      listTemplate: 'pages/companies.html'");
        sb.AppendLine("    service:");
        sb.AppendLine("      permalink: '/services/{slug}/'");
        sb.AppendLine("      template: 'pages/service.html'");
        sb.AppendLine("      listRoute: '/services/'");
        sb.AppendLine("      listTemplate: 'pages/services.html'");

        if (routeMap != null)
        {
            var appPages = routeMap.Pages
                .Where(p => !string.IsNullOrWhiteSpace(p.Route) &&
                            !string.IsNullOrWhiteSpace(p.Template) &&
                            !IsDefaultPageRoute(p.Route, p.Template))
                .ToList();
            if (appPages.Count > 0)
            {
                sb.AppendLine("  appPages:");
                foreach (var page in appPages)
                {
                    var slug = !string.IsNullOrWhiteSpace(page.Slug)
                        ? page.Slug
                        : SanitizeAppPageSlug(page.Source);
                    sb.AppendLine($"    {slug}:");
                    sb.AppendLine($"      route: {page.Route}");
                    sb.AppendLine($"      template: pages/{page.Template}.html");
                    if (!string.IsNullOrWhiteSpace(page.Description))
                        sb.AppendLine($"      description: {page.Description}");
                }
            }
        }

        if (options.ContentSource.Equals("notion", StringComparison.OrdinalIgnoreCase) &&
            options.BuildSource.Equals("notion", StringComparison.OrdinalIgnoreCase))
        {
            var dbId = !string.IsNullOrWhiteSpace(options.NotionDatabaseId)
                ? options.NotionDatabaseId
                : "${NOTION_DATABASE_ID}";
            var tokenEnv = !string.IsNullOrWhiteSpace(options.NotionTokenEnv)
                ? options.NotionTokenEnv
                : "NOTION_TOKEN";

            sb.AppendLine("content:");
            sb.AppendLine("  provider: notion");
            sb.AppendLine("  notion:");
            sb.AppendLine($"    databaseId: {dbId}");
            sb.AppendLine($"    tokenEnv: {tokenEnv}");
            sb.AppendLine("    filterProperty: Published");
            sb.AppendLine("    filterType: checkbox_true");
            sb.AppendLine("    sortProperty: Title");
            sb.AppendLine("    sortDirection: ascending");
        }
        else
        {
            sb.AppendLine("content:");
            sb.AppendLine("  provider: markdown");
            sb.AppendLine("  markdown:");
            sb.AppendLine($"    dir: {contentDir}");
            sb.AppendLine("    defaultType: page");
        }
        sb.AppendLine("build:");
        sb.AppendLine("  output: dist");
        sb.AppendLine("  clean: true");
        sb.AppendLine("theme:");
        sb.AppendLine($"  name: {options.ThemeName}");

        File.WriteAllText(yamlPath, sb.ToString());
        return true;
    }

    private static bool IsDefaultPageRoute(string route, string template)
    {
        route = route.Trim('/');
        return (route, template.ToLowerInvariant()) switch
        {
            ("" or "index", "index") => true,
            ("insights", "insights") => true,
            ("companies", "companies") => true,
            ("services", "services") => true,
            _ => false
        };
    }

    private static string SanitizeAppPageSlug(string source)
    {
        var name = Path.GetFileNameWithoutExtension(source);
        var chars = name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray();
        var result = new string(chars);
        while (result.Contains("--"))
            result = result.Replace("--", "-");
        return result.Trim('-').ToLowerInvariant();
    }
}
