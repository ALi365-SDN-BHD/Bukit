using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
namespace Bukit.Content.Notion;

internal static class NotionTaxonomyPromoter
{
    public static void PromoteRelationTaxonomyTerms(Dictionary<string, object> meta, IReadOnlyDictionary<string, ContentField> fields, string key)
    {
        var linkKey = $"{key}_links";
        if (!fields.TryGetValue(linkKey, out var linksField) || linksField.Value is null)
        {
            return;
        }

        if (linksField.Value is not IEnumerable<Dictionary<string, object?>> links)
        {
            return;
        }

        var terms = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in links)
        {
            if (link is null)
            {
                continue;
            }

            var title = link.TryGetValue("title", out var t) ? t?.ToString() : null;
            var slug = link.TryGetValue("slug", out var s) ? s?.ToString() : null;
            var id = link.TryGetValue("id", out var i) ? i?.ToString() : null;

            var term = FirstNonEmpty(title, slug, id);
            if (term is null)
            {
                continue;
            }

            term = term.Trim();
            if (term.Length == 0)
            {
                continue;
            }

            if (seen.Add(term))
            {
                terms.Add(term);
            }
        }

        if (terms.Count == 0)
        {
            return;
        }

        meta[key] = terms;
    }

    private static string? FirstNonEmpty(string? a, string? b, string? c)
    {
        if (!string.IsNullOrWhiteSpace(a))
        {
            return a;
        }

        if (!string.IsNullOrWhiteSpace(b))
        {
            return b;
        }

        if (!string.IsNullOrWhiteSpace(c))
        {
            return c;
        }

        return null;
    }
}

