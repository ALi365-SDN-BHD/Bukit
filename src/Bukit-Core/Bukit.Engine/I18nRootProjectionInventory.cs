using Bukit.Config;
using Bukit.Shared;
using System.Text.Json;
using System.Xml.Linq;

namespace Bukit.Engine;

internal static class I18nRootProjectionInventory
{
    /// <param name="languagePath">
    /// Set when each language writes its own copy of the artifact and no root file merges them: maps a
    /// language to its file, relative to the output root. Each language's routes are then checked
    /// against that file instead of the root file, and that file is the reported path.
    /// </param>
    /// <param name="reportRootPath">
    /// Reports the representation's root path even when <paramref name="languagePath"/> is set: in index
    /// modes the root file is the search index index or sitemap index that leads to each language's file.
    /// </param>
    internal static IReadOnlyList<PublishRepresentationOutput> BuildOutputs(
        string outputDir,
        PublishRepresentation representation,
        IReadOnlyList<BuildVariantResult> results,
        IReadOnlySet<string>? publishedUrls = null,
        Func<string, string>? languagePath = null,
        bool reportRootPath = false)
    {
        var rootPath = representation.Path.Replace('\\', '/');
        var artifacts = new Dictionary<string, ArtifactUrls>(StringComparer.Ordinal);
        var outputs = new List<PublishRepresentationOutput>();
        foreach (var result in results)
        {
            var relativePath = languagePath is null
                ? rootPath
                : BuildPathUtils.NormalizeRelPath(languagePath(result.Language));
            if (!artifacts.TryGetValue(relativePath, out var artifact))
            {
                artifact = ArtifactUrls.Read(Path.Combine(outputDir, relativePath), representation.Kind,
                    languagePath is null ? publishedUrls : null);
                artifacts[relativePath] = artifact;
            }

            foreach (var (_, seo) in result.SeoIndex.OrderBy(x => x.Value.Route.Url, StringComparer.OrdinalIgnoreCase))
            {
                var url = I18nRootProjectionPath.CombineBaseUrl(result.BaseUrl, seo.Route.Url);
                var exists = representation.Kind.Equals("robots", StringComparison.OrdinalIgnoreCase)
                    ? artifact.FileExists
                    : artifact.FileExists && seo.Indexable && artifact.Contains(url, seo.Canonical);
                outputs.Add(new PublishRepresentationOutput(
                    representation.Kind,
                    url,
                    reportRootPath ? rootPath : relativePath,
                    exists,
                    seo.Indexable));
            }
        }

        if (outputs.Count > 0)
        {
            return outputs;
        }

        return
        [
            new PublishRepresentationOutput(
                representation.Kind,
                "/" + rootPath,
                rootPath,
                File.Exists(Path.Combine(outputDir, representation.Path)),
                Indexable: false)
        ];
    }

    private sealed record ArtifactUrls(bool FileExists, IReadOnlySet<string> Urls, HashSet<string>? SitemapLocKeys)
    {
        internal static ArtifactUrls Read(string path, string kind, IReadOnlySet<string>? publishedUrls)
        {
            var fileExists = File.Exists(path);
            var urls = publishedUrls ?? (fileExists ? ReadUrls(File.ReadAllText(path), kind) : []);
            // Sitemap writers dedupe canonicals by normalized origin, so compare by that identity too.
            var sitemapLocKeys = kind.Equals("sitemap", StringComparison.OrdinalIgnoreCase)
                ? urls.Select(SitemapLocFilter.CanonicalKey).ToHashSet(StringComparer.Ordinal)
                : null;
            return new ArtifactUrls(fileExists, urls, sitemapLocKeys);
        }

        internal bool Contains(string url, string canonical)
            => Urls.Contains(url) || Urls.Contains(canonical) ||
               (SitemapLocKeys is not null && SitemapCanonicalIdentity.Contains(SitemapLocKeys, canonical));
    }

    private static HashSet<string> ReadUrls(string text, string kind)
    {
        var urls = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            if (kind is "search" or "jsonfeed" or "agent-manifest")
            {
                ReadJsonUrls(text, kind, urls);
            }
            else if (kind is "sitemap" or "feed" or "atom")
            {
                ReadXmlUrls(text, kind, urls);
            }
        }
        catch (Exception exception) when (exception is JsonException or System.Xml.XmlException)
        {
            // Invalid generated artifacts cannot prove that a route was included.
            urls.Clear();
        }
        return urls;
    }

    private static void ReadJsonUrls(string text, string kind, HashSet<string> urls)
    {
        using var json = JsonDocument.Parse(text);
        var items = json.RootElement;
        var arrayName = kind == "jsonfeed" ? "items" : "documents";
        if (items.ValueKind != JsonValueKind.Array &&
            (items.ValueKind != JsonValueKind.Object || !items.TryGetProperty(arrayName, out items)))
            return;
        if (items.ValueKind != JsonValueKind.Array) return;
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty(kind == "agent-manifest" ? "route" : "url", out var url) &&
                url.ValueKind == JsonValueKind.String)
                urls.Add(url.GetString()!);
        }
    }

    private static void ReadXmlUrls(string text, string kind, HashSet<string> urls)
    {
        var root = XDocument.Parse(text).Root;
        var container = kind == "feed" ? root?.Elements().FirstOrDefault(element => element.Name.LocalName == "channel") : root;
        var entryName = kind switch { "sitemap" => "url", "feed" => "item", _ => "entry" };
        foreach (var entry in container?.Elements().Where(element => element.Name.LocalName == entryName) ?? [])
        {
            foreach (var link in entry.Elements().Where(element => element.Name.LocalName == (kind == "sitemap" ? "loc" : "link")))
            {
                if (kind == "atom" && link.Attribute("rel")?.Value is not (null or "alternate")) continue;
                var url = kind == "atom" ? link.Attribute("href")?.Value : link.Value;
                if (!string.IsNullOrWhiteSpace(url)) urls.Add(url.Trim());
            }
        }
    }
}

internal static class I18nRootProjectionPath
{
    internal static string CombineBaseUrl(string baseUrl, string routeUrl)
    {
        var normalizedBaseUrl = BuildPathUtils.NormalizeBaseUrl(baseUrl).TrimEnd('/');
        var normalizedRouteUrl = routeUrl.StartsWith('/') ? routeUrl : "/" + routeUrl;
        return string.IsNullOrWhiteSpace(normalizedBaseUrl)
            ? normalizedRouteUrl
            : normalizedBaseUrl + normalizedRouteUrl;
    }
}
