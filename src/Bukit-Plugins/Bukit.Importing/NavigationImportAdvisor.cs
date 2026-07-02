using System.Text.RegularExpressions;

namespace Bukit.Importing;

internal static partial class NavigationImportAdvisor
{
    internal static void AddMissingNavigationWarnings(
        IReadOnlyList<DiscoveredPage> pages,
        ExtractedContent content,
        List<string> warnings)
    {
        if (content.Navigation.Count > 0)
            return;

        var suspiciousPages = pages
            .Where(HasNavigationShellWithoutStaticLinks)
            .Select(p => p.RelativePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        if (suspiciousPages.Count == 0)
            return;

        warnings.Add(
            "未提取到菜单项，但检测到 header/menu/hamburger 等菜单容器迹象。import 不执行 JS 动态生成菜单；请提供包含静态 <a href> 的菜单锚点、静态 HTML 片段，或先导出浏览器渲染后的 HTML。Affected: " +
            string.Join(", ", suspiciousPages));
    }

    private static bool HasNavigationShellWithoutStaticLinks(DiscoveredPage page)
    {
        var html = StripScriptContent($"{page.BodyOpening}\n{page.BodyContent}\n{page.BodyClosing}");
        if (string.IsNullOrWhiteSpace(html))
            return false;
        if (NavigationMarkupExtractor.ExtractBest(html) is not null)
            return false;

        return HeaderOrMenuRegex().IsMatch(html) &&
               (MenuTokenRegex().IsMatch(html) || HamburgerTokenRegex().IsMatch(html)) &&
               NavigationMarkupExtractor.ExtractLinks(html).Count() < 2;
    }

    private static string StripScriptContent(string html)
        => ScriptBlockRegex().Replace(html, "");

    [GeneratedRegex(@"<(?:header|div|ul|ol|button)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex HeaderOrMenuRegex();

    [GeneratedRegex(@"\b(?:menu|nav|navbar|navigation|drawer|offcanvas|mobile-menu|site-menu|main-menu)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MenuTokenRegex();

    [GeneratedRegex(@"\b(?:hamburger|burger|menu-toggle|navbar-toggler|drawer-toggle)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HamburgerTokenRegex();

    [GeneratedRegex(@"<script\b[^>]*>.*?</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptBlockRegex();
}
