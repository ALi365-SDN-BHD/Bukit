using System.Collections;
using System.Globalization;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine.ContentLinks;

/// <summary>
/// Resolves <c>content.links</c>: the keys a page holds in a field become a <c>&lt;field&gt;_links</c>
/// list of the documents they name, written next to the field and never over it. A nested path such
/// as <c>days[].attractions</c> also gets one top-level list of every link, without repeats
/// (<c>days_attractions_links</c>). Each link is a sorted map of <c>key</c>, <c>title</c>,
/// <c>slug</c>, <c>url</c> (null for a document without a page), <c>collection</c> and the
/// <c>select</c>ed target fields.
/// </summary>
/// <remarks>
/// Both entry points see the documents of one language as the build filters them
/// (<see cref="I18nOutputMerger.FilterDocumentsByLanguage"/>). <see cref="Validate"/> runs before
/// routing, once per site language, and reports every problem on the linking page, under its
/// collection's <c>schemaFailMode</c>. <see cref="Apply"/> runs on the routed pages of one language
/// and leaves out keys it cannot resolve; its output reaches every page consumer and the page's
/// incremental hash through the page fields.
/// </remarks>
internal static class ContentLinkResolver
{
    internal const string TargetUnresolvedCode = "content.link_target_unresolved";
    internal const string TargetDuplicateCode = "content.link_target_duplicate";
    internal const string ValueInvalidCode = "content.link_value_invalid";
    internal const string OutputConflictCode = "content.link_output_conflict";

    /// <param name="Repeated">True when another document of the same language has the key too; the first one wins.</param>
    private sealed record Target(string Key, ContentDocument Document, string? Url, bool Repeated = false);

    private sealed record Link(ContentLinkConfig Config, ContentLinkPath Path);

    private readonly record struct Problem(string Code, string Message);

    /// <summary>Writes the links into the routed pages of one language.</summary>
    internal static IReadOnlyList<RoutedContentDocument> Apply(
        IReadOnlyList<ContentLinkConfig> links,
        IReadOnlyList<RoutedContentDocument> routed,
        IReadOnlyList<ContentDocument> dataDocuments)
    {
        if (links.Count == 0)
        {
            return routed;
        }

        var parsed = Parse(links);
        var index = BuildIndex(
            parsed,
            routed.Select(page => (page.Document, (string?)page.Route.Url))
                .Concat(dataDocuments.Select(document => (document, (string?)null))));
        var result = new RoutedContentDocument[routed.Count];
        for (var i = 0; i < routed.Count; i++)
        {
            var page = routed[i];
            var fields = page.Document.CustomFields;
            foreach (var link in LinksFor(parsed, page.Document))
            {
                fields = Resolve(fields, link, index, problems: null) ?? fields;
            }

            result[i] = ReferenceEquals(fields, page.Document.CustomFields)
                ? page
                : page with { Document = page.Document with { CustomFields = fields } };
        }

        return result;
    }

    /// <summary>
    /// Reports, per linking page, unresolved or repeated keys, invalid key values and output
    /// conflicts, checking each site language against the documents built for it.
    /// </summary>
    internal static IEnumerable<ContentValidationIssue> Validate(AppConfig config, IReadOnlyList<ContentDocument> documents)
    {
        var links = config.Content.Links;
        if (links.Count == 0)
        {
            return [];
        }

        var parsed = Parse(links);
        // The validate stage keeps one issue per document, field and code, so each joins its messages.
        var messages = new Dictionary<(string Page, string Field, string Code), List<string>>();
        foreach (var view in LanguageViews(config, documents))
        {
            var index = BuildIndex(parsed, view.Select(document => (document, (string?)null)));
            foreach (var page in view.Where(document => !ContentFieldReader.IsDataItem(document)))
            {
                foreach (var link in LinksFor(parsed, page))
                {
                    var problems = new List<Problem>();
                    Resolve(page.CustomFields, link, index, problems);
                    foreach (var problem in problems)
                    {
                        var key = (page.Id, link.Config.Field, problem.Code);
                        if (!messages.TryGetValue(key, out var list))
                        {
                            messages[key] = list = [];
                        }

                        if (!list.Contains(problem.Message, StringComparer.Ordinal))
                        {
                            list.Add(problem.Message);
                        }
                    }
                }
            }
        }

        return messages.Select(item => new ContentValidationIssue(item.Key.Field, item.Key.Code, string.Join(" ", item.Value), item.Key.Page));
    }

