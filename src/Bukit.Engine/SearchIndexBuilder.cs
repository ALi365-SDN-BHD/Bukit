using System.Net;
using System.Text;
using System.Text.Json;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
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

    internal static void GenerateSingleSearchIndex(
        string outputDir,
        string baseUrl,
        bool includeDerived,
        bool emitSnippet,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> derivedRouted,
        IReadOnlyDictionary<string, SeoIndexEntry> seoIndex,
        IContentBodyStore bodyStore)
    {
        var outPath = Path.Combine(outputDir, "search.json");
        Directory.CreateDirectory(outputDir);

        using var stream = File.Create(outPath);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });
        writer.WriteStartArray();

        var itemsByPath = BuildItemMap(includeDerived ? routed.Concat(derivedRouted) : routed);
        foreach (var (key, seo) in seoIndex
                     .Where(x => x.Value.Indexable)
                     .OrderBy(x => x.Value.Route.Url, StringComparer.OrdinalIgnoreCase))
        {
            if (itemsByPath.TryGetValue(key, out var item) && !IsSearchExcluded(item))
            {
                WriteSearchItem(writer, item, seo.Route, baseUrl, bodyStore, emitSnippet);
            }
        }

        writer.WriteEndArray();
        writer.Flush();
    }

    private static bool IsSearchExcluded(ContentItem item)
    {
        if (item.Meta.TryGetValue("searchExclude", out var value) && value is not null)
        {
            if (value is bool b)
            {
                return b;
            }

            if (value is string s && bool.TryParse(s, out var parsed))
            {
                return parsed;
            }
        }

        return false;
    }

    internal static void WriteSearchItem(
        Utf8JsonWriter writer,
        ContentItem item,
        RouteInfo route,
        string baseUrl,
        IContentBodyStore bodyStore,
        bool emitSnippet)
    {
        var record = CanonicalContentGraphBuilder.ToRecord(item);

        writer.WriteStartObject();
        writer.WriteString("id", item.Id);
        writer.WriteString("title", record.Presentation.Title);
        writer.WriteString("url", NormalizeSearchUrl(baseUrl, route.Url));

        if (!string.IsNullOrWhiteSpace(record.Presentation.Summary))
        {
            writer.WriteString("summary", record.Presentation.Summary);
        }

#pragma warning disable CS0618
        var text = StripHtmlToText(ContentBodyResolver.GetHtml(item, bodyStore));
#pragma warning restore CS0618
        if (text.Length > 8000)
        {
            text = text[..8000];
        }

        writer.WriteString("content", text);
        if (emitSnippet)
        {
            writer.WriteString("snippet", BuildSnippet(item, record, text));
        }
        writer.WriteString("type", record.Classification.Type);
        writer.WriteString("contentType", record.Identity.ContentType);
        writer.WriteString("source", record.Provenance.Source);
        writer.WriteString("reviewStatus", record.Trust.ReviewStatus);

        if (record.Classification.Tags.Count > 0)
        {
            writer.WriteStartArray("tags");
            foreach (var t in record.Classification.Tags)
            {
                writer.WriteStringValue(t);
            }

            writer.WriteEndArray();
        }

        if (record.Classification.Sections.Count > 0)
        {
            writer.WriteStartArray("categories");
            foreach (var c in record.Classification.Sections)
            {
                writer.WriteStringValue(c);
            }

            writer.WriteEndArray();
        }

        writer.WriteString("language", record.Presentation.Language);
        writer.WriteString("sourceKey", record.Provenance.Source ?? MetaHelpers.GetString(item.Meta, "sourceKey") ?? MetaHelpers.GetString(item.Meta, "source"));
        writer.WriteString("publishAt", record.Lifecycle.PublishedAt.ToString("O"));

        writer.WriteStartArray("entities");
        foreach (var entity in record.Entities)
        {
            writer.WriteStringValue(entity.Name);
        }
        writer.WriteEndArray();
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

    private static string BuildSnippet(ContentItem item, ContentRecord record, string text)
    {
        if (!string.IsNullOrWhiteSpace(record.Presentation.Summary))
        {
            return record.Presentation.Summary;
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
