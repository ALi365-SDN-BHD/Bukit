using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Stages;
using Bukit.Rendering.Scriban;
using Bukit.Shared;
using Scriban.Syntax;

namespace Bukit.Engine.DerivedFields;

/// <summary>
/// Applies <c>content.derivedFields</c>: each value is a Scriban template over one page's own
/// top-level fields, so it can read <c>&lt;field&gt;_links</c> but never other pages. Values run in
/// declaration order, so a later one sees an earlier one. <c>default</c> writes only a missing or
/// empty field; <c>override</c> always writes.
/// </summary>
/// <remarks>
/// <see cref="ValidateTemplates"/> runs with config validation (<c>config check</c> and build
/// planning), so a template that does not parse fails before content is fetched.
/// <see cref="Apply"/> runs in <see cref="RoutePipeline"/> after routing and content links and before
/// anything reads the pages, so lists, SEO, feeds and search see the values, and the page hash covers
/// them. A runtime error leaves that field unchanged and is collected, with the link data the template
/// read, then <see cref="Report"/> logs it under the page collection's <c>schemaFailMode</c>.
/// </remarks>
internal static class DerivedFieldResolver
{
    internal const string FailedCode = "content.derived_field_failed";

    private sealed record Rule(ContentDerivedFieldConfig Config, DerivedFieldTemplate Template);

    /// <summary>Names that also set the summary the SEO model and lists read.</summary>
    private static readonly HashSet<string> SummaryFields = new(["summary", "description", "excerpt"], StringComparer.OrdinalIgnoreCase);

    /// <param name="problems">Receives one issue per runtime error; null drops them.</param>
    internal static IReadOnlyList<RoutedContentDocument> Apply(
        ContentConfig content,
        IReadOnlyList<RoutedContentDocument> routed,
        List<ContentValidationIssue>? problems = null)
    {
        if (content.DerivedFields.Count == 0)
        {
            return routed;
        }

        var rules = Compile(content.DerivedFields);
        var result = new RoutedContentDocument[routed.Count];
        for (var i = 0; i < routed.Count; i++)
        {
            var page = routed[i];
            var document = Derive(page.Document, rules, problems);
            result[i] = ReferenceEquals(document, page.Document) ? page : page with { Document = document };
        }

        return result;
    }

    /// <summary>
    /// Copies the summaries <see cref="Apply"/> derived into the content graph records with the same id
    /// and language, so consumers that read the graph (the page index) see the derived text too.
    /// </summary>
    internal static CanonicalContentGraph ProjectSummaries(
        ContentConfig content,
        CanonicalContentGraph graph,
        IReadOnlyList<RoutedContentDocument> routed)
    {
        var collections = content.DerivedFields
            .Where(rule => SummaryFields.Contains(rule.Field))
            .Select(rule => rule.Collection)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (collections.Count == 0)
        {
            return graph;
        }

        var summaries = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in routed)
        {
            var document = page.Document;
            if (collections.Contains(ContentFieldReader.GetCollection(document) ?? string.Empty))
            {
                summaries.TryAdd(SummaryKey(document.Id, document.Record.Presentation.Language), document.Record.Presentation.Summary);
            }
        }

        var changed = false;
        var records = new ContentRecord[graph.Records.Count];
        for (var i = 0; i < records.Length; i++)
        {
            var record = graph.Records[i];
            records[i] = summaries.TryGetValue(SummaryKey(record.Identity.Id, record.Presentation.Language), out var summary) &&
                         !string.Equals(summary, record.Presentation.Summary, StringComparison.Ordinal)
                ? record with { Presentation = record.Presentation with { Summary = summary } }
                : record;
            changed |= !ReferenceEquals(records[i], record);
        }