    /// <summary>The documents of each language the build makes, filtered as <see cref="RoutePipeline"/> filters them.</summary>
    private static IEnumerable<IReadOnlyList<ContentDocument>> LanguageViews(AppConfig config, IReadOnlyList<ContentDocument> documents)
    {
        var languages = I18nOutputMerger.GetLanguages(config.Site);
        if (languages.Count == 0)
        {
            return [I18nOutputMerger.FilterDocumentsByLanguage(documents, config.Site.Language, config.Site.Language)];
        }

        var defaultLanguage = I18nOutputMerger.GetDefaultLanguage(config.Site, languages);
        return languages.Select(language => I18nOutputMerger.FilterDocumentsByLanguage(documents, language, defaultLanguage));
    }

    /// <summary>Parses each link path once; config validation has already rejected invalid paths.</summary>
    private static Link[] Parse(IReadOnlyList<ContentLinkConfig> links)
        => [.. links.Select(link => ContentLinkPath.TryParse(link.Field, out var path) ? new Link(link, path) : null).OfType<Link>()];

    private static IEnumerable<Link> LinksFor(Link[] links, ContentDocument document)
    {
        var collection = document.Record.Classification.Collection;
        return links.Where(link => string.Equals(link.Config.Collection, collection, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Indexes the documents of every target collection by key. The first document with a key wins
    /// and is marked <see cref="Target.Repeated"/> when another document has the key too.
    /// </summary>
    private static Dictionary<(string Collection, string Field), Dictionary<string, Target>> BuildIndex(
        Link[] links,
        IEnumerable<(ContentDocument Document, string? Url)> documents)
    {
        var index = links
            .Select(link => (link.Config.Target.Collection, link.Config.Target.Key))
            .Distinct(TargetComparer.Instance)
            .ToDictionary(
                target => target,
                _ => new Dictionary<string, Target>(StringComparer.OrdinalIgnoreCase),
                TargetComparer.Instance);
        foreach (var (document, url) in documents)
        {
            var collection = document.Record.Classification.Collection;
            foreach (var ((targetCollection, keyField), targets) in index)
            {
                if (!string.Equals(collection, targetCollection, StringComparison.OrdinalIgnoreCase) ||
                    ReadKey(document, keyField) is not { } key)
                {
                    continue;
                }

                if (targets.TryGetValue(key, out var first))
                {
                    targets[key] = first with { Repeated = true };
                }
                else
                {
                    targets[key] = new Target(key, document, url);
                }
            }
        }

        return index;
    }

    /// <summary>The key of a document: its slug, or a text or whole-number field in invariant form.</summary>
    private static string? ReadKey(ContentDocument document, string keyField)
    {
        if (string.Equals(keyField, "slug", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(document.Slug) ? null : document.Slug.Trim();
        }

        if (document.CustomFields is null || !document.CustomFields.TryGetValue(keyField, out var field))
        {
            return null;
        }

        // Notion numbers are doubles and Markdown "1.0" is a decimal: a whole number keys as its digits.
        return field.Value switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => text.Trim(),
            long or int => ((IFormattable)field.Value).ToString(null, CultureInfo.InvariantCulture),
            decimal exact when decimal.IsInteger(exact) => exact.ToString("0", CultureInfo.InvariantCulture),
            double real when double.IsInteger(real) && Math.Abs(real) <= MaxExactDouble => ((long)real).ToString(CultureInfo.InvariantCulture),
            _ => null
        };
    }

    /// <summary>2^53: every whole double up to this is exact.</summary>
    private const double MaxExactDouble = 9007199254740992d;

    /// <returns>
    /// The page fields with the links written, or null when nothing changed. Validation (with
    /// <paramref name="problems"/>) only collects problems and builds no fields.
    /// </returns>
    private static Dictionary<string, ContentField>? Resolve(
        IReadOnlyDictionary<string, ContentField>? fields,
        Link link,
        Dictionary<(string Collection, string Field), Dictionary<string, Target>> index,
        List<Problem>? problems)
    {
        var path = link.Path;
        var rootName = path.Segments[0].Name;
        if (fields is null || !fields.TryGetValue(rootName, out var root) || root.Value is null)
        {
            return null;
        }

        var walk = new Walk(link.Config, index[(link.Config.Target.Collection, link.Config.Target.Key)], problems);
        if (!path.IsNested)
        {
            if (walk.Links(root.Value, rootName) is not { } links || !walk.CanWrite(fields, path.GroupedName) || problems is not null)
            {
                return null;
            }

            var updated = Copy(fields);
            updated[path.GroupedName] = new ContentField("list", links);
            return updated;
        }

        if (walk.Descend(root.Value, path.Segments, 0) is not { } rebuilt)
        {
            return null;
        }

        var writeFlat = walk.CanWrite(fields, path.FlatName);
        if (problems is not null)
        {
            return null;
        }

        var copy = Copy(fields);
        copy[rootName] = root with { Value = rebuilt };
        if (writeFlat)
        {
            copy[path.FlatName] = new ContentField("list", walk.Flat);
        }

        return copy;
    }

    private static Dictionary<string, ContentField> Copy(IReadOnlyDictionary<string, ContentField> fields)
        => new(fields, fields is Dictionary<string, ContentField> dictionary ? dictionary.Comparer : StringComparer.OrdinalIgnoreCase);

    private sealed class Walk(
        ContentLinkConfig link,
        Dictionary<string, Target> targets,
        List<Problem>? problems)
    {
        private readonly HashSet<string> _flatKeys = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Every link the walk resolved, first occurrence of each key only.</summary>
        internal List<object?> Flat { get; } = [];

        /// <summary>
        /// Copies <paramref name="value"/> along the path from <paramref name="index"/> and writes the
        /// grouped links into each object that holds the key field.
        /// </summary>
        /// <returns>The copy, or null when the value does not have the path's shape.</returns>
        internal object? Descend(object value, IReadOnlyList<ContentLinkPath.Segment> segments, int index)
        {
            var segment = segments[index];
            var at = Location(segments, index);
            if (!segment.IsList)
            {
                return value is IDictionary<string, object?> map
                    ? Enter(map, segments, index + 1)
                    : Invalid(at, "is not an object");
            }

            if (value is string or IDictionary<string, object?> or not IEnumerable)
            {
                return Invalid(at, "is not a list of objects");
            }

            var copy = new List<object?>();
            var position = 0;
            foreach (var item in (IEnumerable)value)
            {
                // A null item holds no keys, like an absent field.
                copy.Add(item switch
                {
                    null => null,
                    IDictionary<string, object?> map => Enter(map, segments, index + 1) ?? map,
                    _ => Invalid($"{at}[{position}]", "is not an object") ?? item
                });
                position++;
            }

            return copy;
        }

        private object? Enter(IDictionary<string, object?> map, IReadOnlyList<ContentLinkPath.Segment> segments, int index)
        {
            var segment = segments[index];
            if (!map.TryGetValue(segment.Name, out var value) || value is null)
            {
                return null;
            }

            var copy = CopyMap(map);
            if (index == segments.Count - 1)
            {
                if (Links(value, Location(segments, index)) is not { } links)
                {
                    return null;
                }

                var name = segment.Name + "_links";
                if (copy.ContainsKey(name))
                {
                    problems?.Add(new Problem(OutputConflictCode, $"'{Location(segments, index)}_links' already exists, so its links were not written."));
                    return null;
                }

                copy[name] = links;
                return copy;
            }

            if (Descend(value, segments, index) is not { } child)
            {
                return null;
            }

            copy[segment.Name] = child;
            return copy;
        }

        /// <summary>The links for a key value: one key or a list of keys, in order.</summary>
        internal List<object?>? Links(object value, string at)
        {
            IEnumerable keys;
            if (value is string text)
            {
                keys = new[] { text };
            }
            else if (value is IEnumerable list and not IDictionary<string, object?>)
            {
                keys = list;
            }
            else
            {
                Invalid(at, "is not a key or a list of keys");
                return null;
            }

            var links = new List<object?>();
            foreach (var item in keys)
            {
                if (item is not string key)
                {
                    if (item is not null)
                    {
                        problems?.Add(new Problem(ValueInvalidCode, $"'{at}' holds a value that is not text; keys must be text."));
                    }

                    continue;
                }

                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (!targets.TryGetValue(key.Trim(), out var target))
                {
                    problems?.Add(new Problem(
                        TargetUnresolvedCode,
                        $"'{at}' names {link.Target.Key} '{key.Trim()}', which no document of this language in collection '{link.Target.Collection}' has; the link was left out."));
                    continue;
                }

                if (target.Repeated)
                {
                    problems?.Add(new Problem(
                        TargetDuplicateCode,
                        $"'{at}' names {link.Target.Key} '{target.Key}', which more than one document of this language in collection '{link.Target.Collection}' has; the link uses the first."));
                }

                var entry = ToLink(target);
                links.Add(entry);
                if (_flatKeys.Add(target.Key))
                {
                    Flat.Add(entry);
                }
            }

            return links;
        }

        /// <summary>False, with a conflict reported, when the page already has <paramref name="name"/>.</summary>
        internal bool CanWrite(IReadOnlyDictionary<string, ContentField> fields, string name)
        {
            if (!fields.ContainsKey(name))
            {
                return true;
            }

            problems?.Add(new Problem(OutputConflictCode, $"Field '{name}' already exists, so its links were not written."));
            return false;
        }

        private SortedDictionary<string, object?> ToLink(Target target)
        {
            var entry = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["collection"] = target.Document.Record.Classification.Collection,
                ["key"] = target.Key,
                ["slug"] = target.Document.Slug,
                ["title"] = target.Document.Title,
                ["url"] = target.Url
            };
            foreach (var name in link.Select)
            {
                entry[name] = target.Document.CustomFields is not null && target.Document.CustomFields.TryGetValue(name, out var field)
                    ? field.Value
                    : null;
            }

            return entry;
        }

        private object? Invalid(string at, string problem)
        {
            problems?.Add(new Problem(ValueInvalidCode, $"'{at}' {problem}, so links cannot be read from it."));
            return null;
        }

        private static string Location(IReadOnlyList<ContentLinkPath.Segment> segments, int index)
            => string.Join('.', segments.Take(index + 1).Select(segment => segment.Name));

        private static IDictionary<string, object?> CopyMap(IDictionary<string, object?> map) => map switch
        {
            SortedDictionary<string, object?> sorted => new SortedDictionary<string, object?>(sorted, sorted.Comparer),
            Dictionary<string, object?> dictionary => new Dictionary<string, object?>(dictionary, dictionary.Comparer),
            _ => new Dictionary<string, object?>(map, StringComparer.OrdinalIgnoreCase)
        };
    }

    private sealed class TargetComparer : IEqualityComparer<(string Collection, string Field)>
    {
        internal static readonly TargetComparer Instance = new();

        public bool Equals((string Collection, string Field) x, (string Collection, string Field) y)
            => string.Equals(x.Collection, y.Collection, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(x.Field, y.Field, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Collection, string Field) obj)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Collection),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Field));
    }
}
