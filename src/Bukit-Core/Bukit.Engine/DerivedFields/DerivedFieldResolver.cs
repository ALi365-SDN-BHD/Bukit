using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
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
/// <see cref="Apply"/> runs in <see cref="RoutePipeline"/> after content links and before anything
/// reads the pages, so lists, SEO, feeds and search see the values, and the page hash covers them.
/// <see cref="Validate"/> runs in the validate stage: a template that does not parse fails the build;
/// a runtime error is reported on the page under its collection's <c>schemaFailMode</c>, and
/// <see cref="Apply"/> leaves that field unchanged.
/// </remarks>
internal static class DerivedFieldResolver
{
    internal const string FailedCode = "content.derived_field_failed";

    private sealed record Rule(ContentDerivedFieldConfig Config, DerivedFieldTemplate Template);

    /// <summary>Names that also set the summary the SEO model and lists read.</summary>
    private static readonly HashSet<string> SummaryFields = new(["summary", "description", "excerpt"], StringComparer.OrdinalIgnoreCase);

    internal static IReadOnlyList<RoutedContentDocument> Apply(ContentConfig content, IReadOnlyList<RoutedContentDocument> routed)
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
            var document = Derive(page.Document, rules, problems: null);
            result[i] = ReferenceEquals(document, page.Document) ? page : page with { Document = document };
        }

        return result;
    }

    internal static IEnumerable<ContentValidationIssue> Validate(AppConfig config, IReadOnlyList<ContentDocument> documents)
    {
        if (config.Content.DerivedFields.Count == 0)
        {
            return [];
        }

        var rules = Compile(config.Content.DerivedFields);
        var issues = new List<ContentValidationIssue>();
        foreach (var document in documents.Where(document => CollectionPagePolicy.HasPage(document, config.Site)))
        {
            Derive(document, rules, issues);
        }

        return issues;
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
