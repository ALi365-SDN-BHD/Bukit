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
            var items = includeDerived ? r.Routed.Concat(r.DerivedRouted) : r.Routed;
            foreach (var (item, route) in items)
            {
                var key = BuildPathUtils.NormalizeRelPath(route.OutputPath);
                if (r.SeoIndex.TryGetValue(key, out var seo) && !seo.Indexable)
                {
                    continue;
                }

                writer.WriteStartObject();
                writer.WriteString("id", item.Id);
                writer.WriteString("title", item.Title);
                writer.WriteString("url", NormalizeSearchUrl(r.BaseUrl, route.Url));

                if (item.Meta.TryGetValue("summary", out var summary) && summary is not null)
                {
                    writer.WriteString("summary", summary.ToString());
                }

                var text = StripHtmlToText(ContentBodyResolver.GetHtml(item, r.BodyStore));
                if (text.Length > 8000)
                {
                    text = text[..8000];
                }

                writer.WriteString("content", text);
                if (r.SearchSnippetsEnabled)
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
        }

        writer.WriteEndArray();
        writer.Flush();
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
        for (var i = 0; i < html.Length; i++)
        {
            var c = html[i];
            if (c == '<')
            {
                inside = true;
                continue;
            }

            if (c == '>')
            {
                inside = false;
                sb.Append(' ');
                continue;
            }

            if (!inside)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().ReplaceLineEndings(" ").Trim();
    }
}
