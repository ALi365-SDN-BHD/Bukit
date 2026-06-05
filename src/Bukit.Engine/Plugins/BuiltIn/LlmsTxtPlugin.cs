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

    private static void WriteLlmsTxt(BuildContext context, SeoGeoConfig geo)
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
        var routed = context.Routed
            .Concat(context.DerivedRouted)
            .ToList();

        var keyed = new Dictionary<string, (ContentItem Item, SeoIndexEntry Entry, SeoModel? Model)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (item, route) in routed)
        {
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
                keyed[key] = (item, entry, model);
            }
        }

        var pages = new List<(string Url, string Title, string? Description)>();
        var groups = new Dictionary<string, List<(string Url, string Title, string? Description, DateTimeOffset Published)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, (item, entry, model)) in keyed)
        {
            var url = ResolveFullUrl(entry.Route.Url, canonicalBase);
            var pageTitle = model?.Title ?? item.Title;
            var desc = model?.Description ?? description;
            var collection = MetaHelpers.GetString(item.Meta, "collection") ?? MetaHelpers.GetString(item.Meta, "type");

            if (!string.IsNullOrWhiteSpace(collection))
            {
                if (!groups.TryGetValue(collection, out var group))
                {
                    group = new List<(string Url, string Title, string? Description, DateTimeOffset Published)>();
                    groups[collection] = group;
                }

                group.Add((url, pageTitle, desc, item.PublishAt));
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

    private static void WriteLlmsFullTxt(BuildContext context)
    {
        var sb = new StringBuilder();
        var title = context.Config.Site.Title;
        var description = context.Config.Site.Description;
        var canonicalBase = BuildBase(context);

        sb.AppendLine($"# {title}");
        if (!string.IsNullOrWhiteSpace(description))
        {
            sb.AppendLine($"> {description}");
            sb.AppendLine();
        }

        var routed = context.Routed.Concat(context.DerivedRouted);
        var itemsByPath = new Dictionary<string, ContentItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var (item, route) in routed)
        {
            itemsByPath[BuildPathUtils.NormalizeRelPath(route.OutputPath)] = item;
        }

        foreach (var (key, entry) in context.SeoIndex
                     .Where(x => x.Value.Indexable)
                     .OrderBy(x => x.Value.Route.Url, StringComparer.OrdinalIgnoreCase))
        {
            if (!itemsByPath.TryGetValue(key, out var item))
            {
                continue;
            }

            var url = ResolveFullUrl(entry.Route.Url, canonicalBase);
            sb.AppendLine($"# {item.Title}");
            sb.AppendLine();
            sb.AppendLine($"URL: {url}");
            sb.AppendLine();

            var itemDescription = ResolveDescription(item, context.Config.Site.Description);
            if (!string.IsNullOrWhiteSpace(itemDescription))
            {
                sb.AppendLine(itemDescription);
                sb.AppendLine();
            }

#pragma warning disable CS0618
            var html = ContentBodyResolver.GetHtml(item, context.BodyStore);
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

    private static string? ResolveDescription(ContentItem item, string? siteDescription)
    {
        if (item.Meta.TryGetValue("summary", out var summary) && summary is string s1 && !string.IsNullOrWhiteSpace(s1))
        {
            return s1.Trim();
        }

        if (item.Meta.TryGetValue("seo_desc", out var seoDesc) && seoDesc is string s2 && !string.IsNullOrWhiteSpace(s2))
        {
            return s2.Trim();
        }

        if (item.Meta.TryGetValue("seodesc", out var legacySeoDesc) && legacySeoDesc is string s4 && !string.IsNullOrWhiteSpace(s4))
        {
            return s4.Trim();
        }

        if (item.Meta.TryGetValue("description", out var desc) && desc is string s3 && !string.IsNullOrWhiteSpace(s3))
        {
            return s3.Trim();
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
