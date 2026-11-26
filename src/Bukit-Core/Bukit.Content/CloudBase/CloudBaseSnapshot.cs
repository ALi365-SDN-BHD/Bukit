using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Bukit.CloudBase.Models;
using Bukit.CloudBase.Transport;
using Bukit.Config;
using Bukit.Content.Media;
using Bukit.Content.Notion.Snapshot;
using Bukit.Shared;

namespace Bukit.Content.CloudBase;

/// <summary>
/// One CloudBase query in the content offline snapshot: the projected public rows the build read
/// and, per file object, its media identity (the signed URL without its token). An offline build
/// replays the same projection from the rows and finds the media files the recording build
/// localized by that identity, so it needs no key and no network.
/// </summary>
/// <remarks>
/// Rows keep only the mapped columns, the file fields and the columns the field policy publishes;
/// personal and operator columns and nested keys are never written, so widening the policy offline
/// cannot reveal them. Entries live at
/// <c>cloudbase/&lt;table key&gt;/&lt;shape key&gt;/&lt;filter key&gt;.json</c>, keyed by hashes.
/// The shape key covers the settings that decide which rows and columns are recorded (mapped
/// fields, file fields, field policy, sort, item limit and bucket), so two sources on one table
/// keep separate entries and a changed source fails offline. The filter key covers the resolved
/// filter value, so a table recorded for one release fails offline for another instead of
/// serving the recorded rows.
/// </remarks>
internal sealed class CloudBaseSnapshot : IDisposable
{
    /// <summary>The reason a file object has no recorded identity.</summary>
    internal const string NotRecordedError = "not-in-snapshot";

    private const int EntryFormat = 1;
    private const string Hint =
        "Run a build with CLOUDBASE_PUBLISHABLE_KEY and --content-snapshot (without --content-offline) to record it.";

    private readonly JsonDocument _document;
    private readonly Dictionary<string, string> _files;

    private CloudBaseSnapshot(
        JsonDocument document,
        IReadOnlyList<JsonElement> rows,
        Dictionary<string, string> files,
        ILookup<int, (string Field, string Problem)> rejected)
    {
        _document = document;
        Rows = rows;
        _files = files;
        Rejected = rejected;
    }

    /// <summary>The recorded rows, valid until this snapshot is disposed.</summary>
    internal IReadOnlyList<JsonElement> Rows { get; }

    /// <summary>The structured columns left out of each row (by row index) and why, reported again offline.</summary>
    internal ILookup<int, (string Field, string Problem)> Rejected { get; }

    /// <summary>
    /// Fails when the offline snapshot holds no query of this CloudBase source with these settings (for
    /// <c>config check</c>). The filter value is not needed, so <c>${env:NAME}</c> need not be set.
    /// </summary>
    internal static void EnsureSource(NotionSnapshotSession session, CloudBaseContentSourceOptions options)
    {
        if (session.Offline &&
            !session.HasEntryDirectory(ShapeDirectory(TableDirectory(options.EnvId, options.Table, options.FilterField), options)))
        {
            throw NotRecorded(session, options);
        }
    }

