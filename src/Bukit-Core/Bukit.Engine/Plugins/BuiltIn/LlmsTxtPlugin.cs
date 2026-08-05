using System.Text;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;
using Bukit.Engine.Abstractions.Plugins;

namespace Bukit.Engine.Plugins.BuiltIn;

internal sealed class LlmsTxtPlugin : IBukitPlugin, IAfterBuildAsyncPlugin
{
    private readonly AppConfig _config;

    internal LlmsTxtPlugin(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

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

    public async Task AfterBuildAsync(BuildContext context, CancellationToken cancellationToken = default)
    {
        var geo = _config.Site.Seo.Geo;
        if (!geo.Enabled)
        {
            return;
        }

        var documentsByPath = new Dictionary<string, ContentDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var routedDocument in context.RoutedDocuments.Concat(context.DerivedDocuments))
        {
            var key = BuildPathUtils.NormalizeRelPath(routedDocument.Route.OutputPath);
            if (key is not null)
            {
                documentsByPath[key] = routedDocument.Document;
            }
        }

        SeoDiagnostics.AnalyzeLlmsCuration(_config, documentsByPath, context.Logger);

        if (geo.LlmsTxt)
        {
            WriteLlmsTxt(context, _config, geo);
        }

        if (geo.LlmsFullTxt)
        {
            await WriteLlmsFullTxtAsync(context, _config, cancellationToken).ConfigureAwait(false);
        }
    }

    internal sealed record LlmsCandidate(
        ContentDocument Document,
        ContentRecord Record,
        SeoIndexEntry Entry,
        SeoModel? Model,
        LlmsCurationPolicy Policy);

    internal sealed record LlmsSelection(
        IReadOnlyList<LlmsCandidate> Primary,
        IReadOnlyList<LlmsCandidate> Optional,
        IReadOnlyList<LlmsCandidate> Excluded);

    internal static LlmsSelection BuildLlmsSelection(
        IEnumerable<(ContentDocument Document, ContentRecord Record, SeoIndexEntry Entry, SeoModel? Model)> candidates)
    {
        var primary = new List<LlmsCandidate>();
        var optional = new List<LlmsCandidate>();
        var excluded = new List<LlmsCandidate>();

        foreach (var (document, record, entry, model) in candidates)
        {
            var parse = LlmsCurationPolicyParser.Parse(document);
            var candidate = new LlmsCandidate(document, record, entry, model, parse.Policy);

            if (!entry.Indexable || !parse.Valid || parse.Policy.Visibility == LlmsVisibility.Exclude)
            {
                excluded.Add(candidate);
            }
            else if (parse.Policy.Tier == LlmsTier.Optional)
            {
                optional.Add(candidate);
            }
            else
            {
                primary.Add(candidate);
            }
        }

        return new LlmsSelection(primary, optional, excluded);
    }

    internal static bool ShouldIncludeInLlms(ContentDocument document, SeoIndexEntry entry)
    {
        if (!entry.Indexable)
        {
            return false;
        }

        var parse = LlmsCurationPolicyParser.Parse(document);
        return parse.Valid && parse.Policy.Visibility != LlmsVisibility.Exclude;
    }

    private static IReadOnlyList<LlmsCandidate> SelectCollectionItems(
        IEnumerable<LlmsCandidate> candidates,
        int maxArticles)
    {
        var materialized = candidates.ToList();
        var includes = materialized
            .Where(candidate => candidate.Policy.Visibility == LlmsVisibility.Include)
            .ToList();
        var orderedAutos = OrderDeterministically(materialized
            .Where(candidate => candidate.Policy.Visibility != LlmsVisibility.Include));
        var selectedAutos = maxArticles == 0 ? orderedAutos : orderedAutos.Take(maxArticles).ToList();

        return OrderDeterministically(includes.Concat(selectedAutos));
    }

    private static IReadOnlyList<LlmsCandidate> OrderDeterministically(IEnumerable<LlmsCandidate> candidates)
        => candidates
            .OrderByDescending(candidate => candidate.Policy.Priority)
            .ThenByDescending(candidate => candidate.Document.PublishAt)
            .ThenBy(candidate => candidate.Entry.Canonical, StringComparer.Ordinal)
            .ToList();

