using Bukit.Content;
using Bukit.Engine;
using System.Text;
using System.Text.Json;

namespace Bukit.Engine.Plugins.BuiltIn;

public sealed class SearchIndexPlugin : IBukitPlugin, IAfterBuildPlugin
{
    public string Name => "search-index";
    public string Version => "2.1.0";

    public void AfterBuild(BuildContext context)
    {
        var outPath = Path.Combine(context.OutputDir, "search.json");
        Directory.CreateDirectory(context.OutputDir);
        var emitSnippet = TemplateCapabilitiesResolver.SupportsSearchSnippets(TemplateCapabilitiesResolver.SearchTemplatePath, context.LayoutsDir);

        using var stream = File.Create(outPath);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });

        writer.WriteStartArray();

        var includeDerived = context.Config.Site.SearchIncludeDerived;
        var items = includeDerived ? context.Routed.Concat(context.DerivedRouted) : context.Routed;

        foreach (var (item, route) in items)
        {
            var key = BuildPathUtils.NormalizeRelPath(route.OutputPath);
            if (context.SeoIndex.TryGetValue(key, out var seo) && !seo.Indexable)
            {
                continue;
            }

            writer.WriteStartObject();
            writer.WriteString("id", item.Id);
            writer.WriteString("title", item.Title);
            writer.WriteString("url", NormalizeUrl(context.BaseUrl, route.Url));

            if (item.Meta.TryGetValue("summary", out var summary) && summary is not null)
            {
                writer.WriteString("summary", summary.ToString());
            }

            var text = StripHtml(ContentBodyResolver.GetHtml(item, context.BodyStore), 8000);

            writer.WriteString("content", text);
            if (emitSnippet)
            {
                writer.WriteString("snippet", BuildSnippet(item, text));
            }
            writer.WriteString("type", GetString(item.Meta, "type"));

            var tags = GetStringList(item.Meta, "tags");
            if (tags is not null)
            {
                writer.WriteStartArray("tags");
                foreach (var t in tags)
                {
                    writer.WriteStringValue(t);
                }

                writer.WriteEndArray();
            }

            var categories = GetStringList(item.Meta, "categories");
            if (categories is not null)
            {
                writer.WriteStartArray("categories");
                foreach (var c in categories)
                {
                    writer.WriteStringValue(c);
                }

                writer.WriteEndArray();
            }

            writer.WriteString("language", GetString(item.Meta, "language"));
            writer.WriteString("sourceKey", GetString(item.Meta, "sourceKey") ?? GetString(item.Meta, "source"));
            writer.WriteString("publishAt", item.PublishAt.ToString("O"));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.Flush();
    }

    private static string NormalizeUrl(string baseUrl, string url)
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

    private static string StripHtml(string html, int maxChars)
    {
        if (maxChars <= 0 || string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(Math.Min(html.Length, maxChars));
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
                if (sb.Length < maxChars)
                {
                    sb.Append(' ');
                }
                continue;
            }

            if (!inside)
            {
                if (sb.Length >= maxChars)
                {
                    break;
                }

                sb.Append(c);
            }
        }

        var text = sb.ToString().ReplaceLineEndings(" ").Trim();
        return text.Length > maxChars ? text[..maxChars] : text;
    }

    private static string BuildSnippet(ContentItem item, string text)
    {
        if (item.Meta.TryGetValue("summary", out var summary) && summary is not null)
        {
            return summary.ToString() ?? string.Empty;
        }

        return text.Length > 280 ? text[..280] : text;
    }

    private static string? GetString(IReadOnlyDictionary<string, object> meta, string key)
    {
        return meta.TryGetValue(key, out var v) && v is not null ? v.ToString() : null;
    }

    private static IReadOnlyList<string>? GetStringList(IReadOnlyDictionary<string, object> meta, string key)
    {
        if (!meta.TryGetValue(key, out var v) || v is null)
        {
            return null;
        }

        if (v is string s)
        {
            var parts = s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? null : parts;
        }

        if (v is IEnumerable<object> seq)
        {
            var list = seq.Select(x => x?.ToString() ?? string.Empty)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            return list.Count == 0 ? null : list;
        }

        return null;
    }
}
