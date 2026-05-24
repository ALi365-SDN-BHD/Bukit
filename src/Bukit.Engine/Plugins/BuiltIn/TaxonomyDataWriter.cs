using System.Text.Json;
using Bukit.Config;

namespace Bukit.Engine.Plugins.BuiltIn;

internal static class TaxonomyDataWriter
{
    internal static void SetTaxonomyData(BuildContext context, IReadOnlyList<string> itemFields)
    {
        var taxonomy = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (context.Config.Taxonomy.Kinds is { Count: > 0 } kinds)
        {
            foreach (var kindConfig in kinds)
            {
                var key = (kindConfig.Key ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var kind = string.IsNullOrWhiteSpace(kindConfig.Kind) ? key : kindConfig.Kind.Trim();
                var terms = TaxonomyIndexBuilder.GetOrBuildIndex(context, key, itemFields);
                TaxonomyIndexBuilder.MergeEnsureTerms(context, kind, terms);
                if (terms.Count == 0)
                {
                    continue;
                }

                var title = string.IsNullOrWhiteSpace(kindConfig.Title) ? kind : kindConfig.Title.Trim();
                taxonomy[kind] = BuildKindData(key, kind, title, terms);
            }
        }
        else
        {
            var tags = TaxonomyIndexBuilder.GetOrBuildIndex(context, "tags", itemFields);
            TaxonomyIndexBuilder.MergeEnsureTerms(context, "tags", tags);
            if (tags.Count > 0)
            {
                taxonomy["tags"] = BuildKindData(key: "tags", kind: "tags", title: "Tags", tags);
            }

            var categories = TaxonomyIndexBuilder.GetOrBuildIndex(context, "categories", itemFields);
            TaxonomyIndexBuilder.MergeEnsureTerms(context, "categories", categories);
            if (categories.Count > 0)
            {
                taxonomy["categories"] = BuildKindData(key: "categories", kind: "categories", title: "Categories", categories);
            }
        }

        if (taxonomy.Count > 0)
        {
            context.Data["taxonomy"] = taxonomy;
        }
    }

    internal static IReadOnlyDictionary<string, object> BuildKindData(
        string key,
        string kind,
        string title,
        Dictionary<string, TaxonomyTerm> terms)
    {
        var hierarchy = TaxonomyHierarchyBuilder.BuildHierarchy(terms);
        var termsValue = new List<object>();
        var itemsByTerm = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var term in terms.Values
            .OrderByDescending(x => x.Weight)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var termObj = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["title"] = term.DisplayName,
                ["slug"] = term.Slug,
                ["url"] = "/" + kind + "/" + term.Slug + "/",
                ["count"] = term.Pages.Count
            };
            if (!string.IsNullOrWhiteSpace(term.Description))
            {
                termObj["description"] = term.Description;
            }
            if (!string.IsNullOrWhiteSpace(term.Image))
            {
                termObj["image"] = term.Image;
            }
            if (term.Weight != 0)
            {
                termObj["weight"] = term.Weight;
            }
            if (!string.IsNullOrWhiteSpace(term.ParentSlug))
            {
                termObj["parent"] = term.ParentSlug;
            }
            if (term.Aliases is { Count: > 0 })
            {
                termObj["aliases"] = term.Aliases;
            }
            if (hierarchy.TryGetValue(term.Slug, out var hi))
            {
                if (hi.Children.Count > 0)
                {
                    termObj["children"] = hi.Children;
                }
                if (hi.Ancestors.Count > 0)
                {
                    termObj["ancestors"] = hi.Ancestors;
                }
            }
            termsValue.Add(termObj);

            var itemsValue = new List<object>(term.Pages.Count);
            foreach (var page in term.Pages)
            {
                var obj = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["title"] = page.Title,
                    ["url"] = page.Url,
                    ["publish_date"] = page.PublishAt.DateTime
                };
                if (!string.IsNullOrWhiteSpace(page.Summary))
                {
                    obj["summary"] = page.Summary!;
                }

                if (page.Extra is not null)
                {
                    foreach (var kv in page.Extra)
                    {
                        if (!obj.ContainsKey(kv.Key))
                        {
                            obj[kv.Key] = kv.Value;
                        }
                    }
                }

                itemsValue.Add(obj);
            }

