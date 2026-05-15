using System.Net;
using System.Text;
using System.Text.Json;
using Bukit.Content;
using Bukit.Routing;

namespace Bukit.Engine;

internal static class SearchIndexBuilder
{
    internal static void GenerateMergedSearchIndex(
        string outputDir,
        IReadOnlyList<BuildVariantResult> results,
        bool includeDerived)
    {
        var outPath = Path.Combine(outputDir, "search.json");
        Directory.CreateDirectory(outputDir);

        using var stream = File.Create(outPath);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });

        writer.WriteStartArray();

        foreach (var r in results)
        {
            var itemsByPath = BuildItemMap(includeDerived ? r.Routed.Concat(r.DerivedRouted) : r.Routed);
            foreach (var (key, seo) in r.SeoIndex
                         .Where(x => x.Value.Indexable)
                         .OrderBy(x => x.Value.Route.Url, StringComparer.OrdinalIgnoreCase))
            {
                if (!itemsByPath.TryGetValue(key, out var item))
                {
                    continue;
                }

                WriteSearchItem(writer, item, seo.Route, r.BaseUrl, r.BodyStore, r.SearchSnippetsEnabled);
            }
        }

        writer.WriteEndArray();
        writer.Flush();
    }

    internal static void WriteSearchItem(
        Utf8JsonWriter writer,
        ContentItem item,
        RouteInfo route,
        string baseUrl,
        IContentBodyStore bodyStore,
        bool emitSnippet)
    {
        writer.WriteStartObject();
        writer.WriteString("id", item.Id);
        writer.WriteString("title", item.Title);
        writer.WriteString("url", NormalizeSearchUrl(baseUrl, route.Url));

        if (item.Meta.TryGetValue("summary", out var summary) && summary is not null)
        {
            writer.WriteString("summary", summary.ToString());
        }

        var text = StripHtmlToText(ContentBodyResolver.GetHtml(item, bodyStore));
        if (text.Length > 8000)
        {
            text = text[..8000];
        }

        writer.WriteString("content", text);
        if (emitSnippet)
        {
            writer.WriteString("snippet", BuildSnippet(item, text));
        }
        writer.WriteString("type", MetaHelpers.GetString(item.Meta, "type"));

        var tags = MetaHelpers.GetStringList(item.Meta, "tags");
        if (tags is not null)
        {
            writer.WriteStartArray("tags");
            foreach (var t in tags)
            {
                writer.WriteStringValue(t);
            }

            writer.WriteEndArray();
        }

        var categories = MetaHelpers.GetStringList(item.Meta, "categories");
        if (categories is not null)
        {
            writer.WriteStartArray("categories");
            foreach (var c in categories)
            {
                writer.WriteStringValue(c);
            }

            writer.WriteEndArray();
        }

        writer.WriteString("language", MetaHelpers.GetString(item.Meta, "language"));
        writer.WriteString("sourceKey", MetaHelpers.GetString(item.Meta, "sourceKey") ?? MetaHelpers.GetString(item.Meta, "source"));
        writer.WriteString("publishAt", item.PublishAt.ToString("O"));
        writer.WriteEndObject();
    }

    internal static Dictionary<string, ContentItem> BuildItemMap(IEnumerable<(ContentItem Item, RouteInfo Route)> routed)
    {
        var result = new Dictionary<string, ContentItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var (item, route) in routed)
        {
            result[BuildPathUtils.NormalizeRelPath(route.OutputPath)] = item;
        }

        return result;
    }

    internal static void GenerateSearchIndexIndex(string outputDir, IReadOnlyList<BuildVariantResult> results)
    {
        var outPath = Path.Combine(outputDir, "search.index.json");
        Directory.CreateDirectory(outputDir);

        using var stream = File.Create(outPath);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });

        writer.WriteStartObject();
        writer.WriteNumber("version", 1);
        writer.WriteStartArray("indexes");
        foreach (var r in results)
        {
            writer.WriteStartObject();
            writer.WriteString("language", r.Language);
            writer.WriteString("path", NormalizeSearchUrl(r.BaseUrl, "/search.json"));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
    }

    internal static string NormalizeSearchUrl(string baseUrl, string url)
    {
        var u = url.StartsWith('/') ? url : "/" + url;
        if (string.IsNullOrWhiteSpace(baseUrl) || baseUrl == "/")
        {
            return u;
        }

        var b = baseUrl.StartsWith('/') ? baseUrl : "/" + baseUrl;
        if (b.Length > 1 && b.EndsWith('/'))
        {
            b = b.TrimEnd('/');
        }

        return b + u;
    }

    private static string BuildSnippet(ContentItem item, string text)
    {
        if (item.Meta.TryGetValue("summary", out var summary) && summary is not null)
        {
            return summary.ToString() ?? string.Empty;
        }

        return text.Length > 280 ? text[..280] : text;
    }

    internal static string StripHtmlToText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(html.Length);
        var inside = false;
        var tagName = new StringBuilder();
        var inTagName = false;
        for (var i = 0; i < html.Length; i++)
        {
            var c = html[i];
            if (c == '<')
            {
                inside = true;
                tagName.Clear();
                inTagName = true;
                continue;
            }

            if (c == '>')
            {
                inside = false;
                inTagName = false;
                var tag = tagName.ToString();
                if (tag.StartsWith("script", StringComparison.OrdinalIgnoreCase) ||
                    tag.StartsWith("style", StringComparison.OrdinalIgnoreCase))
                {
                    var closeTag = "</" + tag;
                    var closeIndex = html.IndexOf(closeTag, i, StringComparison.OrdinalIgnoreCase);
                    if (closeIndex >= 0)
                    {
                        i = closeIndex + closeTag.Length - 1;
                        continue;
                    }
                }

                sb.Append(' ');
                continue;
            }

            if (!inside)
            {
                sb.Append(c);
            }
            else if (c == ' ' && inTagName)
            {
                inTagName = false;
            }
            else if (inTagName && (char.IsLetter(c) || c == '/' || c == '!'))
            {
                tagName.Append(c);
            }

            if (inside && c == '-' && i + 2 < html.Length && html[i + 1] == '-' && html[i + 2] == '>')
            {
                inside = false;
                inTagName = false;
                sb.Append(' ');
                i += 2;
            }
        }

        return WebUtility.HtmlDecode(sb.ToString()).ReplaceLineEndings(" ").Trim();
    }
}