    internal static void WriteLlmsTxt(
        BuildContext context,
        AppConfig config,
        SeoGeoConfig geo)
        => WriteLlmsTxt(
            config,
            context.OutputDir,
            context.BaseUrl,
            context.RoutedDocuments,
            context.DerivedDocuments,
            context.SeoIndex,
            context.Data.TryGetValue(BuildContextDataKeys.SeoModels, out var m) && m is IReadOnlyDictionary<string, SeoModel> models
                ? models
                : new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase),
            geo);

    internal static void WriteLlmsTxt(
        AppConfig config,
        string outputDir,
        string baseUrl,
        IReadOnlyList<RoutedContentDocument> routedDocuments,
        IReadOnlyList<RoutedContentDocument> derivedDocuments,
        IReadOnlyDictionary<string, SeoIndexEntry> seoIndex,
        IReadOnlyDictionary<string, SeoModel> seoModels,
        SeoGeoConfig geo)
    {
        var sb = new StringBuilder();
        var title = config.Site.Title;
        var description = config.Site.Description;

        sb.AppendLine($"# {title}");
        if (!string.IsNullOrWhiteSpace(description))
        {
            sb.AppendLine($"> {description}");
            sb.AppendLine();
        }

        var canonicalBase = BuildBase(config, baseUrl);
        var routed = routedDocuments
            .Concat(derivedDocuments)
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

            if (seoIndex.TryGetValue(key, out var entry))
            {
                var model = seoModels.TryGetValue(key, out var seoModel)
                    ? seoModel
                    : null;
                keyed[key] = (document, document.Record, entry, model);
            }
        }

        var selection = BuildLlmsSelection(keyed.Values);

        var pages = new List<(string Url, string Title, string? Description, DateTimeOffset Published, int Priority)>();
        var groups = new Dictionary<string, List<LlmsCandidate>>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in selection.Primary)
        {
            var url = ResolveFullUrl(candidate.Entry.Route.Url, canonicalBase);
            var pageTitle = candidate.Record.Presentation.Title ?? candidate.Model?.Title ?? candidate.Document.Title;
            var desc = candidate.Record.Presentation.Summary ?? candidate.Model?.Description ?? description;
            var collection = candidate.Record.Classification.Collection ?? ContentFieldReader.GetCollection(candidate.Document);

            if (!string.IsNullOrWhiteSpace(collection))
            {
                if (!groups.TryGetValue(collection, out var group))
                {
                    group = new List<LlmsCandidate>();
                    groups[collection] = group;
                }

                group.Add(candidate);
            }
            else
            {
                pages.Add((url, pageTitle, desc, candidate.Document.PublishAt, candidate.Policy.Priority));
            }
        }

        var optionalPages = new List<(string Url, string Title, string? Description)>();
        foreach (var candidate in OrderDeterministically(selection.Optional))
        {
            var url = ResolveFullUrl(candidate.Entry.Route.Url, canonicalBase);
            var pageTitle = candidate.Record.Presentation.Title ?? candidate.Model?.Title ?? candidate.Document.Title;
            var desc = candidate.Record.Presentation.Summary ?? candidate.Model?.Description ?? description;
            optionalPages.Add((url, pageTitle, desc));
        }

        var linkCount = 0;

        if (pages.Count > 0)
        {
            var orderedPages = pages
                .OrderByDescending(page => page.Priority)
                .ThenByDescending(page => page.Published)
                .ThenBy(page => page.Url, StringComparer.Ordinal)
                .ToList();
            var section = orderedPages.Any(p => p.Url == "/" || p.Url == canonicalBase) ? "Documentation" : "Pages";
            sb.AppendLine($"## {section}");
            sb.AppendLine();
            foreach (var page in orderedPages)
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

        foreach (var (groupKey, group) in groups.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var selected = SelectCollectionItems(group, geo.LlmsTxtMaxArticles);
            sb.AppendLine($"## {ToTitle(groupKey)}");
            sb.AppendLine();
            foreach (var candidate in selected)
            {
                var url = ResolveFullUrl(candidate.Entry.Route.Url, canonicalBase);
                var pageTitle = candidate.Record.Presentation.Title ?? candidate.Model?.Title ?? candidate.Document.Title;
                var desc = candidate.Record.Presentation.Summary ?? candidate.Model?.Description ?? description;
                sb.Append(MarkdownLink(pageTitle, url));
                if (!string.IsNullOrWhiteSpace(desc))
                {
                    sb.Append($": {desc}");
                }

                sb.AppendLine();
                linkCount++;
            }

            sb.AppendLine();
        }

        if (optionalPages.Count > 0 || geo.LlmsTxtOptionalLinks is { Count: > 0 })
        {
            sb.AppendLine("## Optional");
            sb.AppendLine();
            foreach (var page in optionalPages)
            {
                sb.Append(MarkdownLink(page.Title, page.Url));
                if (!string.IsNullOrWhiteSpace(page.Description))
                {
                    sb.Append($": {page.Description}");
                }

                sb.AppendLine();
                linkCount++;
            }

            if (geo.LlmsTxtOptionalLinks is { Count: > 0 })
            {
                foreach (var link in geo.LlmsTxtOptionalLinks)
                {
                    sb.Append(MarkdownLink(link.Title, link.Url));
                    if (!string.IsNullOrWhiteSpace(link.Description))
                    {
                        sb.Append($": {link.Description}");
                    }

                    sb.AppendLine();
                    linkCount++;
                }
            }

            sb.AppendLine();
        }

        if (linkCount == 0)
        {
            sb.AppendLine("No indexable pages found.");
        }

        var path = Path.Combine(outputDir, "llms.txt");
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    internal static async Task WriteLlmsFullTxtAsync(
        BuildContext context,
        AppConfig config,
        CancellationToken cancellationToken = default)
        => await WriteLlmsFullTxtAsync(
            config,
            context.OutputDir,
            context.BaseUrl,
            context.RoutedDocuments,
            context.DerivedDocuments,
            context.ContentGraph,
            context.SeoIndex,
            context.BodyStore,
            cancellationToken).ConfigureAwait(false);

    internal static async Task WriteLlmsFullTxtAsync(
        AppConfig config,
        string outputDir,
        string baseUrl,
        IReadOnlyList<RoutedContentDocument> routedDocuments,
        IReadOnlyList<RoutedContentDocument> derivedDocuments,
        CanonicalContentGraph contentGraph,
        IReadOnlyDictionary<string, SeoIndexEntry> seoIndex,
        IContentBodyStore bodyStore,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        var title = config.Site.Title;
        var description = config.Site.Description;
        var canonicalBase = BuildBase(config, baseUrl);
        var recordsById = contentGraph.Records
            .GroupBy(x => x.Identity.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        sb.AppendLine($"# {title}");
        if (!string.IsNullOrWhiteSpace(description))
        {
            sb.AppendLine($"> {description}");
            sb.AppendLine();
        }

        var routed = routedDocuments.Concat(derivedDocuments);
        var documentsByPath = new Dictionary<string, ContentDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var routedDocument in routed)
        {
            documentsByPath[BuildPathUtils.NormalizeRelPath(routedDocument.Route.OutputPath)] = routedDocument.Document;
        }

        foreach (var (key, entry) in seoIndex
                     .Where(x => documentsByPath.TryGetValue(x.Key, out var document) && ShouldIncludeInLlms(document, x.Value))
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

            var itemDescription = ResolveDescription(document, record, config.Site.Description);
            if (!string.IsNullOrWhiteSpace(itemDescription))
            {
                sb.AppendLine(itemDescription);
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(record.Ownership.Author))
            {
                sb.AppendLine($"Author: {record.Ownership.Author}");
            }

            if (!string.IsNullOrWhiteSpace(record.Trust.ReviewStatus))
            {
                sb.AppendLine($"Review Status: {record.Trust.ReviewStatus}");
            }

            var publicEntities = PublicContentProjectionPolicy.SanitizeEntities(record);
            if (publicEntities.Count > 0)
            {
                sb.AppendLine($"Entities: {string.Join(", ", publicEntities.Select(x => x.Name))}");
            }

            if (!string.IsNullOrWhiteSpace(record.Ownership.Author) ||
                !string.IsNullOrWhiteSpace(record.Trust.ReviewStatus) ||
                publicEntities.Count > 0)
            {
                sb.AppendLine();
            }

            var html = await ContentBodyResolver.GetHtmlAsync(document, bodyStore, cancellationToken).ConfigureAwait(false);
            var text = SearchIndexBuilder.StripHtmlToText(html);
            sb.AppendLine(text);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        var path = Path.Combine(outputDir, "llms-full.txt");
        Directory.CreateDirectory(outputDir);
        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Synchronous facade for the projection pipeline. Calls <see cref="GetHtml"/> internally.
    /// Prefer <see cref="WriteLlmsFullTxtAsync"/> in async contexts.
    /// </summary>
    internal static void WriteLlmsFullTxt(
        AppConfig config,
        string outputDir,
        string baseUrl,
        IReadOnlyList<RoutedContentDocument> routedDocuments,
        IReadOnlyList<RoutedContentDocument> derivedDocuments,
        CanonicalContentGraph contentGraph,
        IReadOnlyDictionary<string, SeoIndexEntry> seoIndex,
        IContentBodyStore bodyStore)
    {
        var sb = new StringBuilder();
        var title = config.Site.Title;
        var description = config.Site.Description;
        var canonicalBase = BuildBase(config, baseUrl);
        var recordsById = contentGraph.Records
            .GroupBy(x => x.Identity.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        sb.AppendLine($"# {title}");
        if (!string.IsNullOrWhiteSpace(description))
        {
            sb.AppendLine($"> {description}");
            sb.AppendLine();
        }

        var routed = routedDocuments.Concat(derivedDocuments);
        var documentsByPath = new Dictionary<string, ContentDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var routedDocument in routed)
        {
            documentsByPath[BuildPathUtils.NormalizeRelPath(routedDocument.Route.OutputPath)] = routedDocument.Document;
        }

        foreach (var (key, entry) in seoIndex
                     .Where(x => documentsByPath.TryGetValue(x.Key, out var document) && ShouldIncludeInLlms(document, x.Value))
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

            var itemDescription = ResolveDescription(document, record, config.Site.Description);
            if (!string.IsNullOrWhiteSpace(itemDescription))
            {
                sb.AppendLine(itemDescription);
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(record.Ownership.Author))
            {
                sb.AppendLine($"Author: {record.Ownership.Author}");
            }

            if (!string.IsNullOrWhiteSpace(record.Trust.ReviewStatus))
            {
                sb.AppendLine($"Review Status: {record.Trust.ReviewStatus}");
            }

            var publicEntities = PublicContentProjectionPolicy.SanitizeEntities(record);
            if (publicEntities.Count > 0)
            {
                sb.AppendLine($"Entities: {string.Join(", ", publicEntities.Select(x => x.Name))}");
            }

            if (!string.IsNullOrWhiteSpace(record.Ownership.Author) ||
                !string.IsNullOrWhiteSpace(record.Trust.ReviewStatus) ||
                publicEntities.Count > 0)
            {
                sb.AppendLine();
            }

#pragma warning disable CS0618
            var html = ContentBodyResolver.GetHtml(document, bodyStore);
#pragma warning restore CS0618
            var text = SearchIndexBuilder.StripHtmlToText(html);
            sb.AppendLine(text);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        var path = Path.Combine(outputDir, "llms-full.txt");
        Directory.CreateDirectory(outputDir);
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

    private static string BuildBase(AppConfig config, string baseUrl)
    {
        if (!string.IsNullOrWhiteSpace(config.Site.Url))
        {
            return config.Site.Url.Trim().TrimEnd('/');
        }

        return baseUrl;
    }
}
