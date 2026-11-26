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

    private const string FieldIssuePrefix = "fields.";

    /// <param name="problems">Receives one issue per runtime error; null drops them.</param>
    /// <param name="schema">
    /// The content model the canonical record of a derived page is rebuilt with, so its summary,
    /// tags, media and other projections match the derived fields.
    /// </param>
    internal static IReadOnlyList<RoutedContentDocument> Apply(
        ContentConfig content,
        IReadOnlyList<RoutedContentDocument> routed,
        List<ContentValidationIssue>? problems = null,
        ContentModelSchema? schema = null)
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
            var document = Derive(page.Document, rules, problems, schema);
            result[i] = ReferenceEquals(document, page.Document) ? page : page with { Document = document };
        }

        return result;
    }

    /// <summary>
    /// Replaces the content graph documents and records that <see cref="Apply"/> derived (same id and
    /// language), so consumers that read the graph (the page index, related pages) see the derived values.
    /// </summary>
    internal static CanonicalContentGraph ProjectRecords(CanonicalContentGraph graph, IReadOnlyList<RoutedContentDocument> routed)
    {
        var derived = new Dictionary<string, ContentDocument>(StringComparer.OrdinalIgnoreCase);
        var originals = graph.Documents
            .GroupBy(document => RecordKey(document.Record), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Record, StringComparer.OrdinalIgnoreCase);
        foreach (var page in routed)
        {
            var key = RecordKey(page.Document.Record);
            if (originals.TryGetValue(key, out var original) && !ReferenceEquals(original, page.Document.Record))
            {
                derived.TryAdd(key, page.Document);
            }
        }

        return derived.Count == 0
            ? graph
            : CanonicalContentGraphBuilder.BuildFromDocuments(
                [.. graph.Documents.Select(document => derived.TryGetValue(RecordKey(document.Record), out var replacement) ? replacement : document)]);
    }

    /// <summary>
    /// True for a content model issue on a field a derived rule writes in the page's collection: it is
    /// checked by <see cref="ValidateDerived"/> on the derived values instead of on the source values.
    /// </summary>
    internal static bool IsDerivedFieldIssue(
        ContentConfig content,
        ContentValidationIssue issue,
        IReadOnlyDictionary<string, ContentDocument> documentsById)
        => content.DerivedFields.Count > 0 &&
           issue.Field.StartsWith(FieldIssuePrefix, StringComparison.Ordinal) &&
           issue.SourcePath is { } id &&
           documentsById.TryGetValue(id, out var document) &&
           ContentFieldReader.GetCollection(document) is { } collection &&
           content.DerivedFields.Any(rule =>
               string.Equals(rule.Collection, collection, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(rule.Field, issue.Field[FieldIssuePrefix.Length..], StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Checks the derived values: the content model checks of the derived fields, on the derived pages
    /// and on the entries of the same collections without a page (which keep their source values), and
    /// the canonical checks that a rebuilt record newly fails (such as a cleared required author).
    /// </summary>
    internal static IEnumerable<ContentValidationIssue> ValidateDerived(
        AppConfig config,
        ContentModelSchema schema,
        IReadOnlyList<RoutedContentDocument> before,
        IReadOnlyList<RoutedContentDocument> routed,
        IReadOnlyList<ContentDocument> withoutPage)
        => config.Content.DerivedFields.Count == 0
            ? []
            : ValidateSchema(config.Content, schema, routed, withoutPage).Concat(ValidateRebuiltRecords(config, schema, before, routed, withoutPage));

    private static IEnumerable<ContentValidationIssue> ValidateSchema(
        ContentConfig content,
        ContentModelSchema schema,
        IReadOnlyList<RoutedContentDocument> routed,
        IReadOnlyList<ContentDocument> withoutPage)
    {
        var collections = content.DerivedFields.Select(rule => rule.Collection).ToHashSet(StringComparer.OrdinalIgnoreCase);
        ContentDocument[] documents = [.. routed.Select(page => page.Document).Concat(withoutPage)
            .Where(document => collections.Contains(ContentFieldReader.GetCollection(document) ?? string.Empty))];
        var documentsById = documents
            .GroupBy(document => document.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        return ContentModelSchemaValidator.Validate(CanonicalContentGraphBuilder.BuildFromDocuments(documents), schema)
            .Where(issue => IsDerivedFieldIssue(content, issue, documentsById));
    }

    /// <summary>
    /// Canonical and record-level content model issues (such as <c>requireAuthor</c>) of the rebuilt
    /// records that their source records did not have. Both checks run on the whole site (pages and
    /// entries without a page), so relation targets and cross-record rules resolve as in the validate
    /// stage; only issues of derived pages are kept. Derived-field issues come from <see cref="ValidateSchema"/>.
    /// </summary>
    private static IEnumerable<ContentValidationIssue> ValidateRebuiltRecords(
        AppConfig config,
        ContentModelSchema schema,
        IReadOnlyList<RoutedContentDocument> before,
        IReadOnlyList<RoutedContentDocument> routed,
        IReadOnlyList<ContentDocument> withoutPage)
    {
        var derivedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < routed.Count; i++)
        {
            if (!ReferenceEquals(before[i].Document, routed[i].Document))
            {
                derivedIds.Add(routed[i].Document.Id);
            }
        }

        if (derivedIds.Count == 0)
        {
            return [];
        }

        IEnumerable<ContentValidationIssue> Check(IReadOnlyList<RoutedContentDocument> pages)
        {
            var graph = CanonicalContentGraphBuilder.BuildFromDocuments([.. pages.Select(page => page.Document).Concat(withoutPage)]);
            return CanonicalContentValidator.Validate(graph, config.Site)
                .Concat(ContentModelSchemaValidator.Validate(graph, schema)
                    .Where(issue => !issue.Field.StartsWith(FieldIssuePrefix, StringComparison.Ordinal)))
                .Where(issue => issue.SourcePath is { } id && derivedIds.Contains(id));
        }

        var known = Check(before).Select(IssueKey).ToHashSet(StringComparer.Ordinal);
        return Check(routed).Where(issue => !known.Contains(IssueKey(issue)));
    }

    private static string IssueKey(ContentValidationIssue issue) => $"{issue.SourcePath}\n{issue.Field}\n{issue.Code}";

    private static string RecordKey(ContentRecord record) => $"{record.Identity.Id}\n{record.Presentation.Language}";

    /// <summary>Throws a config error for the first template that does not parse.</summary>
    internal static void ValidateTemplates(ContentConfig content) => Compile(content.DerivedFields);

    /// <summary>
    /// Logs the runtime errors <see cref="Apply"/> collected, as the validate stage logs content issues,
    /// and fails the build when a failing page's collection uses <c>schemaFailMode: strict</c>.
    /// </summary>
    /// <param name="documents">Every content document, pages and entries without a page, to find an issue's collection.</param>
    internal static void Report(
        AppConfig config,
        IReadOnlyList<ContentDocument> documents,
        IReadOnlyList<ContentValidationIssue> issues,
        ILogger logger)
    {
        if (issues.Count == 0)
        {
            return;
        }

        var globalMode = (config.Build.SchemaFailMode ?? "warn").Trim().ToLowerInvariant();
        var documentsById = documents
            .GroupBy(document => document.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
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

    private static ContentDocument Derive(ContentDocument document, Rule[] rules, List<ContentValidationIssue>? problems, ContentModelSchema? schema)
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

        // The canonical record projects summary, tags, media, ownership and more from the fields:
        // rebuild it from the derived ones. Identity, title, body and language cannot be derived.
        var rebuilt = CanonicalContentGraphBuilder.ToRecord(document, fields, schema);
        return document with
        {
            CustomFields = fields,
            Record = rebuilt with
            {
                Identity = document.Record.Identity,
                Presentation = document.Record.Presentation with { Summary = rebuilt.Presentation.Summary }
            }
        };
    }

    private static bool HasValue(IReadOnlyDictionary<string, ContentField>? fields, string name)
        => fields is not null &&
           fields.TryGetValue(name, out var field) &&
           field.Value is not null &&
           (field.Value is not string text || !string.IsNullOrWhiteSpace(text));
}
