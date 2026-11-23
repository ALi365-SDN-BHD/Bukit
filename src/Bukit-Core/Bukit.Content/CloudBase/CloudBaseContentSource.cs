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
        var documents = new List<RawContentDocument>(page.Rows.Count);
        var bodies = new Dictionary<string, string>(StringComparer.Ordinal);
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
            if (ParseDate(row, _options.UpdatedAtField) is { } updatedAt)
            {
                fields["last_edited_time"] = new ContentField("date", updatedAt);
            }

            var markdown = isData ? string.Empty : GetString(row, _options.BodyField) ?? string.Empty;
            if (!isData)
            {
                bodies[id] = markdown;
            }

            documents.Add(new RawContentDocument(
                SourceId: id,
                SourceKind: "cloudbase",
                Title: GetNonBlank(row, _options.TitleField) ?? id,
                Slug: GetNonBlank(row, _options.SlugField) ?? id,
                PublishedAt: ParseDate(row, _options.PublishAtField),
                Body: isData ? new RawBody() : new RawBody(BodyKey: id, Markdown: markdown),
                Properties: null,
                Source: new ContentSourceInfo(
                    "cloudbase",
                    SourcePath: _options.Table,
                    ExternalId: id,
                    SyncStatus: "loaded"),
                CustomFields: fields));
        }

        _logger?.Info($"event=cloudbase.stats table={_options.Table} count={documents.Count}");
        return new RawContentLoadResult(documents, new CloudBaseBodyStore(bodies));
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
