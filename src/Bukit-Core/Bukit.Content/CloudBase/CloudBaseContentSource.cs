using System.Globalization;
using System.Text.Json;
using Bukit.CloudBase.Models;
using Bukit.CloudBase.Transport;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Content.CloudBase;

internal sealed class CloudBaseContentSource : IContentProvider
{
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
            page = await client.QueryAsync(
                new CloudBaseQueryRequest
                {
                    Table = _options.Table,
                    IdField = _options.IdField,
                    PageSize = _options.PageSize,
                    MaxItems = _options.MaxItems,
                    FilterField = _options.FilterField,
                    FilterValue = _options.FilterValue,
                    SortField = _options.SortField,
                    SortDirection = _options.SortDirection
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (CloudBaseApiException ex)
        {
            throw new ContentException(
                $"CloudBase query failed: {ex.Message}",
                CloudBaseErrorMapper.Map(ex.Kind));
        }

        var isData = string.Equals(_options.Mode, "data", StringComparison.OrdinalIgnoreCase);
        var drafts = new List<Draft>(page.Rows.Count);
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

            var fields = CloudBaseFieldProjector.Project(
                row,
                _options.FieldPolicyMode,
                _options.AllowedFields,
                _options.IdField);
            fields["source"] = new ContentField("text", "cloudbase");
            if (ParseDate(row, _options.UpdatedAtField) is { } updatedAt)
            {
                fields["last_edited_time"] = new ContentField("date", updatedAt);
            }

            foreach (var fileField in _options.FileFields)
            {
                if (GetString(row, fileField) is { } fileValue)
                {
                    fields[fileField] = new ContentField("text", fileValue);
                }
            }

            drafts.Add(new Draft(
                id,
                GetNonBlank(row, _options.TitleField) ?? id,
                GetNonBlank(row, _options.SlugField) ?? id,
                ParseDate(row, _options.PublishAtField),
                fields,
                isData ? string.Empty : GetString(row, _options.BodyField) ?? string.Empty));
        }

        await ResolveFilesAsync(drafts, client, isData, cancellationToken).ConfigureAwait(false);

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
                CustomFields: draft.Fields));
        }

        _logger?.Info($"event=cloudbase.stats table={_options.Table} count={documents.Count}");
        return new RawContentLoadResult(documents, new CloudBaseBodyStore(bodies));
    }

    private async Task ResolveFilesAsync(
        List<Draft> drafts,
        CloudBaseClient client,
        bool isData,
        CancellationToken cancellationToken)
    {
        if (isData ||
            (_options.FileFields.Count == 0 &&
             !drafts.Any(draft =>
                 draft.Markdown.Contains("cloud://", StringComparison.OrdinalIgnoreCase) ||
                 draft.Fields.Values.Any(field => ContainsCloudReference(field.Value)))))
        {
            return;
        }

        var resolver = new CloudBaseFileResolver(
            client,
            _options.EnvId,
            _options.StorageBucket ?? string.Empty,
            _options.DefaultImageUrl,
            _options.SignExpiresSeconds);
        foreach (var draft in drafts)
        {
            foreach (var fileField in _options.FileFields)
            {
                if (draft.Fields.TryGetValue(fileField, out var field) &&
                    field.Value?.ToString() is { } value)
                {
                    if (resolver.TryAddFieldValue(value, out var objectName))
                    {
                        draft.FileObjects[fileField] = objectName;
                    }
                    else
                    {
                        draft.Fields[fileField] = new ContentField("text", resolver.Placeholder);
                    }
                }
            }

            resolver.CollectMarkdownReferences(draft.Markdown);
        }

        await resolver.SignAsync(cancellationToken).ConfigureAwait(false);
        foreach (var draft in drafts)
        {
            foreach (var (fileField, objectName) in draft.FileObjects)
            {
                draft.Fields[fileField] = new ContentField("text", resolver.Resolve(objectName));
            }

            foreach (var name in draft.Fields.Keys.Where(name =>
                !_options.FileFields.Contains(name, StringComparer.OrdinalIgnoreCase)).ToArray())
            {
                if (ContainsCloudReference(draft.Fields[name].Value))
                {
                    draft.Fields.Remove(name);
                    resolver.MarkUnsupported();
                }
            }

            draft.Markdown = resolver.RewriteMarkdown(draft.Markdown);
        }

        if (resolver.ResolveFailures > 0)
        {
            _logger?.Warn(
                $"event=cloudbase.file_resolve_failed code={DiagnosticCodeFormatter.Format(DiagnosticCode.CloudBaseFileResolveFailed)} count={resolver.ResolveFailures}");
        }

        if (resolver.UnsupportedReferences > 0)
        {
            _logger?.Warn(
                $"event=cloudbase.unsupported_file_reference code={DiagnosticCodeFormatter.Format(DiagnosticCode.CloudBaseUnsupportedFileReference)} count={resolver.UnsupportedReferences}");
        }
    }

    private static bool ContainsCloudReference(object? value)
        => value switch
        {
            string text => text.Contains("cloud://", StringComparison.OrdinalIgnoreCase),
            IEnumerable<string> items => items.Any(item =>
                item?.Contains("cloud://", StringComparison.OrdinalIgnoreCase) == true),
            _ => false
        };

    private sealed class Draft(
        string id,
        string title,
        string slug,
        DateTimeOffset? publishedAt,
        Dictionary<string, ContentField> fields,
        string markdown)
    {
        internal string Id { get; } = id;
        internal string Title { get; } = title;
        internal string Slug { get; } = slug;
        internal DateTimeOffset? PublishedAt { get; } = publishedAt;
        internal Dictionary<string, ContentField> Fields { get; } = fields;
        internal string Markdown { get; set; } = markdown;
        internal Dictionary<string, string> FileObjects { get; } = new(StringComparer.OrdinalIgnoreCase);
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
                MaxRetries = _options.MaxRetries,
                MaxResponseBytes = _options.MaxResponseBytes
            });

    private static string? GetString(JsonElement row, string field)
        => row.ValueKind == JsonValueKind.Object &&
            row.TryGetProperty(field, out var value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

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
            return epoch >= 100_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(epoch)
                : DateTimeOffset.FromUnixTimeSeconds(epoch);
        }

        return null;
    }
}
