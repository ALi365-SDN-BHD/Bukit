using Bukit.Engine.Abstractions.Content;

namespace Bukit.Content.Notion;

/// <summary>
/// Resolves schema-declared Notion relations after every configured source has loaded.
/// Loaded documents are authoritative; a resolver is only consulted for mappings with a reference rule.
/// </summary>
internal sealed record NotionRelationProjectionSource(
    string SourceKey,
    IReadOnlyList<RawContentDocument> Documents,
    INotionRelationFallbackResolver? Resolver = null);

internal interface INotionRelationFallbackResolver
{
    Task<NotionRelationFallbackResult> ResolveAsync(
        IReadOnlyList<string> pageIds,
        CancellationToken cancellationToken);
}

internal interface INotionRelationFallbackResolverProvider
{
    INotionRelationFallbackResolver RelationFallbackResolver { get; }
}

internal sealed record NotionRelationFallbackResult(
    IReadOnlyList<RelationTargetInfo> Targets,
    IReadOnlyDictionary<string, string> Failures);

internal static class NotionCrossSourceRelationProjector
{
    private const int MaxFallbackTargets = 200;

    internal static async Task<IReadOnlyList<NotionRelationProjectionSource>> ProjectAsync(
        IReadOnlyList<NotionRelationProjectionSource> sources,
        ContentModelSchema? schema,
        CancellationToken cancellationToken)
    {
        if (sources.Count == 0 || schema?.RelationMappings is null || schema.RelationMappings.Count == 0)
        {
            return sources;
        }

        var mappings = schema.RelationMappings.Values
            .Where(mapping => !string.IsNullOrWhiteSpace(mapping.RawKey))
            .ToArray();
        if (mappings.Length == 0)
        {
            return sources;
        }

        var index = BuildLoadedTargetIndex(sources);
        var projected = new NotionRelationProjectionSource[sources.Count];

        for (var sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = sources[sourceIndex];
            var missing = BuildMissingIds(source.Documents, mappings, index);
            var fallback = source.Resolver is null || missing.Count == 0
                ? EmptyFallback
                : await source.Resolver.ResolveAsync(missing, cancellationToken);
            var resolved = new Dictionary<string, RelationTargetInfo>(index, StringComparer.OrdinalIgnoreCase);
            foreach (var target in fallback.Targets)
            {
                if (!string.IsNullOrWhiteSpace(target.PageId))
                {
                    resolved[target.PageId] = target;
                }
            }

            var documents = source.Documents
                .Select(document => ProjectDocument(document, mappings, resolved, fallback.Failures))
                .ToArray();
            projected[sourceIndex] = source with { Documents = documents };
        }

        return projected;
    }

    private static readonly NotionRelationFallbackResult EmptyFallback = new(
        Array.Empty<RelationTargetInfo>(),
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    private static IReadOnlyDictionary<string, RelationTargetInfo> BuildLoadedTargetIndex(
        IReadOnlyList<NotionRelationProjectionSource> sources)
    {
        var index = new Dictionary<string, RelationTargetInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            foreach (var document in source.Documents)
            {
                var target = ToTarget(document);
                if (!string.IsNullOrWhiteSpace(target.PageId) && !index.ContainsKey(target.PageId))
                {
                    index[target.PageId] = target;
                }
            }
        }

        return index;
    }

    private static RelationTargetInfo ToTarget(RawContentDocument document)
    {
        var fields = document.CustomFields;
        return new RelationTargetInfo(
            document.Id,
            document.Title,
            document.Slug,
            ContentFieldReader.GetText(fields, "type") ?? document.SourceKind,
            ContentFieldReader.GetText(fields, "url"),
            ContentFieldReader.GetText(fields, "image"),
            ContentFieldReader.GetTextList(fields, "sameAs") ?? ContentFieldReader.GetTextList(fields, "same_as"));
    }

