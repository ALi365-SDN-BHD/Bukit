using Bukit.Config;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

internal static class CanonicalContentValidator
{
    internal static IReadOnlyList<ContentValidationIssue> Validate(CanonicalContentGraph graph, SiteConfig site)
    {
        var languages = I18nLanguagePolicy.GetLanguages(site);
        return Validate(graph, languages, I18nLanguagePolicy.GetDefaultLanguage(site, languages));
    }

    /// <param name="languages">The configured site languages; empty for a single-language site.</param>
    /// <param name="defaultLanguage">The variant that pages without a language render into.</param>
    internal static IReadOnlyList<ContentValidationIssue> Validate(
        CanonicalContentGraph graph,
        IReadOnlyList<string> languages,
        string defaultLanguage)
    {
        if (graph.Records.Count == 0)
        {
            return Array.Empty<ContentValidationIssue>();
        }

        var errors = new List<ContentValidationIssue>();

        // In a multi-language site translations share an id by design (same slug, one page per
        // language), so an id only has to be unique within the language variant a page renders
        // into. A single-language site renders every page together, so ids are site-wide.
        foreach (var duplicate in graph.Records
            .GroupBy(record => (record.Identity.Id, Language: VariantLanguage(record, languages, defaultLanguage)), IdLanguageComparer.Instance)
            .Where(group => group.Count() > 1))
        {
            var scope = duplicate.Key.Language.Length == 0 ? string.Empty : $" in language '{duplicate.Key.Language}'";
            errors.Add(new ContentValidationIssue(
                "identity.id",
                "canonical_duplicate_id",
                $"Duplicate canonical content id '{duplicate.Key.Id}'{scope}.",
                duplicate.Key.Id));
        }

        foreach (var record in graph.Records)
        {
            if (record.Provenance.Source is not null &&
                string.IsNullOrWhiteSpace(record.Provenance.Source))
            {
                errors.Add(new ContentValidationIssue(
                    "provenance.source",
                    "canonical_source_missing",
                    "Canonical provenance source is blank.",
                    record.Identity.Id));
            }

            if (string.IsNullOrWhiteSpace(record.Trust.ReviewStatus))
            {
                errors.Add(new ContentValidationIssue(
                    "trust.review_status",
                    "canonical_review_status_missing",
                    "Canonical trust review status is blank.",
                    record.Identity.Id));
            }

            foreach (var relation in record.Relations)
            {
                if (string.IsNullOrWhiteSpace(relation.Target))
                {
                    errors.Add(new ContentValidationIssue(
                        "relations.target",
                        "canonical_relation_target_missing",
                        $"Canonical relation '{relation.Type}' is missing a target.",
                        record.Identity.Id));
                }
            }

            foreach (var media in record.Media)
            {
                if (RequiresAlt(media) && string.IsNullOrWhiteSpace(media.Alt))
                {
                    errors.Add(new ContentValidationIssue(
                        "media.alt",
                        "canonical_media_alt_missing",
                        $"Media asset '{media.Url}' is missing alt text.",
                        record.Identity.Id));
                }
            }
        }

        return errors;
    }

    // Matches I18nLanguagePolicy.FilterDocumentsByLanguage: a page without a language ("und")
    // renders into the default language.
    private static string VariantLanguage(ContentRecord record, IReadOnlyList<string> languages, string defaultLanguage)
    {
        if (languages.Count == 0)
        {
            return string.Empty;
        }

        var language = record.Presentation.Language;
        return string.IsNullOrWhiteSpace(language) || string.Equals(language, "und", StringComparison.OrdinalIgnoreCase)
            ? defaultLanguage
            : language;
    }

    private static bool RequiresAlt(MediaAsset media)
        => string.Equals(media.Kind, "image", StringComparison.OrdinalIgnoreCase) &&
           !string.IsNullOrWhiteSpace(media.Url);

    private sealed class IdLanguageComparer : IEqualityComparer<(string Id, string Language)>
    {
        internal static readonly IdLanguageComparer Instance = new();

        public bool Equals((string Id, string Language) x, (string Id, string Language) y)
            => StringComparer.OrdinalIgnoreCase.Equals(x.Id, y.Id) &&
               StringComparer.OrdinalIgnoreCase.Equals(x.Language, y.Language);

        public int GetHashCode((string Id, string Language) obj)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Id),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Language));
    }
}