    /// <summary>Opens the recorded query for <paramref name="options"/>.</summary>
    internal static CloudBaseSnapshot Read(NotionSnapshotSession session, CloudBaseContentSourceOptions options)
    {
        var bytes = session.ReadEntry(EntryPath(options)) ?? throw NotRecorded(session, options);

        JsonDocument? document = null;
        try
        {
            document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("format", out var format) || !format.TryGetInt32(out var version) || version != EntryFormat ||
                !root.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array ||
                !root.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("The entry does not have the expected shape.");
            }

            // Only an identity the client could have recorded counts: an edited entry cannot publish other links.
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            var gateway = CloudBaseEndpoints.GatewayHost(options.EnvId, options.Region);
            foreach (var file in files.EnumerateObject())
            {
                if (options.StorageBucket is { Length: > 0 } bucket &&
                    file.Value.ValueKind == JsonValueKind.String &&
                    file.Value.GetString() is { } url &&
                    Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                    CloudBaseEndpoints.IsSignUrl(uri, gateway, bucket))
                {
                    map[file.Name] = url;
                }
            }

            var snapshot = new CloudBaseSnapshot(
                document,
                [.. rows.EnumerateArray()],
                map,
                ReadRejected(root));
            document = null;
            return snapshot;
        }
        catch (JsonException exception)
        {
            throw new NotionSnapshotException(
                $"Content offline snapshot has an unreadable entry for CloudBase table '{options.Table}'. {Hint}",
                exception,
                DiagnosticCode.NotionSnapshotEntryMissing);
        }
        finally
        {
            document?.Dispose();
        }
    }

    /// <summary>Reads the recorded rejections; an entry may hold none.</summary>
    private static ILookup<int, (string Field, string Problem)> ReadRejected(JsonElement root)
    {
        var rejected = new List<(int Row, string Field, string Problem)>();
        if (!root.TryGetProperty("rejected", out var items))
        {
            return rejected.ToLookup(item => item.Row, item => (item.Field, item.Problem));
        }

        if (items.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("The entry does not have the expected shape.");
        }

        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("row", out var row) || !row.TryGetInt32(out var index) ||
                !item.TryGetProperty("field", out var field) || field.ValueKind != JsonValueKind.String ||
                !item.TryGetProperty("problem", out var problem) || problem.ValueKind != JsonValueKind.String)
            {
                throw new JsonException("The entry does not have the expected shape.");
            }

            rejected.Add((index, field.GetString()!, problem.GetString()!));
        }

        return rejected.ToLookup(item => item.Row, item => (item.Field, item.Problem));
    }

    /// <summary>Answers a signing request from the recorded identities; an unrecorded object fails like an unavailable one.</summary>
    /// <remarks>Synchronous, so <paramref name="cancellationToken"/> is not observed.</remarks>
    internal Task<IReadOnlyList<CloudBaseSignedObject>> SignAsync(IReadOnlyList<string> objectNames, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<CloudBaseSignedObject>>([.. objectNames.Select(objectName => _files.TryGetValue(objectName, out var url)
            ? new CloudBaseSignedObject(objectName, url, null)
            : new CloudBaseSignedObject(objectName, null, NotRecordedError))]);

    /// <summary>
    /// Records one query: the rows reduced to what the projection may publish, and the identity of
    /// each signed file object. Signed URLs and their tokens are never written.
    /// </summary>
    internal static void Write(
        NotionSnapshotSession session,
        CloudBaseContentSourceOptions options,
        IReadOnlyList<JsonElement> rows,
        IEnumerable<CloudBaseSignedObject> signed)
    {
        var mapped = new HashSet<string>(
            [options.IdField, options.TitleField, options.SlugField, options.PublishAtField, options.UpdatedAtField, options.BodyField, .. options.FileFields],
            StringComparer.OrdinalIgnoreCase);
        var allowed = CloudBaseFieldProjector.BuildAllowedSet(options.FieldPolicyMode, options.AllowedFields);
        var all = string.Equals(options.FieldPolicyMode?.Trim(), "all", StringComparison.OrdinalIgnoreCase);

        var buffer = new MemoryStream();
        // The entry is never HTML: keep non-ASCII text as is, so its size matches the source row.
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("format", EntryFormat);
            writer.WriteString("table", options.Table);
            var rejected = new List<(int Row, string Field, string Problem)>();
            writer.WriteStartArray("rows");
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                writer.WriteStartObject();
                if (row.ValueKind == JsonValueKind.Object)
                {
                    foreach (var column in row.EnumerateObject())
                    {
                        var published = !CloudBaseFieldRules.IsHardExcluded(column.Name) && (all || allowed!.Contains(column.Name));
                        if (!mapped.Contains(column.Name) &&
                            published &&
                            CloudBaseFieldProjector.TryGetStructuredRejection(column.Name, column.Value, out var problem))
                        {
                            // Nested personal keys are left out below, which could make the value valid offline.
                            rejected.Add((index, column.Name, problem));
                            continue;
                        }

                        if (mapped.Contains(column.Name) || published)
                        {
                            writer.WritePropertyName(column.Name);
                            WritePublic(writer, column.Value);
                        }
                    }
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("rejected");
            foreach (var (row, field, problem) in rejected)
            {
                writer.WriteStartObject();
                writer.WriteNumber("row", row);
                writer.WriteString("field", field);
                writer.WriteString("problem", problem);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartObject("files");
            foreach (var item in signed)
            {
                if (item.Url is { } url && Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    writer.WriteString(item.ObjectName, SignedUrlIdentity.IdentityTarget(uri));
                }
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        session.RecordEntry(EntryPath(options), buffer.ToArray());
    }

    public void Dispose() => _document.Dispose();

    /// <summary>Writes a value without the personal and operator keys of nested objects.</summary>
    private static void WritePublic(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject())
                {
                    if (!CloudBaseFieldRules.IsHardExcluded(property.Name))
                    {
                        writer.WritePropertyName(property.Name);
                        WritePublic(writer, property.Value);
                    }
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    WritePublic(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                value.WriteTo(writer);
                break;
        }
    }

    private static NotionSnapshotException Missing(string table)
        => new($"Content offline snapshot has no data for CloudBase table '{table}'. {Hint}", DiagnosticCode.NotionSnapshotDatabaseMissing);

    /// <summary>The query was not recorded: say whether the table, the source settings or the filter value differ.</summary>
    private static NotionSnapshotException NotRecorded(NotionSnapshotSession session, CloudBaseContentSourceOptions options)
    {
        var table = TableDirectory(options.EnvId, options.Table, options.FilterField);
        if (!session.HasEntryDirectory(table))
        {
            return Missing(options.Table);
        }

        var difference = session.HasEntryDirectory(ShapeDirectory(table, options))
            ? "another filter value"
            : "different source settings (mode, mapped fields, file fields, field policy, sort, maxItems or bucket)";
        return new NotionSnapshotException(
            $"Content offline snapshot recorded CloudBase table '{options.Table}' with {difference}. {Hint}",
            DiagnosticCode.NotionSnapshotQueryMismatch);
    }

    private static string TableDirectory(string envId, string table, string? filterField)
        => Path.Combine("cloudbase", Hash($"{envId}\n{table}\n{filterField}"));

    /// <summary>
    /// Hashes the settings that decide which rows, columns and files a query records. The allowed list is
    /// matched ignoring order, so it is hashed that way; the mode decides whether body
    /// images are signed. In all mode only the id publication is hashed from the allowed list.
    /// </summary>
    private static string ShapeDirectory(string tableDirectory, CloudBaseContentSourceOptions options)
    {
        var all = string.Equals(options.FieldPolicyMode?.Trim(), "all", StringComparison.OrdinalIgnoreCase);
        var shape = string.Join(
            '\n',
            options.Mode.Trim().ToLowerInvariant(),
            options.IdField,
            options.TitleField,
            options.SlugField,
            options.PublishAtField,
            options.UpdatedAtField,
            options.BodyField,
            // Every file field is read by its exact name, a later one overriding an earlier one: kept as given.
            string.Join('\u001f', options.FileFields.Select(name => name.Trim())),
            all ? "ALL" : "WHITELIST",
            all ? PublishesId(options) : NameSet(options.AllowedFields),
            options.SortField,
            options.SortDirection,
            options.MaxItems?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            options.StorageBucket);
        return Path.Combine(tableDirectory, Hash(shape));
    }

    /// <summary>In all mode the allowed list only decides whether the id column is published.</summary>
    private static string PublishesId(CloudBaseContentSourceOptions options)
        => options.AllowedFields?.Contains(options.IdField, StringComparer.OrdinalIgnoreCase) is true ? "ID" : string.Empty;

    /// <summary>
    /// The columns a list selects, hashed ignoring order. Columns are selected by their exact names,
    /// and of names differing only in case the query keeps the first, so that spelling is hashed.
    /// </summary>
    private static string NameSet(IEnumerable<string>? names)
        => string.Join('\u001f', (names ?? []).Select(name => name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal));

    private static string EntryPath(CloudBaseContentSourceOptions options)
        => Path.Combine(
            ShapeDirectory(TableDirectory(options.EnvId, options.Table, options.FilterField), options),
            Hash(options.FilterValue ?? string.Empty) + ".json");

    private static string Hash(string text)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)), 0, 16);
}
