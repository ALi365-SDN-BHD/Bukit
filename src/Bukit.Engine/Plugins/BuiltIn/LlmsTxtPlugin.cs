using System.Text;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;
using Bukit.Engine.Abstractions.Plugins;

namespace Bukit.Engine.Plugins.BuiltIn;

public sealed class LlmsTxtPlugin : IBukitPlugin, IAfterBuildPlugin
{
    public string Name => "llms-txt";
    public string Version => "1.0.0";

    private static readonly string[] AiBots =
    {
        "GPTBot",
        "ChatGPT-User",
        "Google-Extended",
        "Claude-Web",
        "ClaudeBot",
        "Anthropic-AI",
        "PerplexityBot",
        "Cohere-AI",
        "CCBot",
        "Diffbot",
        "FacebookBot",
        "OAI-SearchBot"
    };

    public void AfterBuild(BuildContext context)
    {
        var geo = context.Config.Site.Seo.Geo;
        if (!geo.Enabled)
        {
            return;
        }

        if (geo.LlmsTxt)
        {
            WriteLlmsTxt(context, geo);
        }

        if (geo.LlmsFullTxt)
        {
            WriteLlmsFullTxt(context);
        }
    }

    internal static void WriteLlmsTxt(BuildContext context, SeoGeoConfig geo)
    {
        var sb = new StringBuilder();
        var title = context.Config.Site.Title;
        var description = context.Config.Site.Description;

        sb.AppendLine($"# {title}");
        if (!string.IsNullOrWhiteSpace(description))
        {
            sb.AppendLine($"> {description}");
            sb.AppendLine();
        }

        var canonicalBase = BuildBase(context);
        var routed = context.RoutedDocuments
            .Concat(context.DerivedDocuments)
            .ToList();

        var keyed = new Dictionary<string, (ContentDocument Document, ContentRecord Record, SeoIndexEntry Entry, SeoModel? Model)>(StringComparer.OrdinalIgnoreCase);
        foreach (var routedDocument in routed)
        {
            var document = routedDocument.Document;
            var route = routedDocument.Route;
            var key = BuildPathUtils.NormalizeRelPath(route.OutputPath);
            if (key is null)
            {
                continue;
            }

            if (context.SeoIndex.TryGetValue(key, out var entry) && entry.Indexable)
            {
                var model = context.Data.TryGetValue("__seo_models", out var m) && m is Dictionary<string, SeoModel> dict
                    && dict.TryGetValue(key, out var seoModel)
                    ? seoModel
                    : null;
                keyed[key] = (document, document.Record, entry, model);
            }
        }

        var pages = new List<(string Url, string Title, string? Description)>();
        var groups = new Dictionary<string, List<(string Url, string Title, string? Description, DateTimeOffset Published)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, (document, record, entry, model)) in keyed)
        {
            var url = ResolveFullUrl(entry.Route.Url, canonicalBase);
            var pageTitle = record.Presentation.Title ?? model?.Title ?? document.Title;
            var desc = record.Presentation.Summary ?? model?.Description ?? description;
            var collection = record.Classification.Collection ?? ContentFieldReader.GetCollection(document);

            if (!string.IsNullOrWhiteSpace(collection))
            {
                if (!groups.TryGetValue(collection, out var group))
                {
                    group = new List<(string Url, string Title, string? Description, DateTimeOffset Published)>();
                    groups[collection] = group;
                }

                group.Add((url, pageTitle, desc, document.PublishAt));
            }
            else
            {
                pages.Add((url, pageTitle, desc));
            }
        }

        var linkCount = 0;

        if (pages.Count > 0)
        {
            var section = pages.Count switch
            {
                _ when pages.Any(p => p.Url == "/" || p.Url == canonicalBase) => "Documentation",
                _ => "Pages"
            };
            sb.AppendLine($"## {section}");
            sb.AppendLine();
            foreach (var page in pages)
            {
                sb.Append(MarkdownLink(page.Title, page.Url));
                if (!string.IsNullOrWhiteSpace(page.Description))
                {
                    sb.Append($": {page.Description}");
                }

                sb.AppendLine();
                linkCount++;
            }

            sb.AppendLine();
        }

        foreach (var (groupKey, items) in groups.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var sorted = items.OrderByDescending(a => a.Published).Take(geo.LlmsTxtMaxArticles).ToList();
            sb.AppendLine($"## {ToTitle(groupKey)}");
            sb.AppendLine();
            foreach (var item in sorted)
            {
                sb.Append(MarkdownLink(item.Title, item.Url));
                if (!string.IsNullOrWhiteSpace(item.Description))
                {
                    sb.Append($": {item.Description}");
                }

                sb.AppendLine();
                linkCount++;
            }

            sb.AppendLine();
        }

        if (geo.LlmsTxtOptionalLinks is { Count: > 0 })
        {
            sb.AppendLine("## Optional");
            sb.AppendLine();
            foreach (var link in geo.LlmsTxtOptionalLinks)
            {
                sb.Append(MarkdownLink(link.Title, link.Url));
                if (!string.IsNullOrWhiteSpace(link.Description))
                {
                    sb.Append($": {link.Description}");
                }

                sb.AppendLine();
            }

            sb.AppendLine();
        }

        if (linkCount == 0)
        {
            sb.AppendLine("No indexable pages found.");
        }

        var path = Path.Combine(context.OutputDir, "llms.txt");
        Directory.CreateDirectory(context.OutputDir);
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    internal static void WriteLlmsFullTxt(BuildContext context)
    {
        var sb = new StringBuilder();
        var title = context.Config.Site.Title;
        var description = context.Config.Site.Description;
        var canonicalBase = BuildBase(context);
        var recordsById = context.ContentGraph.Records
            .GroupBy(x => x.Identity.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        sb.AppendLine($"# {title}");
        if (!string.IsNullOrWhiteSpace(description))
        {
            sb.AppendLine($"> {description}");
            sb.AppendLine();
        }

        var routed = context.RoutedDocuments.Concat(context.DerivedDocuments);
        var documentsByPath = new Dictionary<string, ContentDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var routedDocument in routed)
        {
            documentsByPath[BuildPathUtils.NormalizeRelPath(routedDocument.Route.OutputPath)] = routedDocument.Document;
        }

        foreach (var (key, entry) in context.SeoIndex
                     .Where(x => x.Value.Indexable)
                     .OrderBy(x => x.Value.Route.Url, StringComparer.OrdinalIgnoreCase))
        {
            if (!documentsByPath.TryGetValue(key, out var document))
            {
                continue;
            }

            var record = recordsById.TryGetValue(document.Id, out var canonicalRecord)
                ? canonicalRecord
                : document.Record;

            var url = ResolveFullUrl(entry.Route.Url, canonicalBase);
            sb.AppendLine($"# {record.Presentation.Title ?? document.Title}");
            sb.AppendLine();
            sb.AppendLine($"URL: {url}");
            sb.AppendLine();

            var itemDescription = ResolveDescription(document, record, context.Config.Site.Description);
            if (!string.IsNullOrWhiteSpace(itemDescription))
            {
                sb.AppendLine(itemDescription);
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(record.Ownership.Author))
            {
                sb.AppendLine($"Author: {record.Ownership.Author}");
            }

            if (!string.IsNullOrWhiteSpace(record.Provenance.Source))
            {
                sb.AppendLine($"Source: {record.Provenance.Source}");
            }

            if (!string.IsNullOrWhiteSpace(record.Trust.ReviewStatus))
            {
                sb.AppendLine($"Review Status: {record.Trust.ReviewStatus}");
            }

            if (record.Entities.Count > 0)
            {
                sb.AppendLine($"Entities: {string.Join(", ", record.Entities.Select(x => x.Name))}");
            }

            if (!string.IsNullOrWhiteSpace(record.Ownership.Author) ||
                !string.IsNullOrWhiteSpace(record.Provenance.Source) ||
                !string.IsNullOrWhiteSpace(record.Trust.ReviewStatus) ||
                record.Entities.Count > 0)
            {
                sb.AppendLine();
            }

#pragma warning disable CS0618
            var html = ContentBodyResolver.GetHtml(document, context.BodyStore);
#pragma warning restore CS0618
            var text = SearchIndexBuilder.StripHtmlToText(html);
            sb.AppendLine(text);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        var path = Path.Combine(context.OutputDir, "llms-full.txt");
        Directory.CreateDirectory(context.OutputDir);
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static string MarkdownLink(string text, string url)
    {
        return $"- [{text}]({url})";
    }

    private static string ToTitle(string value)
    {
        var parts = value.Replace('-', ' ').Replace('_', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return "Content";
        }

        return string.Join(" ", parts.Select(part =>
            char.ToUpperInvariant(part[0]) + (part.Length == 1 ? string.Empty : part[1..])));
    }

    private static string? ResolveDescription(ContentDocument item, ContentRecord? record, string? siteDescription)
    {
        if (!string.IsNullOrWhiteSpace(record?.Presentation.Summary))
        {
            return record.Presentation.Summary.Trim();
        }

        var summary = ContentFieldReader.GetSummary(item);
        if (!string.IsNullOrWhiteSpace(summary))
        {
            return summary.Trim();
        }

        var seoDesc = ContentFieldReader.GetText(item.CustomFields, "seo_desc");
        if (!string.IsNullOrWhiteSpace(seoDesc))
        {
            return seoDesc.Trim();
        }

        var compactSeoDesc = ContentFieldReader.GetText(item.CustomFields, "seodesc");
        if (!string.IsNullOrWhiteSpace(compactSeoDesc))
        {
            return compactSeoDesc.Trim();
        }

        var desc = ContentFieldReader.GetText(item.CustomFields, "description");
        if (!string.IsNullOrWhiteSpace(desc))
        {
            return desc.Trim();
        }

        if (!string.IsNullOrWhiteSpace(siteDescription))
        {
            return siteDescription.Trim();
        }

        return null;
    }

    private static string ResolveFullUrl(string url, string baseUrl)
    {
        var trimmedUrl = url.Trim();
        if (trimmedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmedUrl;
        }

        var u = trimmedUrl.StartsWith('/') ? trimmedUrl : "/" + trimmedUrl;
        if (string.IsNullOrWhiteSpace(baseUrl) || baseUrl == "/")
        {
            return u;
        }

        var b = baseUrl.Trim().TrimEnd('/');
        if (b.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            b.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return b + u;
        }

        if (!b.StartsWith('/'))
        {
            b = "/" + b;
        }

        return b + u;
    }

    private static string BuildBase(BuildContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.Config.Site.Url))
        {
            return context.Config.Site.Url.Trim().TrimEnd('/');
        }

        return context.BaseUrl;
    }
}
