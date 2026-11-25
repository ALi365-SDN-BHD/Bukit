using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bukit.CloudBase.Models;
using Bukit.CloudBase.Transport;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Content.CloudBase;

internal sealed class CloudBaseContentSource : IContentProvider
{
    // DateTimeOffset.MinValue / MaxValue as Unix time.
    private const long MinUnixSeconds = -62_135_596_800;
    private const long MaxUnixMilliseconds = 253_402_300_799_999;
    private const int MaxLoggedFailures = 10;

    /// <summary>
    /// A structured field dropped for being too deep, too large or invalid. Its severity is error, so
    /// the content validation stage applies the collection's <c>schemaFailMode</c>: off ignores it,
    /// warn logs it, and strict fails the build.
    /// </summary>
    internal const string StructuredFieldInvalidCode = "cloudbase.structured_field_invalid";

    private readonly CloudBaseContentSourceOptions _options;
    private readonly ILogger? _logger;
    private readonly Func<CloudBaseClient>? _clientFactory;

    internal CloudBaseContentSource(
        CloudBaseContentSourceOptions options,
        ILogger? logger = null,
        Func<CloudBaseClient>? clientFactory = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _logger = logger;
        _clientFactory = clientFactory;
    }

    public async Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.PublishableKey))
        {
            throw new ContentException(
                "CLOUDBASE_PUBLISHABLE_KEY is required for the cloudbase provider.",
                DiagnosticCode.CloudBaseCredentialMissing);
        }

        using var client = CreateClient();
        CloudBaseQueryResult page;
        try
        {
            page = await QueryRowsAsync(client, cancellationToken).ConfigureAwait(false);
        }
        catch (CloudBaseApiException ex)
        {
            throw new ContentException(
                $"CloudBase query failed: {ex.Message}",
                CloudBaseErrorMapper.Map(ex.Kind));
        }

        var isData = string.Equals(_options.Mode, "data", StringComparison.OrdinalIgnoreCase);
        var allowedSet = CloudBaseFieldProjector.BuildAllowedSet(_options.FieldPolicyMode, _options.AllowedFields);
        var drafts = new List<Draft>(page.Rows.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var slugFromId = 0;
        var untitled = 0;
        foreach (var row in page.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = GetNonBlank(row, _options.IdField);
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ContentException(
                    $"CloudBase row is missing the configured idField '{_options.IdField}'.",
                    DiagnosticCode.CloudBaseResponseInvalid);
            }

            if (!seenIds.Add(id))
            {
                throw new ContentException(
                    $"CloudBase returned more than one row with the same idField '{_options.IdField}' value; idField must be unique.",
                    DiagnosticCode.CloudBaseResponseInvalid);
            }

            var (draft, slugFromIdRow, untitledRow) = BuildDraft(row, id, allowedSet, isData);
            slugFromId += slugFromIdRow ? 1 : 0;
            untitled += untitledRow ? 1 : 0;
            drafts.Add(draft);
        }

        if (slugFromId > 0 || untitled > 0)
        {
            _logger?.Warn(
                $"event=cloudbase.missing_title_or_slug table={_options.Table} slugFromId={slugFromId} untitled={untitled}");
        }

        var imageUrls = await ResolveFilesAsync(drafts, client, isData, cancellationToken).ConfigureAwait(false);

        var documents = new List<RawContentDocument>(drafts.Count);
        var bodies = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var draft in drafts)
        {
            if (!isData)
            {
                bodies[draft.Id] = draft.Markdown;
            }

            documents.Add(new RawContentDocument(
                SourceId: draft.Id,
                SourceKind: "cloudbase",
                Title: draft.Title,
                Slug: draft.Slug,
                PublishedAt: draft.PublishedAt,
                Body: isData ? new RawBody() : new RawBody(BodyKey: draft.Id, Markdown: draft.Markdown),
                Properties: null,
                Source: new ContentSourceInfo(
                    "cloudbase",
                    SourcePath: _options.Table,
                    ExternalId: draft.Id,
                    SyncStatus: "loaded"),
                CustomFields: draft.Fields)
            {
                Diagnostics = draft.Diagnostics
            });
        }

        _logger?.Info($"event=cloudbase.stats table={_options.Table} count={documents.Count}");
        return new RawContentLoadResult(
            documents,
            new CloudBaseBodyStore(bodies, imageUrls, _options.DefaultImageUrl));
    }

    /// <summary>
    /// In whitelist mode the query asks only for the mapped, file and allowed columns. The mapped
    /// columns have defaults (slug, publish_at, updated_at) a table may not have, and PostgREST
    /// rejects a select naming a missing column with 400, so on 400 the query is repeated once
    /// without select; the projector still drops every column outside the whitelist.
    /// </summary>
    private async Task<CloudBaseQueryResult> QueryRowsAsync(
        CloudBaseClient client,
        CancellationToken cancellationToken)
    {
        var request = new CloudBaseQueryRequest
        {
            Table = _options.Table,
            Select = BuildSelectList(),
            IdField = _options.IdField,
            PageSize = _options.PageSize,
            MaxItems = _options.MaxItems,
            FilterField = _options.FilterField,
            FilterValue = _options.FilterValue,
            SortField = _options.SortField,
            SortDirection = _options.SortDirection
        };
        try
        {
            return await client.QueryAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (CloudBaseApiException ex) when (ex.StatusCode == 400 && request.Select is not null)
        {
            _logger?.Info($"event=cloudbase.select_fallback table={_options.Table}");
            return await client.QueryAsync(request with { Select = null }, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> ResolveFilesAsync(
        List<Draft> drafts,
        CloudBaseClient client,
        bool isData,
        CancellationToken cancellationToken)
    {
        if (isData)
        {
            // Data mode resolves no file references (it has no fileFields), so any field holding cloud:// is dropped.
            WarnUnsupported(StripCloudReferenceFields(drafts));
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        if (_options.FileFields.Count == 0 &&
            !drafts.Any(draft =>
                CloudBaseFileResolver.MayHoldCloudReference(draft.Markdown) ||
                draft.Fields.Values.Any(field => ContainsCloudReference(field.Value))))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var resolver = new CloudBaseFileResolver(
            client,
            _options.EnvId,
            _options.StorageBucket ?? string.Empty,
            _options.DefaultImageUrl,
            _options.SignExpiresSeconds);
        var nonStringFileValues = 0;
        foreach (var draft in drafts)
        {
            foreach (var fileField in _options.FileFields)
            {
                // A non-string file value is never sent to signing: it becomes the placeholder.
                if (draft.NonStringFileFields.Contains(fileField))
                {
                    draft.Fields[fileField] = new ContentField("text", resolver.Placeholder);
                    nonStringFileValues++;
                    continue;
                }

                if (!draft.Fields.TryGetValue(fileField, out var field) || field.Value is not string value)
                {
                    continue;
                }

                if (resolver.TryAddFieldValue(value, LogEntry(draft.Id, fileField), out var objectName))
                {
                    draft.FileObjects[fileField] = objectName;
                }
                else
                {
                    draft.Fields[fileField] = new ContentField("text", resolver.Placeholder);
                }
            }

            resolver.CollectMarkdownReferences(draft.Markdown, LogEntry(draft.Id, "body"));
        }

        await resolver.SignAsync(cancellationToken).ConfigureAwait(false);
        foreach (var draft in drafts)
        {
            foreach (var (fileField, objectName) in draft.FileObjects)
            {
                draft.Fields[fileField] = new ContentField("text", resolver.Resolve(objectName));
            }
        }

        // Name the failing entries (deduplicated, capped) so the cause is findable without the object names.
        var failures = resolver.Failures.Distinct(StringComparer.Ordinal).ToArray();
        if (failures.Length > 0)
        {
            var shown = string.Join("; ", failures.Take(MaxLoggedFailures));
            var more = failures.Length > MaxLoggedFailures
                ? $" (+{failures.Length - MaxLoggedFailures} more)"
                : string.Empty;
            _logger?.Warn(
                $"event=cloudbase.file_resolve_failed code={DiagnosticCodeFormatter.Format(DiagnosticCode.CloudBaseFileResolveFailed)} count={failures.Length} entries=\"{shown}{more}\"");
        }

        WarnUnsupported(resolver.UnsupportedReferences + nonStringFileValues + StripCloudReferenceFields(drafts));
        return resolver.BuildBodyUrlMap();
    }

    /// <summary>Drops public fields outside fileFields whose value contains cloud://; returns the count.</summary>
    private int StripCloudReferenceFields(List<Draft> drafts)
    {
        var dropped = 0;
        foreach (var draft in drafts)
        {
            foreach (var name in draft.Fields.Keys.ToArray())
            {
                if (_options.FileFields.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (ContainsCloudReference(draft.Fields[name].Value))
                {
                    draft.Fields.Remove(name);
                    dropped++;
                }
            }
        }

        return dropped;
    }

    /// <summary>
    /// Names a failing entry by a short hash of the row id (the id itself may be sensitive and
    /// diagnostics log only its hash) and the configured field name.
    /// </summary>
    internal static string LogEntry(string id, string field) => $"{LogId(id)}/{field}";

    /// <summary>Names a row by a short hash of its id, which may be sensitive (D7).</summary>
    internal static string LogId(string id)
        => $"row:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(id)), 0, 6)}";

    private void WarnUnsupported(int count)
    {
        if (count > 0)
        {
            _logger?.Warn(
                $"event=cloudbase.unsupported_file_reference code={DiagnosticCodeFormatter.Format(DiagnosticCode.CloudBaseUnsupportedFileReference)} count={count}");
        }
    }

    /// <summary>True when the value, any nested value or any nested object key contains cloud://.</summary>
    private static bool ContainsCloudReference(object? value)
    {
        switch (value)
        {
            case string text:
                return text.Contains("cloud://", StringComparison.OrdinalIgnoreCase);
            case System.Collections.IDictionary map:
                foreach (System.Collections.DictionaryEntry entry in map)
                {
                    if (ContainsCloudReference(entry.Key) || ContainsCloudReference(entry.Value))
                    {
                        return true;
                    }
                }

                return false;
            case System.Collections.IEnumerable items:
                foreach (var item in items)
                {
                    if (ContainsCloudReference(item))
                    {
                        return true;
                    }
                }

                return false;
            default:
                return false;
        }
    }

    private sealed class Draft(
        string id,
        string title,
        string slug,
        DateTimeOffset? publishedAt,
        Dictionary<string, ContentField> fields,
        string markdown,
        IReadOnlyList<ContentDiagnostic> diagnostics)
    {
        internal string Id { get; } = id;
        internal string Title { get; } = title;
        internal string Slug { get; } = slug;
        internal DateTimeOffset? PublishedAt { get; } = publishedAt;
        internal Dictionary<string, ContentField> Fields { get; } = fields;
        internal string Markdown { get; } = markdown;
        internal IReadOnlyList<ContentDiagnostic> Diagnostics { get; } = diagnostics;
        internal Dictionary<string, string> FileObjects { get; } = new(StringComparer.OrdinalIgnoreCase);
        internal HashSet<string> NonStringFileFields { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private IReadOnlyList<string>? BuildSelectList()
    {
        if (!string.Equals(_options.FieldPolicyMode, "whitelist", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            _options.IdField,
            _options.TitleField,
            _options.SlugField,
            _options.PublishAtField,
            _options.UpdatedAtField
        };
        if (!string.IsNullOrWhiteSpace(_options.BodyField))
        {
            columns.Add(_options.BodyField);
        }

        foreach (var field in _options.FileFields.Concat(_options.AllowedFields ?? []))
        {
            columns.Add(field);
        }

        return [.. columns];
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2000",
        Justification = "LoadRawAsync owns and disposes the returned client.")]
    private CloudBaseClient CreateClient()
        => _clientFactory?.Invoke() ??
            new CloudBaseClient(new CloudBaseClientOptions
            {
                EnvId = _options.EnvId,
                Region = _options.Region,
                PublishableKey = _options.PublishableKey,
                MaxRps = _options.MaxRps,
                MaxRetries = _options.MaxRetries
            });

    private static string? GetString(JsonElement row, string field)
        => row.ValueKind == JsonValueKind.Object &&
            row.TryGetProperty(field, out var value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

    /// <summary>Projects one row into a draft; reports whether its slug came from the id and whether it had no title.</summary>
    private (Draft Draft, bool SlugFromId, bool Untitled) BuildDraft(
        JsonElement row,
        string id,
        IReadOnlySet<string>? allowedSet,
        bool isData)
    {
        var rejected = new List<(string Field, string Problem)>();
        var fields = CloudBaseFieldProjector.ProjectWithSet(
            row,
            _options.FieldPolicyMode,
            allowedSet,
            _options.IdField,
            rejected);
        fields["source"] = new ContentField("text", "cloudbase");
        if (ParseDate(row, _options.UpdatedAtField) is { } updatedAt)
        {
            fields["last_edited_time"] = new ContentField("date", updatedAt);
        }

        // A file field holds one object name. Any other present value (array, object, number) is
        // recorded here, from the raw row, because projection may drop it before resolution.
        List<string>? nonStringFileFields = null;
        foreach (var fileField in _options.FileFields)
        {
            if (GetString(row, fileField) is { } fileValue)
            {
                fields[fileField] = new ContentField("text", fileValue);
            }
            else if (row.TryGetProperty(fileField, out var raw) &&
                     raw.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
            {
                (nonStringFileFields ??= []).Add(fileField);
            }
        }

        var title = GetNonBlank(row, _options.TitleField);
        var slug = GetNonBlank(row, _options.SlugField);
        var slugFromId = false;
        if (!isData)
        {
            // The id is internal: derive a missing slug from the title, and use the id only as a last resort.
            slug ??= NonEmpty(SlugHelper.Slugify(title ?? string.Empty));
            slugFromId = slug is null;
        }

        slug ??= id;
        var draft = new Draft(
            id,
            title ?? (isData ? id : slug),
            slug,
            ParseDate(row, _options.PublishAtField),
            fields,
            isData ? string.Empty : GetString(row, _options.BodyField) ?? string.Empty,
            // A file field holding a structure becomes the placeholder below, like any non-string file value.
            [.. rejected.Where(item => !_options.FileFields.Contains(item.Field, StringComparer.OrdinalIgnoreCase)).Select(item => new ContentDiagnostic(
                StructuredFieldInvalidCode,
                "error",
                $"{item.Problem} The field was dropped.",
                item.Field))]);
        if (nonStringFileFields is not null)
        {
            draft.NonStringFileFields.UnionWith(nonStringFileFields);
        }

        return (draft, slugFromId, !isData && title is null);
    }

    private static string? NonEmpty(string value) => value.Length == 0 ? null : value;

    private static string? GetNonBlank(JsonElement row, string field)
    {
        if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty(field, out var value))
        {
            return null;
        }

        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            _ => null
        };
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static DateTimeOffset? ParseDate(JsonElement row, string field)
    {
        if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty(field, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            return parsed;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var epoch))
        {
            // Out-of-range values (microseconds, sentinels) are treated as missing, not fatal.
            return epoch >= 100_000_000_000
                ? epoch <= MaxUnixMilliseconds ? DateTimeOffset.FromUnixTimeMilliseconds(epoch) : null
                : epoch >= MinUnixSeconds ? DateTimeOffset.FromUnixTimeSeconds(epoch) : null;
        }

        return null;
    }
}
