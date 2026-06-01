using System.Text;

namespace Bukit.Importing;

internal static class SiteConfigGenerator
{
    internal static bool Generate(HtmlDemoImportOptions options)
    {
        var siteDir = HtmlDemoImporter.GetSiteDir(options);
        Directory.CreateDirectory(siteDir);
        var yamlPath = Path.Combine(siteDir, "site.yaml");
        if (File.Exists(yamlPath))
        {
            Console.WriteLine("  site.yaml 已存在，跳过生成。建议手动检查。");
            return false;
        }

        var contentDir = Path.GetRelativePath(options.RootDir, Path.Combine(siteDir, "content"))
            .Replace('\\', '/');

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
        sb.AppendLine("content:");
        sb.AppendLine("  provider: markdown");
        sb.AppendLine("  markdown:");
        sb.AppendLine($"    dir: {contentDir}");
        sb.AppendLine("    defaultType: page");
        sb.AppendLine("build:");
        sb.AppendLine("  output: dist");
        sb.AppendLine("  clean: true");
        sb.AppendLine("theme:");
        sb.AppendLine($"  name: {options.ThemeName}");

        File.WriteAllText(yamlPath, sb.ToString());
        return true;
    }
}
