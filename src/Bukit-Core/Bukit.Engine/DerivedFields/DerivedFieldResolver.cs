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

    private const string IdentityIssuePrefix = "identity.";

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
    /// The keys (id and language) of the pages <see cref="Apply"/> changed: <paramref name="routed"/> is
    /// its result for <paramref name="before"/>, in the same order.
    /// </summary>
    internal static IReadOnlySet<string> DerivedKeys(IReadOnlyList<RoutedContentDocument> before, IReadOnlyList<RoutedContentDocument> routed)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < routed.Count; i++)
        {
            if (!ReferenceEquals(before[i].Document, routed[i].Document))
            {
                keys.Add(RecordKey(routed[i].Document.Record));
            }
        }

        return keys;
    }

    /// <summary>
    /// Replaces the content graph documents and records of the derived pages (<paramref name="derivedKeys"/>
    /// from <see cref="DerivedKeys"/>), so consumers that read the graph (the page index, related pages)
    /// see the derived values. Every other graph document is kept as it is.
    /// </summary>
    internal static CanonicalContentGraph ProjectRecords(
        CanonicalContentGraph graph,
        IReadOnlyList<RoutedContentDocument> routed,
        IReadOnlySet<string> derivedKeys)
    {
        if (derivedKeys.Count == 0)
        {
            return graph;
        }

        var derived = new Dictionary<string, ContentDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in routed)
        {
            var key = RecordKey(page.Document.Record);
            if (derivedKeys.Contains(key))
            {
                derived.TryAdd(key, page.Document);
            }
        }

        return CanonicalContentGraphBuilder.BuildFromDocuments(
            [.. graph.Documents.Select(document => derived.TryGetValue(RecordKey(document.Record), out var replacement) ? replacement : document)]);
    }

    /// <summary>
    /// True for a canonical or content model issue the validate stage leaves to <see cref="ValidateDerived"/>:
    /// any issue of a page in a collection with derived rules, except identity issues (identity cannot be
    /// derived) and checks of fields no rule writes (they keep their source values).
    /// </summary>
    internal static bool DefersCheck(AppConfig config, ContentValidationIssue issue, IReadOnlyDictionary<string, ContentDocument> documentsById)
        => DerivedPageCollection(config, issue.SourcePath, documentsById) is { } collection &&
           IsDeferredCheck(config.Content, collection, issue);

    /// <summary>
    /// True for a required-field diagnostic of the normalizer on a field a derived rule writes on that page:
    /// <see cref="ValidateDerived"/> repeats the check on the derived value.
    /// </summary>
    internal static bool DefersRequiredDiagnostic(AppConfig config, ContentValidationIssue issue, IReadOnlyDictionary<string, ContentDocument> documentsById)
        => RequiredFieldCodes.Contains(issue.Code) &&
           DerivedPageCollection(config, issue.SourcePath, documentsById) is { } collection &&
           WritesField(config.Content, collection, issue.Field);

    /// <summary>
    /// Runs the checks the validate stage deferred (see <see cref="DefersCheck"/> and
    /// <see cref="DefersRequiredDiagnostic"/>) on the routed pages of the collections with derived rules,
    /// after derivation. Relation targets resolve against the whole site, as in the validate stage: the
    /// routed pages with their derived values, and every other document of every language.
    /// </summary>
    /// <param name="siteDocuments">Every content document of the site; routed pages replace their own entry.</param>
    internal static IReadOnlyList<ContentValidationIssue> ValidateDerived(
        AppConfig config,
        ContentModelSchema schema,
        IReadOnlyList<RoutedContentDocument> routed,
        IReadOnlyList<ContentDocument> siteDocuments)
    {
        var content = config.Content;
        if (content.DerivedFields.Count == 0)
        {
            return [];
        }

        var pages = new Dictionary<string, (ContentDocument Document, string Collection)>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in routed)
        {
            if (ContentFieldReader.GetCollection(page.Document) is { } collection && HasRules(content, collection))
            {
                pages.TryAdd(page.Document.Id, (page.Document, collection));
            }
        }

        if (pages.Count == 0)
        {
            return [];
        }

        // Pages come first, so an id that translations share finds the page of this variant.
        var pageKeys = pages.Values.Select(page => RecordKey(page.Document.Record)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var routedKeys = routed.Select(page => RecordKey(page.Document.Record)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var site = CanonicalContentGraphBuilder.BuildFromDocuments(
            [.. routed.Select(page => page.Document), .. siteDocuments.Where(document => !routedKeys.Contains(RecordKey(document.Record)))]);
        // The canonical checks look at one record at a time.
        var own = CanonicalContentGraphBuilder.BuildFromDocuments([.. pages.Values.Select(page => page.Document)]);
        var required = new List<ContentDiagnostic>();
        foreach (var (document, _) in pages.Values)
        {
            DefaultContentNormalizer.AddRequiredFieldDiagnostics(required, schema, document.CustomFields, document.Id);
        }

        return [.. CanonicalContentValidator.Validate(own, config.Site)
            .Concat(ContentModelSchemaValidator.Validate(site, schema, record => pageKeys.Contains(RecordKey(record))))
            .Where(issue => issue.SourcePath is { } id && pages.TryGetValue(id, out var page) && IsDeferredCheck(content, page.Collection, issue))
            .Concat(required
                .Where(diagnostic => diagnostic.SourceId is { } id && diagnostic.Field is { } field && WritesField(content, pages[id].Collection, field))
                .Select(diagnostic => new ContentValidationIssue(diagnostic.Field!, diagnostic.Code, diagnostic.Message, diagnostic.SourceId)))
            .DistinctBy(issue => $"{issue.SourcePath}:{issue.Field}:{issue.Code}", StringComparer.OrdinalIgnoreCase)];
    }

    private static readonly IReadOnlySet<string> RequiredFieldCodes = new HashSet<string>(
        ["content.required_canonical_field_missing", "content.required_custom_field_missing", "content.required_collection_field_missing"],
        StringComparer.Ordinal);

    /// <summary>The collection of the page an issue belongs to, when it has a page and derived rules.</summary>
    private static string? DerivedPageCollection(AppConfig config, string? id, IReadOnlyDictionary<string, ContentDocument> documentsById)
        => config.Content.DerivedFields.Count > 0 &&
           id is not null &&
           documentsById.TryGetValue(id, out var document) &&
           CollectionPagePolicy.HasPage(document, config.Site) &&
           ContentFieldReader.GetCollection(document) is { } collection &&
           HasRules(config.Content, collection)
            ? collection
            : null;

    private static bool IsDeferredCheck(ContentConfig content, string collection, ContentValidationIssue issue)
        => issue.Field.StartsWith(FieldIssuePrefix, StringComparison.Ordinal)
            ? WritesField(content, collection, issue.Field[FieldIssuePrefix.Length..])
            : !issue.Field.StartsWith(IdentityIssuePrefix, StringComparison.Ordinal);

    private static bool HasRules(ContentConfig content, string collection)
        => content.DerivedFields.Any(rule => string.Equals(rule.Collection, collection, StringComparison.OrdinalIgnoreCase));

    private static bool WritesField(ContentConfig content, string collection, string field)
        => content.DerivedFields.Any(rule =>
            string.Equals(rule.Collection, collection, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(rule.Field, field, StringComparison.OrdinalIgnoreCase));

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
            // noindex, robots and the feed, search and sitemap exclusions can be derived too.
            Publish = ContentPublishPolicy.FromFields(fields),
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
