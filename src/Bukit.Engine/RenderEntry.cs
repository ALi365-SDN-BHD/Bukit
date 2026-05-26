using Bukit.Content;
using Bukit.Routing;

namespace Bukit.Engine;

internal enum RenderEntryKind { Page, List, Static }

internal sealed record RenderEntry(
    RenderEntryKind Kind,
    ContentItem? Item,
    RouteInfo Route,
    IReadOnlyList<(ContentItem Item, RouteInfo Route)>? SourceItems,
    bool IncludeContent,
    string? RawContent,
    string Title)
{
    internal static RenderEntry ForPage(ContentItem item, RouteInfo route) =>
        new(RenderEntryKind.Page, item, route, null, false, null, item.Title);

    internal static RenderEntry ForList(RouteInfo listRoute, IReadOnlyList<(ContentItem Item, RouteInfo Route)> source, bool includeContent) =>
        new(RenderEntryKind.List, null, listRoute, source, includeContent, null, listRoute.Url);

    internal static IReadOnlyList<RenderEntry> ForStaticDir(string staticDir, string template, Action<string> warn, bool publishDotFiles)
    {
        var entries = new List<RenderEntry>();
        if (!Directory.Exists(staticDir)) return entries;

        var htmlFiles = Directory.GetFiles(staticDir, "*.html", SearchOption.AllDirectories);
        foreach (var file in htmlFiles)
        {
            var relativeOutputPath = Path.GetRelativePath(staticDir, file);
            if (!publishDotFiles && HasDotPrefixedSegment(relativeOutputPath))
            {
                warn($"Skipping dotfile in static dir: {relativeOutputPath}");
                continue;
            }

            var url = BuildUrlFromStaticHtmlPath(relativeOutputPath);
            var outputPath = BuildOutputPathFromStaticHtmlPath(relativeOutputPath);
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrEmpty(fileName))
            {
                warn($"Skipping invalid static HTML file: {relativeOutputPath}");
                continue;
            }

            var title = fileName.Equals("index", StringComparison.OrdinalIgnoreCase)
                ? BuildListTitleFromUrl(url)
                : char.ToUpperInvariant(fileName[0]) + fileName[1..].Replace('-', ' ');

            var route = new RouteInfo(url, outputPath, template);
            var rawContent = File.ReadAllText(file);
            entries.Add(new RenderEntry(RenderEntryKind.Static, null, route, null, false, rawContent, title));
        }

        return entries;
    }

    private static string BuildUrlFromStaticHtmlPath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (normalized.Equals("index.html", StringComparison.OrdinalIgnoreCase))
            return "/";
        if (normalized.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase))
            return "/" + normalized[..^"index.html".Length];
        return "/" + normalized[..^".html".Length] + "/";
    }

    private static string BuildOutputPathFromStaticHtmlPath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (normalized.Equals("index.html", StringComparison.OrdinalIgnoreCase))
            return "index.html";
        if (normalized.EndsWith("index.html", StringComparison.OrdinalIgnoreCase))
            return normalized;
        return normalized[..^".html".Length] + "/index.html";
    }

    private static string BuildListTitleFromUrl(string url)
    {
        var lastSegment = (url ?? string.Empty)
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();
        if (string.IsNullOrWhiteSpace(lastSegment)) return "Index";
        return char.ToUpperInvariant(lastSegment[0]) + lastSegment[1..].Replace('-', ' ');
    }

    private static bool HasDotPrefixedSegment(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (normalized.StartsWith(".well-known/", StringComparison.OrdinalIgnoreCase) || normalized.Equals(".well-known", StringComparison.OrdinalIgnoreCase))
            return false;
        foreach (var segment in normalized.Split('/'))
        {
            if (segment.StartsWith('.') && !segment.Equals(".well-known", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
