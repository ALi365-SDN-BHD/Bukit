using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
namespace Bukit.Engine;

internal enum RenderEntryKind { Page, List, Static }

internal sealed record RenderEntry(
    RenderEntryKind Kind,
    ContentDocument? Document,
    RouteInfo Route,
    IReadOnlyList<RoutedContentDocument>? SourceDocuments,
    bool IncludeContent,
    IReadOnlyDictionary<string, ContentField>? ListPageFields,
    ListPageContext? ListPageContext,
    string? RawContent,
    string Title,
    ListRoutePlan? MetadataListRoute = null,
    string? SourcePath = null)
{
    internal static RenderEntry ForPage(
        ContentDocument document,
        RouteInfo route,
        ListRoutePlan? metadataListRoute = null) =>
        new(RenderEntryKind.Page, document, route, null, false, null, null, null, document.Title, metadataListRoute);

    internal static RenderEntry ForList(
        RouteInfo listRoute,
        IReadOnlyList<RoutedContentDocument> source,
        bool includeContent,
        IReadOnlyDictionary<string, ContentField>? pageFields = null,
        ListPageContext? pageContext = null) =>
        new(RenderEntryKind.List, null, listRoute, source, includeContent, pageFields, pageContext, null, listRoute.Url);

    internal static IReadOnlyList<RenderEntry> ForStaticDir(string staticDir, string template, Action<string> warn, bool publishDotFiles)
    {
        var entries = new List<RenderEntry>();
        if (!Directory.Exists(staticDir)) return entries;

        var htmlFiles = SafeFileEnumerator.EnumerateFiles(staticDir, "*.html");
        foreach (var file in htmlFiles)
        {
            var relativeOutputPath = Path.GetRelativePath(staticDir, file);
            if (StaticFilePathPolicy.HasSensitiveSegment(relativeOutputPath))
            {
                warn($"Skipping sensitive dotfile in static dir: {relativeOutputPath}");
                continue;
            }

            if (!publishDotFiles && StaticFilePathPolicy.HasDisallowedDotPrefixedSegment(relativeOutputPath))
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
            entries.Add(new RenderEntry(
                RenderEntryKind.Static,
                null,
                route,
                null,
                false,
                null,
                null,
                rawContent,
                title,
                SourcePath: file));
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

}