        return changed ? graph with { Records = records } : graph;
    }

    private static string SummaryKey(string? id, string? language) => $"{id}\n{language}";

    /// <summary>Throws a config error for the first template that does not parse.</summary>
    internal static void ValidateTemplates(ContentConfig content) => Compile(content.DerivedFields);

    /// <summary>
    /// Logs the runtime errors <see cref="Apply"/> collected, as the validate stage logs content issues,
    /// and fails the build when a failing page's collection uses <c>schemaFailMode: strict</c>.
    /// </summary>
    internal static void Report(
        AppConfig config,
        IReadOnlyList<RoutedContentDocument> routed,
        IReadOnlyList<ContentValidationIssue> issues,
        ILogger logger)
    {
        if (issues.Count == 0)
        {
            return;
        }

        var globalMode = (config.Build.SchemaFailMode ?? "warn").Trim().ToLowerInvariant();
        var documentsById = routed
            .GroupBy(page => page.Document.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Document, StringComparer.OrdinalIgnoreCase);
        var strict = 0;
        foreach (var issue in issues)
        {
            var mode = ContentGraphValidateStage.ResolveFailMode(issue, documentsById, config, globalMode);
            if (mode == "off")
            {
                continue;
            }

            strict += mode == "strict" ? 1 : 0;
            logger.Warn($"event=canonical.validation code={issue.Code} field={issue.Field} source={ContentGraphValidateStage.LogSource(issue.SourcePath, documentsById)} message={issue.Message}");
        }

        if (strict > 0)
        {
            throw new ConfigException($"Derived field validation failed with {strict} error(s).", DiagnosticCode.SchemaStrictModeBlocked);
        }
    }

    /// <summary>Parses every template; a template that does not parse is a config error.</summary>
    private static Rule[] Compile(IReadOnlyList<ContentDerivedFieldConfig> derived)
        => [.. derived.Select(item => DerivedFieldTemplate.Parse(item.Value, out var error) is { } template
            ? new Rule(item, template)
            : throw new ConfigException(
                $"content.derivedFields.{item.Collection}.{item.Field}.value does not parse: {error}",
                DiagnosticCode.ConfigInvalidValue))];

    private static ContentDocument Derive(ContentDocument document, Rule[] rules, List<ContentValidationIssue>? problems)
    {
        var collection = ContentFieldReader.GetCollection(document);
        Dictionary<string, ContentField>? fields = null;
        foreach (var rule in rules)
        {
            if (!string.Equals(rule.Config.Collection, collection, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var current = (IReadOnlyDictionary<string, ContentField>?)fields ?? document.CustomFields;
            if (rule.Config.Mode == "default" && HasValue(current, rule.Config.Field))
            {
                continue;
            }

            string value;
            try
            {
                value = rule.Template.Render(current);
            }
            catch (ScriptRuntimeException ex)
            {
                problems?.Add(new ContentValidationIssue(
                    rule.Config.Field,
                    FailedCode,
                    $"content.derivedFields.{rule.Config.Collection}.{rule.Config.Field} failed: {ex.OriginalMessage}; the field was left unchanged.",
                    document.Id));
                continue;
            }

            // An empty result leaves a default field to its fallbacks, such as the summary for seo_desc.
            if (value.Length == 0 && rule.Config.Mode == "default")
            {
                continue;
            }

            fields ??= document.CustomFields is null
                ? new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, ContentField>(document.CustomFields, StringComparer.OrdinalIgnoreCase);
            fields[rule.Config.Field] = new ContentField("text", value);
        }

        if (fields is null)
        {
            return document;
        }

        var presentation = document.Record.Presentation;
        if (rules.Any(rule => SummaryFields.Contains(rule.Config.Field)))
        {
            presentation = presentation with
            {
                Summary = ContentFieldReader.GetText(fields, "summary")
                    ?? ContentFieldReader.GetText(fields, "description")
                    ?? ContentFieldReader.GetText(fields, "excerpt")
            };
        }

        return document with
        {
            CustomFields = fields,
            Record = document.Record with { Presentation = presentation }
        };
    }

    private static bool HasValue(IReadOnlyDictionary<string, ContentField>? fields, string name)
        => fields is not null &&
           fields.TryGetValue(name, out var field) &&
           field.Value is not null &&
           (field.Value is not string text || !string.IsNullOrWhiteSpace(text));
}
