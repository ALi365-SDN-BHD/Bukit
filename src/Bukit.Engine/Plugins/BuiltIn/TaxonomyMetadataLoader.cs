namespace Bukit.Engine.Plugins.BuiltIn;

internal static class TaxonomyMetadataLoader
{
    internal static void LoadAndEnrich(BuildContext context, string kind, Dictionary<string, TaxonomyTerm> terms)
    {
        LoadFromEnsureTerms(context.Data, kind, terms);
        LoadFromIndexFiles(context.RootDir, kind, terms);
    }

    internal static void LoadFromEnsureTerms(Dictionary<string, object> data, string kind, Dictionary<string, TaxonomyTerm> terms)
    {
        if (!data.TryGetValue("taxonomy_ensure_terms", out var obj) || obj is null)
        {
            return;
        }

        if (obj is not Dictionary<string, List<Dictionary<string, object>>> map)
        {
            return;
        }

        if (!map.TryGetValue(kind, out var list) || list is null || list.Count == 0)
        {
            return;
        }

        foreach (var item in list)
        {
            var slug = item.TryGetValue("slug", out var s) && s is not null
                ? (s.ToString() ?? string.Empty).Trim()
                : string.Empty;

            if (string.IsNullOrWhiteSpace(slug) || !terms.TryGetValue(slug, out var term))
            {
                continue;
            }

            EnrichTerm(terms, slug, term, item);
        }
    }

    internal static void LoadFromIndexFiles(string rootDir, string kind, Dictionary<string, TaxonomyTerm> terms)
    {
        var taxonomyDir = Path.Combine(rootDir, "content", "_taxonomy", kind);
        if (!Directory.Exists(taxonomyDir))
        {
            return;
        }

        foreach (var termDir in Directory.EnumerateDirectories(taxonomyDir))
        {
            var slug = Path.GetFileName(termDir);
            if (string.IsNullOrWhiteSpace(slug) || !terms.TryGetValue(slug, out var term))
            {
                continue;
            }

            var indexPath = Path.Combine(termDir, "_index.md");
            if (!File.Exists(indexPath))
            {
                continue;
            }

            try
            {
                var content = File.ReadAllText(indexPath);
                var fm = ParseSimpleFrontMatter(content);
                if (fm is not null)
                {
                    EnrichTerm(terms, slug, term, fm);
                }
            }
            catch
            {
            }
        }
    }

    private static void EnrichTerm(Dictionary<string, TaxonomyTerm> terms, string slug, TaxonomyTerm term, Dictionary<string, object> item)
    {
        var needReplace = false;

        if (string.IsNullOrWhiteSpace(term.Description))
        {
            var desc = TryGetString(item, "description");
            if (desc is not null)
            {
                needReplace = true;
            }
        }

        if (string.IsNullOrWhiteSpace(term.Image))
        {
            var img = TryGetString(item, "image");
            if (img is not null)
            {
                needReplace = true;
            }
        }

        if (term.Weight == 0)
        {
            var w = TryGetInt(item, "weight");
            if (w.HasValue && w.Value != 0)
            {
                needReplace = true;
            }
        }

        if (string.IsNullOrWhiteSpace(term.ParentSlug))
        {
            var parent = TryGetString(item, "parent");
            if (parent is not null)
            {
                needReplace = true;
            }
        }

        if (needReplace)
        {
            var desc = TryGetString(item, "description") ?? term.Description;
            var img = TryGetString(item, "image") ?? term.Image;
            var weight = TryGetInt(item, "weight") ?? term.Weight;
            var parent = TryGetString(item, "parent") ?? term.ParentSlug;

            terms[slug] = new TaxonomyTerm(term.DisplayName, term.Slug)
            {
                Description = desc,
                Image = img,
                Weight = weight,
                IsVisible = term.IsVisible,
                ParentSlug = parent,
                Aliases = term.Aliases,
                Pages = term.Pages,
            };
        }
    }

    internal static Dictionary<string, object>? ParseSimpleFrontMatter(string content)
    {
        var text = content.AsSpan().TrimStart();
        if (!text.StartsWith("---"))
        {
            return null;
        }

        text = text.Slice(3);
        var end = text.IndexOf("---");
        if (end < 0)
        {
            return null;
        }

        var fmSpan = text.Slice(0, end).Trim();
        var fm = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        EnumerateLines(fmSpan.ToString(), lineStr =>
        {
            var line = lineStr.AsSpan();
            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                return;
            }

            var key = line.Slice(0, colon).Trim().ToString();
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            var valueSpan = line.Slice(colon + 1).Trim();
            fm[key] = valueSpan.ToString();
        });

        return fm.Count == 0 ? null : fm;
    }

    private static void EnumerateLines(string text, Action<string> action)
    {
        var remaining = text.AsSpan();
        while (remaining.Length > 0)
        {
            var nl = remaining.IndexOf('\n');
            ReadOnlySpan<char> line;
            if (nl < 0)
            {
                line = remaining;
                remaining = default;
            }
            else
            {
                line = remaining.Slice(0, nl).TrimEnd('\r');
                remaining = remaining.Slice(nl + 1);
            }

            if (!line.IsWhiteSpace())
            {
                action(line.ToString());
            }
        }
    }

    internal static string? TryGetString(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out var v) && v is not null)
        {
            var s = v.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(s))
            {
                return s;
            }
        }
        return null;
    }

    internal static int? TryGetInt(Dictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out var v) || v is null)
        {
            return null;
        }

        return v switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            string s => int.TryParse(s.Trim(), out var i) ? i : null,
            _ => int.TryParse(v.ToString(), out var i) ? i : null
        };
    }
}
