using System.Text;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Abstractions.Plugins;
namespace Bukit.Engine.Plugins.BuiltIn;

internal static class TaxonomyPageCreator
{
    internal static IReadOnlyList<RoutedContentDocument> CreateKind(
        string baseUrlPrefix,
        string kind,
        string title,
        string singularTitlePrefix,
        Dictionary<string, TaxonomyTerm> terms,
        string indexTemplate,
        string termTemplate,
        bool emitContentHtml,
        int pageSize,
        bool indexEnabled,
        bool hierarchical,
        string outputPathEncoding)
    {
        var hierarchy = hierarchical
            ? TaxonomyHierarchyBuilder.BuildHierarchy(terms)
            : new Dictionary<string, TaxonomyHierarchyBuilder.HierarchyInfo>(StringComparer.OrdinalIgnoreCase);
        var derived = new List<RoutedContentDocument>();
        var items = terms.Values
            .OrderByDescending(x => x.Weight)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var publishAt = items
            .SelectMany(t => t.Pages)
            .Select(p => p.PublishAt)
            .DefaultIfEmpty(DateTimeOffset.UnixEpoch)
            .Max();
        if (indexEnabled)
        {
            var visibleTerms = items.Where(t => t.IsVisible).ToList();
            derived.Add(CreateIndexPage(baseUrlPrefix, kind, title, visibleTerms, hierarchy, indexTemplate, publishAt, emitContentHtml, outputPathEncoding));
        }

        foreach (var term in items)
        {
            var hi = hierarchy.TryGetValue(term.Slug, out var h) ? h : null;
            if (term.Pages.Count == 0)
            {
                derived.Add(CreateTermPage(
                    baseUrlPrefix,
                    kind,
                    singularTitlePrefix,
                    term,
                    hi,
                    termTemplate,
                    publishAt,
                    emitContentHtml,
                    pageSize,
                    page: 1,
                    totalPages: 1,
                    items: Array.Empty<TaxonomyPage>(),
                    outputPathEncoding));
                continue;
            }

            var totalPages = (int)Math.Ceiling(term.Pages.Count / (double)pageSize);
            for (var page = 1; page <= totalPages; page++)
            {
                var skip = (page - 1) * pageSize;
                var chunk = term.Pages.Skip(skip).Take(pageSize).ToList();
                var pagePublishAt = chunk.Count == 0 ? publishAt : chunk.Max(x => x.PublishAt);
                derived.Add(CreateTermPage(
                    baseUrlPrefix,
                    kind,
                    singularTitlePrefix,
                    term,
                    hi,
                    termTemplate,
                    pagePublishAt,
                    emitContentHtml,
                    pageSize,
                    page,
                    totalPages,
                    chunk,
                    outputPathEncoding));
            }
        }

        return derived;
    }

    internal static RoutedContentDocument CreateIndexPage(
        string baseUrlPrefix,
        string kind,
        string title,
        IReadOnlyList<TaxonomyTerm> terms,
        IReadOnlyDictionary<string, TaxonomyHierarchyBuilder.HierarchyInfo> hierarchy,
        string template,
        DateTimeOffset publishAt,
        bool emitContentHtml,
        string outputPathEncoding)
    {
        var html = string.Empty;
        if (emitContentHtml)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<ul>");
            foreach (var term in terms)
            {
                var href = $"{baseUrlPrefix}/{kind}/{term.Slug}/";
                sb.AppendLine($"  <li><a href=\"{EscapeAttr(href)}\">{EscapeHtml(term.DisplayName)}</a> <small>({term.Pages.Count})</small></li>");
            }
            sb.AppendLine("</ul>");
            html = sb.ToString();
        }

