using System.Collections.Concurrent;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class StaticFileService
{
    internal static void RenderStaticFiles(string staticDir, string outputDir, ITemplateRenderer renderer, SiteModel siteModel, string templateName, string baseUrl, ConcurrentDictionary<string, byte> currentKeys, CancellationToken cancellationToken)
    {
        var htmlFiles = Directory.GetFiles(staticDir, "*.html", SearchOption.AllDirectories);
        foreach (var file in htmlFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativeOutputPath = Path.GetRelativePath(staticDir, file);
            var url = "/" + Path.GetDirectoryName(relativeOutputPath)?.Replace('\\', '/').TrimStart('.') + "/";
            url = RoutePathBuilder.NormalizeUrl(url);

            var fileName = Path.GetFileNameWithoutExtension(file);
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
            var key = BuildPathUtils.NormalizeRelPath(relativeOutputPath);
            currentKeys.TryAdd(key, 0);
            FileWriter.WriteUtf8(outputDir, relativeOutputPath, rendered);
        }

        var nonHtmlFiles = Directory.GetFiles(staticDir, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".html", StringComparison.OrdinalIgnoreCase));
        foreach (var file in nonHtmlFiles)
        {
            var relativePath = Path.GetRelativePath(staticDir, file);
            var dest = Path.Combine(outputDir, relativePath);
            var destDir = Path.GetDirectoryName(dest);
            if (destDir is not null)
            {
                Directory.CreateDirectory(destDir);
            }
            File.Copy(file, dest, overwrite: true);
        }
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
