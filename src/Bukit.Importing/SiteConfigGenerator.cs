using System.Text;

namespace Bukit.Importing;

internal static class SiteConfigGenerator
{
    internal static bool Generate(HtmlDemoImportOptions options)
    {
        var yamlPath = Path.Combine(options.RootDir, "site.yaml");
        if (File.Exists(yamlPath))
        {
            Console.WriteLine("  site.yaml 已存在，跳过生成。建议手动检查。");
            return false;
        }

        var sb = new StringBuilder();
        sb.AppendLine("site:");
        sb.AppendLine($"  name: {options.ThemeName}");
        sb.AppendLine($"  title: {options.ThemeName}");
        var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? "/" : options.BaseUrl;
        sb.AppendLine($"  baseUrl: {baseUrl}");
        sb.AppendLine($"  language: {options.Language}");
        sb.AppendLine("  seo:");
        sb.AppendLine("    renderMode: 'off'");
        sb.AppendLine("  collections:");
        sb.AppendLine("    page:");
        sb.AppendLine("      permalink: '/{slug}/'");
        sb.AppendLine("      template: 'pages/page.html'");
        sb.AppendLine("      listRoute: '/'");
        sb.AppendLine("content:");
        sb.AppendLine("  provider: markdown");
        sb.AppendLine("  contentDir: content");
        sb.AppendLine("theme:");
        sb.AppendLine($"  name: {options.ThemeName}");

        File.WriteAllText(yamlPath, sb.ToString());
        return true;
    }
}