        var url = "/" + kind + "/";
        var outputPath = RoutePathBuilder.BuildOutputPathFromUrl(url, outputPathEncoding);
        var route = new RouteInfo(url, outputPath, template);
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "derived",
            ["collection"] = "page",
            ["summary"] = $"Browse all {kind}."
        };

        var termsValue = new List<object>(terms.Count);
        foreach (var term in terms)
        {
            var entry = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["title"] = term.DisplayName,
                ["slug"] = term.Slug,
                ["url"] = "/" + kind + "/" + term.Slug + "/",
                ["count"] = term.Pages.Count
            };
            if (!string.IsNullOrWhiteSpace(term.Description))
            {
                entry["description"] = term.Description;
            }
            if (!string.IsNullOrWhiteSpace(term.Image))
            {
                entry["image"] = term.Image;
            }
            if (term.Weight != 0)
            {
                entry["weight"] = term.Weight;
            }
            if (!string.IsNullOrWhiteSpace(term.ParentSlug))
            {
                entry["parent"] = term.ParentSlug;
            }
            if (term.Aliases is { Count: > 0 })
            {
                entry["aliases"] = term.Aliases;
            }
            if (hierarchy.TryGetValue(term.Slug, out var hi))
            {
                if (hi.Children.Count > 0)
                {
                    entry["children"] = hi.Children;
                }
                if (hi.Ancestors.Count > 0)
                {
                    entry["ancestors"] = hi.Ancestors;
                }
            }
            termsValue.Add(entry);
        }

        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = new ContentField("text", "derived"),
            ["collection"] = new ContentField("text", "page"),
            ["summary"] = new ContentField("text", meta["summary"]),
            ["terms"] = new ContentField("list", termsValue)
        };

        var document = DerivedContentDocumentFactory.Create(
            id: $"{kind}-index",
            title: title,
            slug: kind,
            publishAt: publishAt,
            body: new ContentBodyRef(Html: html),
            customFields: fields);

        return new RoutedContentDocument(document, route, publishAt);
    }

    internal static RoutedContentDocument CreateTermPage(
        string baseUrlPrefix,
        string kind,
        string singularTitlePrefix,
        TaxonomyTerm term,
        TaxonomyHierarchyBuilder.HierarchyInfo? hierarchyInfo,
        string template,
        DateTimeOffset publishAt,
        bool emitContentHtml,
        int pageSize,
        int page,
        int totalPages,
        IReadOnlyList<TaxonomyPage> items,
        string outputPathEncoding)
    {
        var html = string.Empty;
        if (emitContentHtml)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<ul>");

            foreach (var pageItem in items)
            {
                var href = $"{baseUrlPrefix}{pageItem.Url}";
                sb.AppendLine($"  <li><a href=\"{EscapeAttr(href)}\">{EscapeHtml(pageItem.Title)}</a></li>");
            }

            sb.AppendLine("</ul>");
            html = sb.ToString();
        }

        var isFirstPage = page <= 1;
        var url = isFirstPage
            ? "/" + kind + "/" + term.Slug + "/"
            : "/" + kind + "/" + term.Slug + "/page/" + page + "/";
        var outputPath = RoutePathBuilder.BuildOutputPathFromUrl(url, outputPathEncoding);
        var route = new RouteInfo(url, outputPath, template);
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "derived",
            ["collection"] = "page",
            ["summary"] = BuildTermSummary(kind, term, page, totalPages, items.Count)
        };

        var itemsValue = new List<object>(items.Count);
        foreach (var pageItem in items)
        {
            var obj = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["title"] = pageItem.Title,
                ["url"] = pageItem.Url,
                ["publish_date"] = pageItem.PublishAt.DateTime
            };
            if (!string.IsNullOrWhiteSpace(pageItem.Summary))
            {
                obj["summary"] = pageItem.Summary!;
            }

            if (pageItem.Extra is not null)
            {
                foreach (var kv in pageItem.Extra)
                {
                    if (!obj.ContainsKey(kv.Key))
                    {
                        obj[kv.Key] = kv.Value;
                    }
                }
            }

            itemsValue.Add(obj);
        }

        var taxonomyValue = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["kind"] = kind,
            ["term"] = term.DisplayName,
            ["slug"] = term.Slug,
            ["count"] = term.Pages.Count
        };
        if (!string.IsNullOrWhiteSpace(term.Description))
        {
            taxonomyValue["description"] = term.Description;
        }
        if (!string.IsNullOrWhiteSpace(term.Image))
        {
            taxonomyValue["image"] = term.Image;
        }
        if (term.Weight != 0)
        {
            taxonomyValue["weight"] = term.Weight;
        }
        if (!string.IsNullOrWhiteSpace(term.ParentSlug))
        {
            taxonomyValue["parent"] = term.ParentSlug;
        }
        if (term.Aliases is { Count: > 0 })
        {
            taxonomyValue["aliases"] = term.Aliases;
        }
        if (hierarchyInfo is not null)
        {
            if (hierarchyInfo.Children.Count > 0)
            {
                taxonomyValue["children"] = hierarchyInfo.Children;
            }
            if (hierarchyInfo.Ancestors.Count > 0)
            {
                taxonomyValue["ancestors"] = hierarchyInfo.Ancestors;
            }
        }

        var paginationValue = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["page"] = page,
            ["page_size"] = pageSize,
            ["total"] = term.Pages.Count,
            ["total_pages"] = totalPages,
            ["has_prev"] = page > 1,
            ["has_next"] = page < totalPages
        };

        var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = new ContentField("text", "derived"),
            ["collection"] = new ContentField("text", "page"),
            ["summary"] = new ContentField("text", meta["summary"]),
            ["items"] = new ContentField("list", itemsValue),
            ["taxonomy"] = new ContentField("object", taxonomyValue),
            ["pagination"] = new ContentField("object", paginationValue)
        };

        var document = DerivedContentDocumentFactory.Create(
            id: page <= 1 ? $"{kind}-{term.Slug}" : $"{kind}-{term.Slug}-page-{page}",
            title: page <= 1 ? $"{singularTitlePrefix}: {term.DisplayName}" : $"{singularTitlePrefix}: {term.DisplayName} (Page {page})",
            slug: term.Slug,
            publishAt: publishAt,
            body: new ContentBodyRef(Html: html),
            customFields: fields);

        return new RoutedContentDocument(document, route, publishAt);
    }

    internal static string EscapeHtml(string value)
    {
        return (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
    }

    private static string BuildTermSummary(string kind, TaxonomyTerm term, int page, int totalPages, int visibleCount)
    {
        if (!string.IsNullOrWhiteSpace(term.Description) && page <= 1)
        {
            return term.Description!;
        }

        var relation = string.Equals(kind, "tags", StringComparison.OrdinalIgnoreCase) ? "tagged" : "in";
        var suffix = totalPages > 1 ? $" Page {page} of {totalPages}." : string.Empty;
        return visibleCount > 0
            ? $"Browse {visibleCount} content items {relation} {term.DisplayName}.{suffix}"
            : $"Browse content {relation} {term.DisplayName}.{suffix}";
    }

    internal static string EscapeAttr(string value)
    {
        return EscapeHtml(value);
    }
}