            itemsByTerm[term.Slug] = itemsValue;
        }

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["key"] = key,
            ["kind"] = kind,
            ["title"] = title,
            ["terms"] = termsValue,
            ["items_by_term"] = itemsByTerm
        };
    }

    internal static void WriteKind(
        Utf8JsonWriter writer,
        string baseUrl,
        string key,
        string kind,
        string title,
        Dictionary<string, TaxonomyTerm> terms)
    {
        var hierarchy = TaxonomyHierarchyBuilder.BuildHierarchy(terms);
        writer.WriteStartObject();
        writer.WriteString("key", key);
        writer.WriteString("kind", kind);
        writer.WriteString("title", title);

        writer.WriteStartArray("terms");
        foreach (var term in terms.Values
            .OrderByDescending(x => x.Weight)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteStartObject();
            writer.WriteString("title", term.DisplayName);
            writer.WriteString("slug", term.Slug);
            writer.WriteString("url", NormalizeUrl(baseUrl, "/" + kind + "/" + term.Slug + "/"));
            writer.WriteNumber("count", term.Pages.Count);
            if (!string.IsNullOrWhiteSpace(term.Description))
            {
                writer.WriteString("description", term.Description);
            }
            if (!string.IsNullOrWhiteSpace(term.Image))
            {
                writer.WriteString("image", term.Image);
            }
            if (term.Weight != 0)
            {
                writer.WriteNumber("weight", term.Weight);
            }
            if (!string.IsNullOrWhiteSpace(term.ParentSlug))
            {
                writer.WriteString("parent", term.ParentSlug);
            }
            if (term.Aliases is { Count: > 0 })
            {
                writer.WriteStartArray("aliases");
                foreach (var alias in term.Aliases)
                {
                    writer.WriteStringValue(alias);
                }
                writer.WriteEndArray();
            }
            if (hierarchy.TryGetValue(term.Slug, out var hi))
            {
                if (hi.Children.Count > 0)
                {
                    writer.WriteStartArray("children");
                    foreach (var c in hi.Children)
                    {
                        writer.WriteStringValue(c);
                    }
                    writer.WriteEndArray();
                }
                if (hi.Ancestors.Count > 0)
                {
                    writer.WriteStartArray("ancestors");
                    foreach (var a in hi.Ancestors)
                    {
                        writer.WriteStringValue(a);
                    }
                    writer.WriteEndArray();
                }
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteStartObject("itemsByTerm");
        foreach (var term in terms.Values
            .OrderByDescending(x => x.Weight)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteStartArray(term.Slug);
            foreach (var page in term.Pages)
            {
                writer.WriteStartObject();
                writer.WriteString("title", page.Title);
                writer.WriteString("url", NormalizeUrl(baseUrl, page.Url));
                writer.WriteString("publishAt", page.PublishAt.ToString("O"));
                if (!string.IsNullOrWhiteSpace(page.Summary))
                {
                    writer.WriteString("summary", page.Summary);
                }
                WriteExtraJson(writer, page.Extra);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    internal static void WriteExtraJson(Utf8JsonWriter writer, IReadOnlyDictionary<string, object>? extra)
    {
        if (extra is null || extra.Count == 0)
        {
            return;
        }

        foreach (var kv in extra)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value is null)
            {
                continue;
            }

            if (kv.Key.Equals("title", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("url", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("publishAt", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("publish_date", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("summary", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            WriteJsonValue(writer, kv.Key, kv.Value);
        }
    }

    internal static void WriteJsonValue(Utf8JsonWriter writer, string name, object value)
    {
        switch (value)
        {
            case string s:
                writer.WriteString(name, s);
                return;
            case bool b:
                writer.WriteBoolean(name, b);
                return;
            case int i:
                writer.WriteNumber(name, i);
                return;
            case long l:
                writer.WriteNumber(name, l);
                return;
            case double d:
                writer.WriteNumber(name, d);
                return;
            case decimal m:
                writer.WriteNumber(name, m);
                return;
            case DateTime dt:
                writer.WriteString(name, dt.ToString("O"));
                return;
            case DateTimeOffset dto:
                writer.WriteString(name, dto.ToString("O"));
                return;
            case IEnumerable<object> seq:
                writer.WriteStartArray(name);
                foreach (var x in seq)
                {
                    if (x is null)
                    {
                        continue;
                    }

                    writer.WriteStringValue(x.ToString());
                }
                writer.WriteEndArray();
                return;
            default:
                writer.WriteString(name, value.ToString());
                return;
        }
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
}
