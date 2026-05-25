using System.Collections.Concurrent;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class StaticFileService
{
    internal static void RenderStaticFiles(string staticDir, string outputDir, ITemplateRenderer renderer, SiteModel siteModel, string templateName, string baseUrl, ConcurrentDictionary<string, byte> currentKeys, CancellationToken cancellationToken, Action<string>? warn = null, bool publishDotFiles = false)
    {
        var htmlFiles = Directory.GetFiles(staticDir, "*.html", SearchOption.AllDirectories);
        foreach (var file in htmlFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativeOutputPath = Path.GetRelativePath(staticDir, file);
            var url = BuildUrlFromStaticHtmlPath(relativeOutputPath);
            var outputPath = BuildOutputPathFromStaticHtmlPath(relativeOutputPath);

            var fileName = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrEmpty(fileName))
            {
                warn?.Invoke($"Skipping invalid static HTML file: {relativeOutputPath}");
                continue;
            }

            var title = fileName.Equals("index", StringComparison.OrdinalIgnoreCase)
                ? BuildListTitleFromUrl(url)
                : char.ToUpperInvariant(fileName[0]) + fileName[1..].Replace('-', ' ');

            var htmlContent = File.ReadAllText(file);

            var pageInfo = new PageInfo
            {
                Title = title,
                Url = url,
                Content = htmlContent,
                Summary = siteModel.Description
            };

            var pageModel = new PageModel
            {
                Site = siteModel,
                Page = pageInfo
            };

            var rendered = renderer.RenderPage(templateName, pageModel);
            var key = BuildPathUtils.NormalizeRelPath(outputPath);
            currentKeys.TryAdd(key, 0);
            FileWriter.WriteUtf8(outputDir, outputPath, rendered);
        }

        var nonHtmlFiles = Directory.GetFiles(staticDir, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".html", StringComparison.OrdinalIgnoreCase));
        foreach (var file in nonHtmlFiles)
        {
            var relativePath = Path.GetRelativePath(staticDir, file);

            if (!publishDotFiles && HasDotPrefixedSegment(relativePath))
            {
                warn?.Invoke($"Skipping dotfile in static dir: {relativePath}");
                continue;
            }

            var dest = FileWriter.GetSafeFullPath(outputDir, relativePath);
            var destDir = Path.GetDirectoryName(dest);
            if (destDir is not null)
            {
                Directory.CreateDirectory(destDir);
            }
            File.Copy(file, dest, overwrite: true);
        }
    }

    private static bool HasDotPrefixedSegment(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (normalized.StartsWith(".well-known/", StringComparison.OrdinalIgnoreCase) || normalized.Equals(".well-known", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var segment in normalized.Split('/'))
        {
            if (segment.StartsWith('.') && !segment.Equals(".well-known", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static IReadOnlyList<RouteInfo> BuildStaticHtmlRoutes(string staticDir, string templateName, Action<string>? warn = null)
    {
        if (!Directory.Exists(staticDir))
        {
            return Array.Empty<RouteInfo>();
        }

        var routes = new List<RouteInfo>();
        foreach (var file in Directory.GetFiles(staticDir, "*.html", SearchOption.AllDirectories))
        {
            var relativeOutputPath = BuildPathUtils.NormalizeRelPath(Path.GetRelativePath(staticDir, file));
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (string.IsNullOrEmpty(fileName))
            {
                warn?.Invoke($"Skipping invalid static HTML file: {relativeOutputPath}");
                continue;
            }

            routes.Add(new RouteInfo(
                BuildUrlFromStaticHtmlPath(relativeOutputPath),
                BuildOutputPathFromStaticHtmlPath(relativeOutputPath),
                templateName));
        }

        return routes;
    }

    internal static string BuildOutputPathFromStaticHtmlPath(string relativeOutputPath)
        => RoutePathBuilder.BuildOutputPathFromUrl(BuildUrlFromStaticHtmlPath(relativeOutputPath));

    internal static string BuildUrlFromStaticHtmlPath(string relativeOutputPath)
    {
        var normalizedPath = BuildPathUtils.NormalizeRelPath(relativeOutputPath);
        var pathWithoutExtension = normalizedPath.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? normalizedPath[..^5]
            : normalizedPath;
        var urlPath = pathWithoutExtension.Equals("index", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : pathWithoutExtension.EndsWith("/index", StringComparison.OrdinalIgnoreCase)
                ? pathWithoutExtension[..^6]
                : pathWithoutExtension;
        return string.IsNullOrEmpty(urlPath)
            ? "/"
            : RoutePathBuilder.NormalizeUrl("/" + urlPath.Trim('/') + "/");
    }

    private static string BuildListTitleFromUrl(string url)
    {
        var lastSegment = (url ?? string.Empty)
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();

        if (string.IsNullOrWhiteSpace(lastSegment))
        {
            return "Home";
        }

        return char.ToUpperInvariant(lastSegment[0]) + lastSegment[1..].Replace('-', ' ');
    }
}
