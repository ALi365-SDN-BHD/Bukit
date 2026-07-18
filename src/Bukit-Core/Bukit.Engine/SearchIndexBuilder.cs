using System.Net;
using System.Text;
using System.Text.Json;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
namespace Bukit.Engine;

internal static class SearchIndexBuilder
{
    internal static void GenerateMergedSearchIndex(
        string outputDir,
        IReadOnlyList<BuildVariantResult> results,
        bool includeDerived,
        int maxContentLength)
    {
        var outPath = Path.Combine(outputDir, "search.json");
        Directory.CreateDirectory(outputDir);

        using var stream = File.Create(outPath);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });

        writer.WriteStartArray();

        foreach (var r in results)
        {
            var documentsByPath = BuildDocumentMap(includeDerived ? r.RoutedDocuments.Concat(r.DerivedDocuments) : r.RoutedDocuments);
            var listRoutesByPath = BuildListRouteMap(r.ListRouteGraph);
            foreach (var (key, seo) in r.SeoIndex
                         .Where(x => x.Value.Indexable)
                         .OrderBy(x => x.Value.Route.Url, StringComparer.OrdinalIgnoreCase))
            {
                if (documentsByPath.TryGetValue(key, out var document))
                {
                    WriteSearchItem(writer, document, seo.Route, r.BaseUrl, r.BodyStore, r.SearchSnippetsEnabled, maxContentLength);
                }
                else if (listRoutesByPath.TryGetValue(BuildPathUtils.NormalizeRelPath(key), out var listRoute))
                {
                    WriteListRouteSearchItem(writer, listRoute, r.BaseUrl, r.SeoModels, r.SearchSnippetsEnabled, maxContentLength);
                }
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
        int maxContentLength,
        IReadOnlyList<RoutedContentDocument> routed,
        IReadOnlyList<RoutedContentDocument> derivedRouted,
        IReadOnlyDictionary<string, SeoIndexEntry> seoIndex,
        IContentBodyStore bodyStore,
        ListRouteGraph? listRouteGraph = null,
        IReadOnlyDictionary<string, SeoModel>? seoModels = null)
    {
        var outPath = Path.Combine(outputDir, "search.json");
        Directory.CreateDirectory(outputDir);

        using var stream = File.Create(outPath);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });
        writer.WriteStartArray();

        var documentsByPath = BuildDocumentMap(includeDerived ? routed.Concat(derivedRouted) : routed);
        var listRoutesByPath = BuildListRouteMap(listRouteGraph);
        foreach (var (key, seo) in seoIndex
                     .Where(x => x.Value.Indexable)
                     .OrderBy(x => x.Value.Route.Url, StringComparer.OrdinalIgnoreCase))
        {
            if (documentsByPath.TryGetValue(key, out var document))
            {
                if (!IsSearchExcluded(document))
                {
                    WriteSearchItem(writer, document, seo.Route, baseUrl, bodyStore, emitSnippet, maxContentLength);
                }
                continue;
            }

            if (listRoutesByPath.TryGetValue(BuildPathUtils.NormalizeRelPath(key), out var listRoute))
            {
                WriteListRouteSearchItem(writer, listRoute, baseUrl, seoModels, emitSnippet, maxContentLength);
            }
        }

        writer.WriteEndArray();
        writer.Flush();
    }

    private static bool IsSearchExcluded(ContentDocument document)
    {
        return ContentFieldReader.GetBool(document.CustomFields, "searchExclude") is true;
    }

    internal static void WriteSearchItem(
        Utf8JsonWriter writer,
        ContentDocument document,
        RouteInfo route,
        string baseUrl,
        IContentBodyStore bodyStore,
        bool emitSnippet,
        int maxContentLength)
    {
        var record = document.Record;
        var publicId = PublicContentProjectionPolicy.ResolvePublicId(record, NormalizeSearchUrl(baseUrl, route.Url));

        writer.WriteStartObject();
        writer.WriteString("id", publicId);
        writer.WriteString("title", record.Presentation.Title);
        writer.WriteString("url", NormalizeSearchUrl(baseUrl, route.Url));

        if (!string.IsNullOrWhiteSpace(record.Presentation.Summary))
        {
            writer.WriteString("summary", record.Presentation.Summary);
        }

#pragma warning disable CS0618
        var text = StripHtmlToText(ContentBodyResolver.GetHtml(document, bodyStore));
#pragma warning restore CS0618
        writer.WriteString("content", TruncateContent(text, maxContentLength));
        if (emitSnippet)
        {
            writer.WriteString("snippet", BuildSnippet(document, record, text));
        }
        writer.WriteString("type", record.Classification.Type);
        writer.WriteString("contentType", record.Identity.ContentType);
        writer.WriteString("collection", record.Classification.Collection);
        writer.WriteString("reviewStatus", record.Trust.ReviewStatus);

        if (record.Classification.Tags.Count > 0)
        {
            writer.WriteStartArray("tags");
            foreach (var tag in record.Classification.Tags)
            {
                writer.WriteStringValue(tag);
            }

            writer.WriteEndArray();
        }

        if (record.Classification.Sections.Count > 0)
        {
            writer.WriteStartArray("categories");
            foreach (var section in record.Classification.Sections)
            {
                writer.WriteStringValue(section);
            }

            writer.WriteEndArray();
        }

        writer.WriteString("language", record.Presentation.Language);
        writer.WriteString("publishAt", record.Lifecycle.PublishedAt.ToString("O"));

        var publicEntities = PublicContentProjectionPolicy.SanitizeEntities(record);
        if (publicEntities.Count > 0)
        {
            writer.WritePropertyName("entities");
            writer.WriteStartArray();
            foreach (var entity in publicEntities.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                writer.WriteStringValue(entity);
            }
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    internal static Dictionary<string, ContentDocument> BuildDocumentMap(IEnumerable<RoutedContentDocument> routed)
    {
        var result = new Dictionary<string, ContentDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var routedDocument in routed)
        {
            result[BuildPathUtils.NormalizeRelPath(routedDocument.Route.OutputPath)] = routedDocument.Document;
        }

        return result;
    }

    private static Dictionary<string, ListRoutePlan> BuildListRouteMap(ListRouteGraph? listRouteGraph)
    {
        var result = new Dictionary<string, ListRoutePlan>(StringComparer.OrdinalIgnoreCase);
        if (listRouteGraph is null)
        {
            return result;
        }

        foreach (var route in listRouteGraph.Routes)
        {
            result[BuildPathUtils.NormalizeRelPath(route.OutputPath)] = route;
        }

        return result;
    }

    internal static void WriteListRouteSearchItem(
        Utf8JsonWriter writer,
        ListRoutePlan route,
        string baseUrl,
        IReadOnlyDictionary<string, SeoModel>? seoModels,
        bool emitSnippet,
        int maxContentLength)
    {
        SeoModel? seo = null;
        if (seoModels is not null)
        {
            var seoKey = BuildPathUtils.NormalizeRelPath(route.OutputPath);
            seoModels.TryGetValue(seoKey, out seo);
        }

        var title = ResolveListRouteTitle(route, seo);
        var summary = seo?.Description;
        var content = BuildListRouteSearchText(route, title, summary);

        writer.WriteStartObject();
        writer.WriteString("id", route.RouteId);
        writer.WriteString("title", title);
        writer.WriteString("url", NormalizeSearchUrl(baseUrl, route.Url));
        if (!string.IsNullOrWhiteSpace(summary))
        {
            writer.WriteString("summary", summary);
        }

        writer.WriteString("content", TruncateContent(content, maxContentLength));
        if (emitSnippet)
        {
            writer.WriteString("snippet", !string.IsNullOrWhiteSpace(summary)
                ? summary
                : content.Length > 280 ? content[..280] : content);
        }

        var type = ResolveListRouteType(route);
        writer.WriteString("type", type);
        writer.WriteString("contentType", "list");
        writer.WriteString("collection", route.Collection);
        writer.WriteString("reviewStatus", "generated");

        var language = route.Items
            .Select(item => item.ContentRecord?.Presentation.Language)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (!string.IsNullOrWhiteSpace(language))
        {
            writer.WriteString("language", language);
        }

        writer.WriteEndObject();
    }

    private static string ResolveListRouteTitle(ListRoutePlan route, SeoModel? seo)
    {
        if (!string.IsNullOrWhiteSpace(seo?.Title))
        {
            return seo.Title;
        }

        if (route.TaxonomyContext is { IsIndex: false } taxonomy &&
            !string.IsNullOrWhiteSpace(taxonomy.Term))
        {
            return taxonomy.Term;
        }

        if (route.FilterContext is { Value: not null } filter &&
            !string.IsNullOrWhiteSpace(filter.Value))
        {
            return filter.Value;
        }

        if (!string.IsNullOrWhiteSpace(route.Collection))
        {
            return route.Collection;
        }

        return route.Url == "/" ? "Home" : route.Url.Trim('/').Replace('-', ' ');
    }

    private static string ResolveListRouteType(ListRoutePlan route)
        => route.Kind switch
        {
            ListRouteKind.TaxonomyIndex or ListRouteKind.TaxonomyTermPage => "taxonomy",
            ListRouteKind.FilteredListPage => "filter",
            ListRouteKind.CollectionList or ListRouteKind.CollectionPage => "collection",
            _ => "list"
        };

    private static string BuildListRouteSearchText(ListRoutePlan route, string title, string? summary)
    {
        var parts = new List<string> { title };
        if (!string.IsNullOrWhiteSpace(summary))
        {
            parts.Add(summary);
        }

        if (!string.IsNullOrWhiteSpace(route.Collection))
        {
            parts.Add(route.Collection);
        }

        if (route.TaxonomyContext is not null)
        {
            parts.Add(route.TaxonomyContext.Kind);
            if (!string.IsNullOrWhiteSpace(route.TaxonomyContext.Term))
            {
                parts.Add(route.TaxonomyContext.Term);
            }
        }

        if (route.FilterContext is not null)
        {
            parts.Add(route.FilterContext.Field);
            if (!string.IsNullOrWhiteSpace(route.FilterContext.Value))
            {
                parts.Add(route.FilterContext.Value);
            }

            parts.AddRange(route.FilterContext.Values.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        foreach (var item in route.Items)
        {
            parts.Add(item.Title);
            if (!string.IsNullOrWhiteSpace(item.Summary))
            {
                parts.Add(item.Summary);
            }
        }

        return string.Join(' ', parts.Where(value => !string.IsNullOrWhiteSpace(value)));
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

    private static string BuildSnippet(ContentDocument document, ContentRecord record, string text)
    {
        if (!string.IsNullOrWhiteSpace(record.Presentation.Summary))
        {
            return record.Presentation.Summary;
        }

        return text.Length > 280 ? text[..280] : text;
    }

    private static string TruncateContent(string value, int maxContentLength)
    {
        if (value.Length <= maxContentLength)
        {
            return value;
        }

        var length = maxContentLength;
        if (char.IsHighSurrogate(value[length - 1]) && char.IsLowSurrogate(value[length]))
        {
            length--;
        }

        return value[..length];
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
