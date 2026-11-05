using Bukit.Engine.Abstractions.Routing;

namespace Bukit.Engine;

/// <summary>Output paths and URLs of the per-document content projections (JSON and Markdown).</summary>
internal static class ContentProjectionPaths
{
    internal static string GetContentProjectionRelativePath(RouteInfo route, string extension = "")
        => "content/" + BuildPathUtils.NormalizeRelPath(route.OutputPath) + extension;

    internal static string GetContentProjectionBasePath(string outputDir, RouteInfo route)
        => FileWriter.GetSafeFullPath(outputDir, GetContentProjectionRelativePath(route));

    internal static string GetContentProjectionUrl(RouteInfo route, string extension, string baseUrl = "/")
        => GetPublicOutputUrl(GetContentProjectionRelativePath(route, extension), baseUrl);

    /// <summary>The page's Markdown and JSON version URLs, keyed as templates see them.</summary>
    internal static IReadOnlyDictionary<string, string> GetRepresentationUrls(RouteInfo route, string baseUrl = "/")
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["markdown"] = GetContentProjectionUrl(route, ".md", baseUrl),
            ["json"] = GetContentProjectionUrl(route, ".json", baseUrl)
        };

    internal static string GetPublicOutputUrl(string relativePath, string baseUrl)
        => BuildPathUtils.NormalizeBaseUrl(baseUrl).TrimEnd('/') + "/" +
           string.Join("/", relativePath.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));
}
