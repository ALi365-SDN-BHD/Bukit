using System.Text.Json;
using System.Text.RegularExpressions;
using Bukit.Engine.Abstractions.Plugins;

namespace Bukit.Engine.PublishAuditRules;

internal static class SemanticHtmlAuditRules
{
    private static readonly Regex ImgTagRegex = new("<img\\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AltAttributeRegex = new("\\balt\\s*=\\s*(?:\"[^\"]*\"|'[^']*')", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HeadingTagRegex = new("<h(?<level>[1-6])\\b[^>]*>(?<text>[\\s\\S]*?)</h\\k<level>>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TimeDatetimeRegex = new("<time\\b[^>]*\\bdatetime\\s*=\\s*(?:\"[^\"]+\"|'[^']+')", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MainOrArticleRegex = new("<(main|article)\\b[^>]*>(?<content>[\\s\\S]*?)</\\1>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FigureRegex = new("<figure\\b[^>]*>(?<content>[\\s\\S]*?)</figure>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StripScriptStyleRegex = new("<(script|style|template)\\b[^>]*>[\\s\\S]*?</\\1>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StripTagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex CollapseWhitespaceRegex = new("\\s+", RegexOptions.Compiled);

    internal static void Analyze(SeoIndexEntry entry, PublishDocument document, string html, List<SeoAuditIssue> issues)
    {
        if (!html.Contains("<main", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Warning("publish.semantic_main_missing", entry.Route.Url, "HTML output is missing a <main> landmark for primary page content."));
        }

        if (!html.Contains("<header", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Warning("publish.semantic_header_missing", entry.Route.Url, "HTML output is missing a <header> landmark."));
        }

        if (!html.Contains("<nav", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Warning("publish.semantic_nav_missing", entry.Route.Url, "HTML output is missing a <nav> landmark."));
        }

        if (!html.Contains("<footer", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Warning("publish.semantic_footer_missing", entry.Route.Url, "HTML output is missing a <footer> landmark."));
        }

        if (!string.Equals(entry.ContentType, "list", StringComparison.OrdinalIgnoreCase) &&
            !html.Contains("<article", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Warning("publish.semantic_article_missing", entry.Route.Url, "HTML output is missing an <article> wrapper for page content."));
        }

        var missingAltCount = ImgTagRegex.Matches(html)
            .Select(match => match.Value)
            .Count(tag => !AltAttributeRegex.IsMatch(tag));
        if (missingAltCount > 0)
        {
            issues.Add(Warning("publish.image_alt_missing", entry.Route.Url, $"HTML output contains {missingAltCount} image element(s) without an alt attribute."));
        }

        var figureWithoutCaptionCount = FigureRegex.Matches(html)
            .Select(match => match.Groups["content"].Value)
            .Count(content =>
                content.Contains("<img", StringComparison.OrdinalIgnoreCase) &&
                !content.Contains("<figcaption", StringComparison.OrdinalIgnoreCase));
        if (figureWithoutCaptionCount > 0)
        {
            issues.Add(Warning("publish.figure_caption_missing", entry.Route.Url, $"HTML output contains {figureWithoutCaptionCount} image figure(s) without a figcaption."));
        }

        var headings = HeadingTagRegex.Matches(html)
            .Select(match => new
            {
                Level = int.Parse(match.Groups["level"].Value),
                Text = NormalizeText(match.Groups["text"].Value)
            })
            .ToArray();
        if (!headings.Any(x => x.Level == 1))
        {
            issues.Add(Warning("publish.heading_h1_missing", entry.Route.Url, "HTML output is missing an <h1> for the primary page heading."));
        }

        for (var i = 1; i < headings.Length; i++)
        {
            if (headings[i].Level - headings[i - 1].Level > 1)
            {
                issues.Add(Warning("publish.heading_level_skip", entry.Route.Url, $"Heading structure skips from h{headings[i - 1].Level} to h{headings[i].Level}."));
                break;
            }
        }

        if (RequiresVisibleTime(document) && !TimeDatetimeRegex.IsMatch(html))
        {
            issues.Add(Warning("publish.time_missing", entry.Route.Url, "Dated content is missing a visible <time datetime=\"...\"> element."));
        }

        if (ContainsScriptShellWithoutReadableContent(html))
        {
            issues.Add(Warning("publish.initial_html_unreadable", entry.Route.Url, "Initial HTML does not expose enough readable main content without executing JavaScript."));
        }

        AnalyzeJsonLdConsistency(document, headings.FirstOrDefault(x => x.Level == 1)?.Text, NormalizeText(html), issues);
    }

    internal static IReadOnlyList<PublishSemanticOutlineItem> ExtractSemanticOutline(string html)
        => HeadingTagRegex.Matches(html)
            .Select(match => new PublishSemanticOutlineItem(
                int.Parse(match.Groups["level"].Value),
                NormalizeText(match.Groups["text"].Value)))
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .ToArray();

    private static void AnalyzeJsonLdConsistency(PublishDocument document, string? visibleHeading, string visibleText, List<SeoAuditIssue> issues)
    {
        if (document.SeoModel is null || string.IsNullOrWhiteSpace(visibleHeading))
        {
            return;
        }

        var titleMismatchReported = false;
        foreach (var jsonLd in document.SeoModel.JsonLd)
        {
            try
            {
                using var parsed = JsonDocument.Parse(jsonLd);
                foreach (var node in EnumerateNodes(parsed.RootElement))
                {
                    var title = ReadString(node, "headline") ?? ReadString(node, "name");
                    if (!string.IsNullOrWhiteSpace(title) &&
                        !titleMismatchReported &&
                        !string.Equals(NormalizeText(title), visibleHeading, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(Warning("publish.jsonld_title_mismatch", document.RouteUrl, "JSON-LD title/headline does not match the visible primary heading."));
                        titleMismatchReported = true;
                    }

                    var description = ReadString(node, "description");
                    if (!string.IsNullOrWhiteSpace(description) &&
                        !string.Equals(description, document.Description, StringComparison.OrdinalIgnoreCase) &&
                        !visibleText.Contains(description, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(Warning("publish.jsonld_description_mismatch", document.RouteUrl, "JSON-LD description does not match publish description or visible content."));
                    }

                    var author = ReadAuthorName(node);
                    if (!string.IsNullOrWhiteSpace(author) &&
                        !string.IsNullOrWhiteSpace(document.Author) &&
                        !string.Equals(author, document.Author, StringComparison.OrdinalIgnoreCase) &&
                        !visibleText.Contains(author, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(Warning("publish.jsonld_author_mismatch", document.RouteUrl, "JSON-LD author does not match publish author or visible content."));
                    }

                    var jsonDate = ReadString(node, "datePublished") ?? ReadString(node, "dateModified");
                    var expectedDate = document.ContentRecord?.Lifecycle.PublishedAt.Date;
                    if (!string.IsNullOrWhiteSpace(jsonDate) &&
                        expectedDate is not null &&
                        TryParseDate(jsonDate, out var parsedDate) &&
                        parsedDate != expectedDate.Value)
                    {
                        issues.Add(Warning("publish.jsonld_date_mismatch", document.RouteUrl, "JSON-LD date does not match canonical publish date."));
                    }
                }
            }
            catch (JsonException)
            {
                return;
            }
        }
    }

    private static IEnumerable<JsonElement> EnumerateNodes(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                yield return item;
            }
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            yield return root;
            if (root.TryGetProperty("@graph", out var graph) && graph.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in graph.EnumerateArray())
                {
                    yield return item;
                }
            }
        }
    }

    private static string? ReadString(JsonElement node, string property)
        => node.ValueKind == JsonValueKind.Object &&
           node.TryGetProperty(property, out var value) &&
           value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? ReadAuthorName(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Object ||
            !node.TryGetProperty("author", out var author))
        {
            return null;
        }

        if (author.ValueKind == JsonValueKind.String)
        {
            return author.GetString();
        }

        if (author.ValueKind == JsonValueKind.Object)
        {
            return ReadString(author, "name");
        }

        if (author.ValueKind == JsonValueKind.Array)
        {
            return author.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : ReadString(item, "name"))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        return null;
    }

    private static bool TryParseDate(string value, out DateTime date)
    {
        if (DateTimeOffset.TryParse(value, out var offset))
        {
            date = offset.Date;
            return true;
        }

        if (DateTime.TryParse(value, out var parsed))
        {
            date = parsed.Date;
            return true;
        }

        date = default;
        return false;
    }

    private static bool RequiresVisibleTime(PublishDocument document)
    {
        if (string.Equals(document.ContentType, "list", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var lifecycle = document.ContentRecord?.Lifecycle;
        return lifecycle is not null && (lifecycle.PublishedAt != default || lifecycle.UpdatedAt is not null);
    }

    private static bool ContainsScriptShellWithoutReadableContent(string html)
    {
        if (!html.Contains("<script", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var primaryContent = MainOrArticleRegex.Matches(html)
            .Select(match => match.Groups["content"].Value)
            .FirstOrDefault(content => !string.IsNullOrWhiteSpace(content));
        if (string.IsNullOrWhiteSpace(primaryContent))
        {
            return false;
        }

        var withoutScripts = StripScriptStyleRegex.Replace(primaryContent, " ");
        var text = NormalizeText(withoutScripts);
        return text.Length < 24;
    }

    private static string NormalizeText(string value)
        => CollapseWhitespaceRegex.Replace(StripTagRegex.Replace(value, " "), " ").Trim();

    private static SeoAuditIssue Warning(string code, string? route, string message) => new("warning", code, route, message);
}
