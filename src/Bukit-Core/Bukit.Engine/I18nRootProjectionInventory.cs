using Bukit.Config;
using Bukit.Shared;
using System.Text.Json;
using System.Xml.Linq;

namespace Bukit.Engine;

internal static class I18nRootProjectionInventory
{
    internal static IReadOnlyList<PublishRepresentationOutput> BuildOutputs(
        string outputDir,
        PublishRepresentation representation,
        IReadOnlyList<BuildVariantResult> results,
        IReadOnlySet<string>? publishedUrls = null)
    {
        var path = Path.Combine(outputDir, representation.Path);
        var fileExists = File.Exists(path);
        var urls = publishedUrls ?? (fileExists ? ReadUrls(File.ReadAllText(path), representation.Kind) : []);
        var outputs = new List<PublishRepresentationOutput>();
        foreach (var result in results)
        {
            foreach (var (_, seo) in result.SeoIndex.OrderBy(x => x.Value.Route.Url, StringComparer.OrdinalIgnoreCase))
            {
                var url = I18nRootProjectionPath.CombineBaseUrl(result.BaseUrl, seo.Route.Url);
                var routePresent = urls.Contains(url) || urls.Contains(seo.Canonical);
                var exists = representation.Kind.Equals("robots", StringComparison.OrdinalIgnoreCase)
                    ? fileExists
                    : fileExists && seo.Indexable && routePresent;
                outputs.Add(new PublishRepresentationOutput(
                    representation.Kind,
                    url,
                    representation.Path.Replace('\\', '/'),
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
                "/" + representation.Path.Replace('\\', '/'),
                representation.Path.Replace('\\', '/'),
                fileExists,
                Indexable: false)
        ];
    }

    private static HashSet<string> ReadUrls(string text, string kind)
    {
        var urls = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            if (kind is "search" or "jsonfeed" or "agent-manifest")
            {
                using var json = JsonDocument.Parse(text);
                var items = json.RootElement;
                var arrayName = kind == "jsonfeed" ? "items" : "documents";
                if (items.ValueKind != JsonValueKind.Array &&
                    (items.ValueKind != JsonValueKind.Object || !items.TryGetProperty(arrayName, out items)))
                    return urls;
                if (items.ValueKind != JsonValueKind.Array) return urls;
                foreach (var item in items.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object &&
                        item.TryGetProperty(kind == "agent-manifest" ? "route" : "url", out var url) &&
                        url.ValueKind == JsonValueKind.String)
                        urls.Add(url.GetString()!);
                }
            }
            else if (kind is "sitemap" or "feed" or "atom")
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
        catch (Exception exception) when (exception is JsonException or System.Xml.XmlException)
        {
            // Invalid generated artifacts cannot prove that a route was included.
            urls.Clear();
        }
        return urls;
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