    private static IReadOnlyList<string> BuildMissingIds(
        IReadOnlyList<RawContentDocument> documents,
        IReadOnlyList<RelationMapping> mappings,
        IReadOnlyDictionary<string, RelationTargetInfo> index)
    {
        var missing = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in documents)
        {
            foreach (var mapping in mappings.Where(mapping => mapping.Reference is not null))
            {
                if (!TryGetRelationField(document.CustomFields, mapping.RawKey, out var field))
                {
                    continue;
                }

                foreach (var id in ContentFieldReader.ToTextList(field.Value) ?? Array.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(id) || index.ContainsKey(id) || !seen.Add(id))
                    {
                        continue;
                    }

                    missing.Add(id);
                    if (missing.Count == MaxFallbackTargets)
                    {
                        return missing;
                    }
                }
            }
        }

        return missing;
    }

    private static RawContentDocument ProjectDocument(
        RawContentDocument document,
        IReadOnlyList<RelationMapping> mappings,
        IReadOnlyDictionary<string, RelationTargetInfo> index,
        IReadOnlyDictionary<string, string> failures)
    {
        var fields = document.CustomFields;
        if (fields is null)
        {
            return document;
        }

        Dictionary<string, ContentField>? projected = null;
        List<ContentDiagnostic>? diagnostics = null;
        foreach (var mapping in mappings)
        {
            if (!TryGetRelationField(fields, mapping.RawKey, out var field, out var actualKey))
            {
                continue;
            }

            var ids = ContentFieldReader.ToTextList(field.Value);
            if (ids is null || ids.Count == 0)
            {
                continue;
            }

            var links = new List<Dictionary<string, object?>>(ids.Count);
            foreach (var rawId in ids)
            {
                var id = rawId.Trim();
                if (index.TryGetValue(id, out var target))
                {
                    links.Add(CreateProjection(target, mapping.Reference));
                    continue;
                }

                links.Add(CreateUnresolvedProjection(id, mapping.Reference));
                diagnostics ??= new List<ContentDiagnostic>();
                var code = failures.TryGetValue(id, out var failure)
                    ? failure
                    : "notion.relation.unresolved";
                diagnostics.Add(new ContentDiagnostic(
                    code,
                    "warning",
                    $"Notion relation '{mapping.RawKey}' target '{id}' could not be resolved.",
                    mapping.RawKey,
                    document.Id));
            }

            projected ??= new Dictionary<string, ContentField>(fields, StringComparer.OrdinalIgnoreCase);
            projected[actualKey] = new ContentField("list", links);
        }

        if (projected is null && diagnostics is null)
        {
            return document;
        }

        return document with
        {
            CustomFields = projected ?? fields,
            Properties = RawContentValue.FromFields(projected ?? fields),
            Diagnostics = diagnostics is null
                ? document.Diagnostics
                : document.Diagnostics.Concat(diagnostics).ToArray()
        };
    }

    private static Dictionary<string, object?> CreateProjection(RelationTargetInfo target, ContentReferenceRule? reference)
    {
        var sameAs = (target.SameAs ?? Array.Empty<string>()).ToArray();
        var projection = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = target.PageId,
            ["title"] = target.Title,
            ["slug"] = target.Slug,
            ["type"] = target.Type,
            ["url"] = target.Url,
            ["image"] = target.Image,
            ["sameAs"] = sameAs
        };
        AddReferenceAliases(projection, reference, target.PageId, target.Title, target.Url);
        return projection;
    }

    private static Dictionary<string, object?> CreateUnresolvedProjection(string id, ContentReferenceRule? reference)
    {
        var projection = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = id,
            ["title"] = null,
            ["slug"] = null,
            ["type"] = null,
            ["url"] = null,
            ["image"] = null,
            ["sameAs"] = null
        };
        AddReferenceAliases(projection, reference, id, null, null);
        return projection;
    }

    private static void AddReferenceAliases(
        Dictionary<string, object?> projection,
        ContentReferenceRule? reference,
        string id,
        string? title,
        string? url)
    {
        if (reference is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(reference.IdField))
        {
            projection[reference.IdField] = id;
        }
        if (!string.IsNullOrWhiteSpace(reference.LabelField))
        {
            projection[reference.LabelField] = title;
        }
        if (!string.IsNullOrWhiteSpace(reference.UrlField))
        {
            projection[reference.UrlField] = url;
        }
    }

    private static bool TryGetRelationField(
        IReadOnlyDictionary<string, ContentField>? fields,
        string rawKey,
        out ContentField field)
        => TryGetRelationField(fields, rawKey, out field, out _);

    private static bool TryGetRelationField(
        IReadOnlyDictionary<string, ContentField>? fields,
        string rawKey,
        out ContentField field,
        out string actualKey)
    {
        field = default!;
        actualKey = string.Empty;
        if (fields is null)
        {
            return false;
        }

        var normalized = NotionContentPropertyParser.NormalizeFieldKey(rawKey);
        foreach (var pair in fields)
        {
            if (string.Equals(pair.Key, rawKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pair.Key, normalized, StringComparison.OrdinalIgnoreCase))
            {
                field = pair.Value;
                actualKey = pair.Key;
                return true;
            }
        }

        return false;
    }
}
