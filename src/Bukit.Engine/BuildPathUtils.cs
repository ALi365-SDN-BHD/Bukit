using System.Text;
using Bukit.Config;
using Bukit.Content;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class BuildPathUtils
{
    internal static string MakeAbsolute(string rootDir, string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.GetFullPath(Path.Combine(rootDir, path));
    }

    internal static string NormalizeBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return "/";
        }

        var trimmed = baseUrl.Trim();
        if (!trimmed.StartsWith('/'))
        {
            trimmed = "/" + trimmed;
        }

        if (trimmed.Length > 1 && trimmed.EndsWith('/'))
        {
            trimmed = trimmed.TrimEnd('/');
        }

        return trimmed;
    }

    internal static string NormalizeRelPath(string path)
    {
        return path.Replace('\\', '/');
    }

    internal static string SanitizeFileSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "default";
        }

        var chars = value.Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }

    internal static string EscapeHtml(string text)
    {
        return text
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
    }

    internal static (string LayoutsDir, string AssetsDir, string StaticDir, string? ParentLayoutsDir, string? ParentAssetsDir, string? ParentStaticDir, string? UserLayoutsDir) ResolveThemeDirectories(string rootDir, ThemeConfig theme, string? resolvedThemeRoot = null)
    {
        var (childLayouts, childAssets, childStatic) = ResolveThemeDirInternal(rootDir, theme, resolvedThemeRoot);

        var userLayoutsDir = Path.Combine(rootDir, "layouts");
        if (!Directory.Exists(userLayoutsDir))
        {
            userLayoutsDir = null;
        }

        if (!string.IsNullOrWhiteSpace(theme.Extends))
        {
            var parentTheme = new ThemeConfig { Name = theme.Extends };
            var (parentLayouts, parentAssets, parentStatic) = ResolveThemeDirInternal(rootDir, parentTheme);
            return (childLayouts, childAssets, childStatic, parentLayouts, parentAssets, parentStatic, userLayoutsDir);
        }

        return (childLayouts, childAssets, childStatic, null, null, null, userLayoutsDir);
    }

    private static (string LayoutsDir, string AssetsDir, string StaticDir) ResolveThemeDirInternal(string rootDir, ThemeConfig theme, string? resolvedThemeRoot = null)
    {
        if (string.IsNullOrWhiteSpace(theme.Name) && string.IsNullOrWhiteSpace(resolvedThemeRoot))
        {
            return (
                MakeAbsolute(rootDir, theme.Layouts),
                MakeAbsolute(rootDir, theme.Assets),
                MakeAbsolute(rootDir, theme.Static)
            );
        }

        var themeRoot = string.IsNullOrWhiteSpace(resolvedThemeRoot)
            ? Path.Combine(rootDir, "themes", theme.Name!)
            : resolvedThemeRoot;

        var layouts = string.Equals(theme.Layouts, "layouts", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(themeRoot, "layouts")
            : MakeAbsolute(rootDir, theme.Layouts);

        var assets = string.Equals(theme.Assets, "assets", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(themeRoot, "assets")
            : MakeAbsolute(rootDir, theme.Assets);

        var stat = string.Equals(theme.Static, "static", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(themeRoot, "static")
            : MakeAbsolute(rootDir, theme.Static);

        return (layouts, assets, stat);
    }

    internal static void WarnIfWindowsIncompatible(string outputPath, HashSet<string> warned, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        var normalized = outputPath.Replace('\\', '/');
        if (!warned.Add(normalized))
        {
            return;
        }

        if (!TryGetWindowsPathIssue(normalized, out var issue))
        {
            return;
        }

        logger.Warn($"windows path warning: outputPath '{normalized}' {issue}");
    }

    internal static bool TryGetWindowsPathIssue(string outputPath, out string issue)
    {
        issue = string.Empty;

        var segments = outputPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            issue = "is empty.";
            return true;
        }

        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                issue = "contains an empty path segment.";
                return true;
            }

            if (segment.EndsWith(' ') || segment.EndsWith('.'))
            {
                issue = $"has a segment that ends with a space or dot: '{segment}'.";
                return true;
            }

            foreach (var ch in segment)
            {
                if (ch < 32 || ch is '<' or '>' or ':' or '\"' or '|' or '?' or '*')
                {
                    issue = $"contains an invalid Windows character in segment '{segment}'.";
                    return true;
                }
            }

            var parts = segment.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                issue = $"contains an invalid segment '{segment}'.";
                return true;
            }

            var baseName = parts[0];
            if (IsWindowsDeviceName(baseName))
            {
                issue = $"uses a reserved Windows device name: '{baseName}'.";
                return true;
            }
        }

        return false;
    }

    internal static string RenderSimplePage(string baseUrl, string title, string url, string contentHtml)
    {
        var cssHref = baseUrl == "/" ? "/assets/style.css" : $"{baseUrl}/assets/style.css";
        var canonical = baseUrl == "/" ? url : $"{baseUrl}{url}";

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"zh-CN\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        sb.AppendLine($"  <title>{EscapeHtml(title)}</title>");
        sb.AppendLine($"  <link rel=\"stylesheet\" href=\"{cssHref}\" />");
        sb.AppendLine($"  <link rel=\"canonical\" href=\"{canonical}\" />");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <main class=\"container\">");
        sb.AppendLine($"    <h1>{EscapeHtml(title)}</h1>");
        sb.AppendLine("    <div class=\"content\">");
        sb.AppendLine(contentHtml);
        sb.AppendLine("    </div>");
        sb.AppendLine("  </main>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    internal static string RenderSimpleIndex(string baseUrl, IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed, string title = "Bukit")
    {
        var cssHref = baseUrl == "/" ? "/assets/style.css" : $"{baseUrl}/assets/style.css";

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"zh-CN\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        sb.AppendLine($"  <title>{EscapeHtml(title)}</title>");
        sb.AppendLine($"  <link rel=\"stylesheet\" href=\"{cssHref}\" />");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <main class=\"container\">");
        sb.AppendLine($"    <h1>{EscapeHtml(title)}</h1>");
        sb.AppendLine("    <ul>");

        foreach (var (item, route) in routed)
        {
            var href = baseUrl == "/" ? route.Url : $"{baseUrl}{route.Url}";
            sb.AppendLine($"      <li><a href=\"{href}\">{EscapeHtml(item.Title)}</a></li>");
        }

        sb.AppendLine("    </ul>");
        sb.AppendLine("  </main>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static bool IsWindowsDeviceName(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return false;
        }

        var name = segment.Trim().ToLowerInvariant();
        if (name is "con" or "prn" or "aux" or "nul")
        {
            return true;
        }

        if (name.Length == 4 && name.StartsWith("com") && char.IsDigit(name[3]))
        {
            return true;
        }

        if (name.Length == 4 && name.StartsWith("lpt") && char.IsDigit(name[3]))
        {
            return true;
        }

        return false;
    }
}
