using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bukit.Shared;
using Bukit.Config;

namespace Bukit.Content.Notion.Snapshot;

/// <summary>
/// A Notion offline snapshot: the raw Notion API data of one formal build plus its
/// rendered page bodies, so a later build can run with <c>--notion-offline</c> and no token.
/// <para>
/// Layout (all under the snapshot directory; directories 0700 and files 0600 on Unix):
/// <c>manifest.json</c> (format, Bukit version, databases, time);
/// <c>api/databases/&lt;id&gt;/schema.json</c> (raw <c>GET /databases/{id}</c>);
/// <c>api/databases/&lt;id&gt;/queries/&lt;fingerprint&gt;/request.json</c> and one file per
/// result page (raw <c>POST /databases/{id}/query</c> body with Notion's server time);
/// <c>api/pages/&lt;id&gt;.json</c> (raw <c>GET /pages/{id}</c>: relation targets, the
/// cross-source fallback and <c>pages_index</c>); <c>api/**/&lt;id&gt;.error.json</c>
/// (a recorded 403/404 answer); <c>cache/pages/&lt;id&gt;.json</c> (rendered bodies).
/// </para>
/// <para>
/// Writing happens in a sibling staging directory; <see cref="Commit"/> replaces the
/// snapshot only after the build succeeded, so a failed build keeps the previous one.
/// The token is never written: only response bodies, query bodies and server times are.
/// </para>
/// </summary>
internal sealed class NotionSnapshotSession : INotionSnapshotHandle, IDisposable
{
    /// <summary>
    /// The snapshot layout and the meaning of its entries that this Bukit reads. Bump it when an
    /// existing entry's meaning changes, when <c>NotionApiUrls.NotionVersion</c> changes, or when
    /// the snapshot directory layout changes. Adding an entry that older snapshots lack does not
    /// need a bump: reading it fails with a clear missing-entry error.
    /// </summary>
    public const int FormatVersion = 1;
    internal const string ManifestFileName = "manifest.json";
    private const string ApiDir = "api";
    private const string CacheDirName = "cache";
    private const string FirstCursorKey = "first";
    private const UnixFileMode DirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode FileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private static readonly JsonWriterOptions s_writerOptions = new() { Indented = false };

    private readonly string? _targetDirectory;
    private readonly bool _ownsStaging;
    private readonly ConcurrentDictionary<string, byte> _usedBodies = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _queryChangeWarned = new(StringComparer.OrdinalIgnoreCase);
    private int _state; // 0 open, 1 committing or committed, 2 disposed
    private bool _committed;

    private NotionSnapshotSession(string directory, bool offline, string? targetDirectory, bool ownsStaging, NotionSnapshotManifest? manifest)
    {
        Directory = directory;
        Offline = offline;
        _targetDirectory = targetDirectory;
        _ownsStaging = ownsStaging;
        Manifest = manifest;
    }

    /// <inheritdoc />
    public string Directory { get; }

    /// <inheritdoc />
    public bool Offline { get; }

    /// <summary>The snapshot selected for the current command, if any.</summary>
    public static NotionSnapshotSession? Current => NotionSnapshotScope.Current as NotionSnapshotSession;

    /// <summary>The manifest read when the snapshot was opened offline.</summary>
    public NotionSnapshotManifest? Manifest { get; }

    /// <summary>The Bukit identity recorded in every snapshot for diagnosis; it is not a validation key.</summary>
    public static string BukitVersion { get; } =
        typeof(NotionSnapshotSession).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
        typeof(NotionSnapshotSession).Assembly.GetName().Version?.ToString() ??
        "unknown";

    /// <summary>Test seam: replaces <see cref="BukitVersion"/> for sessions opened in this async flow.</summary>
    internal static readonly AsyncLocal<string?> BukitVersionOverride = new();

    private static string CurrentBukitVersion => BukitVersionOverride.Value ?? BukitVersion;

    /// <summary>Body cache root handed to <see cref="NotionContentSourceOptions.CacheDir"/>.</summary>
    public string CacheDirectory => Path.Combine(Directory, CacheDirName);

    /// <summary>
    /// Starts recording a new snapshot for <paramref name="targetDirectory"/>. Rendered bodies of
    /// the previous snapshot (same format) seed the staging directory so unchanged pages are not
    /// fetched again; bodies this build does not use are dropped on commit.
    /// </summary>
    public static NotionSnapshotSession BeginWrite(string targetDirectory)
    {
        var target = NormalizeTarget(targetDirectory);
        var parent = Path.GetDirectoryName(target)!;
        var staging = Path.Combine(parent, $".{Path.GetFileName(target)}.staging-{Guid.NewGuid():N}");
        try
        {
            CreatePrivateDirectory(parent);
            RemoveAbandonedSiblings(target);
            CreatePrivateDirectory(staging);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // .NET I/O messages carry the path; the snapshot location is never printed.
            throw new NotionSnapshotException(
                "Notion snapshot directory could not be created.",
                exception,
                DiagnosticCode.NotionSnapshotWriteFailed);
        }
        try
        {
            SeedBodies(target, staging);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Seeding is an optimization; a fresh snapshot is always correct.
        }

        return new NotionSnapshotSession(staging, offline: false, target, ownsStaging: true, manifest: null);
    }

    /// <summary>Opens a committed snapshot for an offline build and validates its manifest.</summary>
    public static NotionSnapshotSession OpenOffline(string directory)
    {
        var target = NormalizeTarget(directory);
        var manifest = ReadValidManifest(target);
        if (manifest is null)
        {
            var interrupted = HasInterruptedCommitBackup(target);
            throw new NotionSnapshotException(
                interrupted
                    ? $"Notion offline snapshot is missing or unreadable; an interrupted commit left a backup next to it. Restore the backup or record again. {NotionSnapshotException.RebuildHint}"
                    : $"Notion offline snapshot not found or incomplete. {NotionSnapshotException.RebuildHint}",
                DiagnosticCode.NotionSnapshotUnavailable);
        }

        return new NotionSnapshotSession(target, offline: true, targetDirectory: null, ownsStaging: false, manifest);
    }

    /// <summary>
    /// Attaches a multilingual build worker to the snapshot its parent opened: the committed
    /// snapshot (offline) or the parent's staging directory (recording; the parent commits).
    /// </summary>
    public static NotionSnapshotSession AttachWorker(string directory, bool offline)
        => offline
            ? OpenOffline(directory)
            : new NotionSnapshotSession(Path.GetFullPath(directory), offline: false, targetDirectory: null, ownsStaging: false, manifest: null);

    /// <summary>Database ids (normalized) the committed snapshot holds query results for.</summary>
    public bool ContainsDatabase(string databaseId)
        => Manifest?.Databases.Contains(NormalizeId(databaseId), StringComparer.Ordinal) == true;

    /// <summary>
    /// Fails when an offline build would need a database the snapshot does not hold
    /// (a changed <c>databaseId</c> or a new Notion source).
    /// </summary>
    public void EnsureDatabase(string databaseId)
    {
        if (!Offline || ContainsDatabase(databaseId))
        {
            return;
        }

        throw new NotionSnapshotException(
            $"Notion offline snapshot has no data for database '{databaseId}' (a changed databaseId or a new Notion source). " +
            "Apply the change and run a build with NOTION_TOKEN and --notion-snapshot to record it.",
            DiagnosticCode.NotionSnapshotDatabaseMissing);
    }

    // ---- recording -------------------------------------------------------------------

    internal void RecordDatabaseSchema(string databaseId, JsonElement body)
        => WriteRaw(DatabaseSchemaPath(databaseId), body);

    internal void RecordPage(string pageId, JsonElement body)
        => WriteRaw(PagePath(pageId), body);

    internal void RecordNotFound(string getPath, int statusCode)
    {
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, s_writerOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("status", statusCode);
            writer.WriteEndObject();
        }

        if (ErrorPath(getPath) is { } path)
        {
            WriteBytes(path, buffer.ToArray());
        }
    }

    internal void RecordQuery(string databaseId, string requestJson, DateTimeOffset serverTime, JsonElement body)
    {
        var (fingerprint, startCursor) = Fingerprint(requestJson);
        var queryDir = QueryDirectory(databaseId, FingerprintKey(fingerprint));
        WriteBytes(Path.Combine(queryDir, "request.json"), Encoding.UTF8.GetBytes(fingerprint));
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, s_writerOptions))
        {
            writer.WriteStartObject();
            if (startCursor is null)
            {
                writer.WriteNull("startCursor");
            }
            else
            {
                writer.WriteString("startCursor", startCursor);
            }

            writer.WriteString("serverTime", serverTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            writer.WritePropertyName("response");
            body.WriteTo(writer);
            writer.WriteEndObject();
        }

        WriteBytes(Path.Combine(queryDir, CursorKey(startCursor) + ".json"), buffer.ToArray());
    }

    /// <summary>Marks a rendered body as used by this build so commit keeps it.</summary>
    internal void MarkBodyUsed(string pageId) => _usedBodies.TryAdd(pageId, 0);

    /// <summary>
    /// Publishes the staging directory as the snapshot: writes the manifest, drops bodies this
    /// build did not use, applies private permissions, then swaps the directories.
    /// </summary>
    public void Commit()
    {
        if (Offline || !_ownsStaging || _targetDirectory is null)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
        {
            throw new InvalidOperationException("Notion snapshot session is already closed.");
        }

        try
        {
            PruneUnusedBodies();
            RemoveTransientFiles(Directory);
            WriteManifest();
            ApplyPrivatePermissions(Directory);
            Swap(Directory, _targetDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new NotionSnapshotException(
                "Notion snapshot could not be saved; the previous snapshot is kept.",
                exception,
                DiagnosticCode.NotionSnapshotWriteFailed);
        }

        _committed = true;
    }

    /// <summary>Discards the staging directory unless <see cref="Commit"/> published it.</summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref _state, 2);
        if (_ownsStaging && !_committed)
        {
            TryDeleteDirectory(Directory);
        }
    }

    // ---- reading ---------------------------------------------------------------------

    internal JsonDocument ReadDatabaseSchema(string databaseId)
    {
        ThrowIfRecordedFailure(NotionApiPath.Database(databaseId));
        return ReadJson(DatabaseSchemaPath(databaseId), $"schema for database '{databaseId}'");
    }

    internal JsonDocument ReadPage(string pageId)
    {
        ThrowIfRecordedFailure(NotionApiPath.Page(pageId));
        return ReadJson(PagePath(pageId), $"page '{pageId}'");
    }

    /// <summary>
    /// Returns the recorded query page for <paramref name="requestJson"/>. A changed filter or
    /// sort still answers from the formal build's results and logs one warning per database.
    /// </summary>
    internal (JsonDocument Document, DateTimeOffset ServerTime) ReadQuery(string databaseId, string requestJson, ILogger? logger)
    {
        var databaseRoot = Path.Combine(Directory, ApiDir, "databases", NormalizeId(databaseId), "queries");
        if (!System.IO.Directory.Exists(databaseRoot))
        {
            EnsureDatabase(databaseId);
            throw NotionSnapshotException.Missing($"query results for database '{databaseId}'");
        }

        var (fingerprint, startCursor) = Fingerprint(requestJson);
        var queryDir = Path.Combine(databaseRoot, FingerprintKey(fingerprint));
        if (!System.IO.Directory.Exists(queryDir))
        {
            var recorded = System.IO.Directory.GetDirectories(databaseRoot);
            if (recorded.Length != 1)
            {
                throw new NotionSnapshotException(
                    $"Notion offline snapshot holds {recorded.Length} different queries for database '{databaseId}' and none matches the current filter and sort. " +
                    NotionSnapshotException.RebuildHint,
                    DiagnosticCode.NotionSnapshotQueryMismatch);
            }

            queryDir = recorded[0];
            if (_queryChangeWarned.TryAdd(NormalizeId(databaseId), 0))
            {
                logger?.Warn(
                    $"event=notion.snapshot.query_changed databaseId={databaseId} " +
                    "message=The Notion filter or sort differs from the formal build; the offline build uses the pages the formal build queried.");
            }
        }

        using var document = ReadJson(Path.Combine(queryDir, CursorKey(startCursor) + ".json"),
            $"query page{(startCursor is null ? string.Empty : " after cursor")} for database '{databaseId}'");
        var root = document.RootElement;
        if (!root.TryGetProperty("response", out var response) || response.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("serverTime", out var serverTimeElement) || serverTimeElement.ValueKind != JsonValueKind.String ||
            !DateTimeOffset.TryParse(serverTimeElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var serverTime))
        {
            throw new NotionSnapshotException(
                $"Notion offline snapshot has an unreadable query page for database '{databaseId}'. {NotionSnapshotException.RebuildHint}",
                DiagnosticCode.NotionSnapshotEntryMissing);
        }

        return (JsonDocument.Parse(response.GetRawText()), serverTime);
    }

    // ---- helpers ---------------------------------------------------------------------

    /// <summary>
    /// Path-safe id: Notion ids without dashes, lowercased; anything else is hashed so a
    /// configured value can never address a path outside the snapshot.
    /// </summary>
    internal static string NormalizeId(string id)
    {
        var compact = (id ?? string.Empty).Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        if (compact.Length is > 0 and <= 64 && compact.All(static c => c is >= 'a' and <= 'z' or >= '0' and <= '9'))
        {
            return compact;
        }

        return "h" + Hash(id ?? string.Empty);
    }

    private string DatabaseSchemaPath(string databaseId)
        => Path.Combine(Directory, ApiDir, "databases", NormalizeId(databaseId), "schema.json");

    private string PagePath(string pageId)
        => Path.Combine(Directory, ApiDir, "pages", NormalizeId(pageId) + ".json");

    private string QueryDirectory(string databaseId, string fingerprintKey)
        => Path.Combine(Directory, ApiDir, "databases", NormalizeId(databaseId), "queries", fingerprintKey);

    private string? ErrorPath(string getPath) => getPath switch
    {
        _ when NotionApiPath.TryParseDatabase(getPath, out var id) => Path.Combine(Directory, ApiDir, "databases", NormalizeId(id), "schema.error.json"),
        _ when NotionApiPath.TryParsePage(getPath, out var id) => Path.Combine(Directory, ApiDir, "pages", NormalizeId(id) + ".error.json"),
        _ => null
    };

    private void ThrowIfRecordedFailure(string getPath)
    {
        var path = ErrorPath(getPath);
        if (path is null || !File.Exists(path))
        {
            return;
        }

        int status;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            status = document.RootElement.TryGetProperty("status", out var value) && value.TryGetInt32(out var parsed) ? parsed : 404;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new NotionSnapshotException(
                $"Notion offline snapshot has an unreadable entry. {NotionSnapshotException.RebuildHint}",
                exception,
                DiagnosticCode.NotionSnapshotEntryMissing);
        }

        throw new NotionSnapshotRecordedFailure(status);
    }

    private static JsonDocument ReadJson(string path, string what)
    {
        if (!File.Exists(path))
        {
            throw NotionSnapshotException.Missing(what);
        }

        try
        {
            return JsonDocument.Parse(File.ReadAllBytes(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new NotionSnapshotException(
                $"Notion offline snapshot has an unreadable {what}. {NotionSnapshotException.RebuildHint}",
                exception,
                DiagnosticCode.NotionSnapshotEntryMissing);
        }
    }

    /// <summary>
    /// The query body without paging (<c>start_cursor</c>, <c>page_size</c>): identical filters
    /// and sorts give identical fingerprints, whatever page is requested.
    /// </summary>
    internal static (string Fingerprint, string? StartCursor) Fingerprint(string requestJson)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(requestJson) ? "{}" : requestJson);
        string? startCursor = null;
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, s_writerOptions))
        {
            writer.WriteStartObject();
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.NameEquals("start_cursor"))
                    {
                        startCursor = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                        continue;
                    }

                    if (property.NameEquals("page_size"))
                    {
                        continue;
                    }

                    property.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        return (Encoding.UTF8.GetString(buffer.ToArray()), string.IsNullOrEmpty(startCursor) ? null : startCursor);
    }

    private static string FingerprintKey(string fingerprint) => Hash(fingerprint);

    private static string CursorKey(string? startCursor)
        => startCursor is null ? FirstCursorKey : "c" + Hash(startCursor);

    private static string Hash(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)), 0, 16).ToLowerInvariant();

    private void WriteRaw(string path, JsonElement body)
        => WriteBytes(path, Encoding.UTF8.GetBytes(body.GetRawText()));

    private void WriteBytes(string path, byte[] bytes)
    {
        if (Offline)
        {
            throw new InvalidOperationException("An offline Notion snapshot is read-only.");
        }

        try
        {
            CreatePrivateDirectory(Path.GetDirectoryName(path)!);
            AtomicNotionCacheWriter.WriteJsonAsync(path, bytes, CancellationToken.None).GetAwaiter().GetResult();
            SetFileMode(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Fails the build: a snapshot missing what the site used must never be committed,
            // and catch-all warnings in relation or taxonomy code would otherwise swallow it.
            throw new NotionSnapshotException(
                "Notion snapshot entry could not be written.",
                exception,
                DiagnosticCode.NotionSnapshotWriteFailed);
        }
    }

    private void WriteManifest()
    {
        var databases = new List<string>();
        var databasesRoot = Path.Combine(Directory, ApiDir, "databases");
        if (System.IO.Directory.Exists(databasesRoot))
        {
            foreach (var database in System.IO.Directory.GetDirectories(databasesRoot))
            {
                if (System.IO.Directory.Exists(Path.Combine(database, "queries")))
                {
                    databases.Add(Path.GetFileName(database));
                }
            }
        }

        databases.Sort(StringComparer.Ordinal);
        var manifest = new NotionSnapshotManifest(FormatVersion, CurrentBukitVersion, DateTimeOffset.UtcNow, databases);
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("format", manifest.Format);
            writer.WriteString("bukitVersion", manifest.BukitVersion);
            writer.WriteString("createdAt", manifest.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteStartArray("databases");
            foreach (var database in manifest.Databases)
            {
                writer.WriteStringValue(database);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        WriteBytes(Path.Combine(Directory, ManifestFileName), buffer.ToArray());
    }

    private static NotionSnapshotManifest? ReadValidManifest(string directory)
    {
        var path = Path.Combine(directory, ManifestFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        NotionSnapshotManifest manifest;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;
            var format = root.TryGetProperty("format", out var f) && f.TryGetInt32(out var parsedFormat) ? parsedFormat : 0;
            var version = root.TryGetProperty("bukitVersion", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
            var createdAt = root.TryGetProperty("createdAt", out var c) && c.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(c.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedCreated)
                ? parsedCreated
                : default;
            var databases = root.TryGetProperty("databases", out var d) && d.ValueKind == JsonValueKind.Array
                ? d.EnumerateArray().Where(static x => x.ValueKind == JsonValueKind.String).Select(static x => x.GetString()!).ToArray()
                : [];
            manifest = new NotionSnapshotManifest(format, version, createdAt, databases);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new NotionSnapshotException(
                $"Notion offline snapshot manifest is unreadable. {NotionSnapshotException.RebuildHint}",
                exception,
                DiagnosticCode.NotionSnapshotUnavailable);
        }

        if (manifest.Format != FormatVersion)
        {
            throw new NotionSnapshotException(
                $"Notion offline snapshot format {manifest.Format} does not match the format {FormatVersion} this Bukit reads. {NotionSnapshotException.RebuildHint}",
                DiagnosticCode.NotionSnapshotUnavailable);
        }

        return manifest;
    }

    /// <summary>
    /// Removes leftovers of earlier runs next to <paramref name="target"/>: replaced snapshots
    /// whose deletion failed, and staging directories of runs that died more than a day ago.
    /// A target without a manifest (a partial restore or a lost file) is not a committed
    /// snapshot; keep <c>.old-*</c> because an interrupted commit may have left the only good copy.
    /// </summary>
    private static void RemoveAbandonedSiblings(string target)
    {
        var parent = Path.GetDirectoryName(target)!;
        var name = Path.GetFileName(target);
        try
        {
            if (File.Exists(Path.Combine(target, ManifestFileName)))
            {
                foreach (var old in System.IO.Directory.EnumerateDirectories(parent, $".{name}.old-*"))
                {
                    TryDeleteDirectory(old);
                }
            }

            foreach (var staging in System.IO.Directory.EnumerateDirectories(parent, $".{name}.staging-*"))
            {
                if (System.IO.Directory.GetLastWriteTimeUtc(staging) < DateTime.UtcNow.AddDays(-1))
                {
                    TryDeleteDirectory(staging);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static bool HasInterruptedCommitBackup(string target)
    {
        var parent = Path.GetDirectoryName(target);
        if (parent is null)
        {
            return false;
        }

        try
        {
            return System.IO.Directory
                .EnumerateDirectories(parent, $".{Path.GetFileName(target)}.old-*")
                .Any();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void SeedBodies(string target, string staging)
    {
        if (!System.IO.Directory.Exists(target))
        {
            return;
        }

        try
        {
            _ = ReadValidManifest(target) ?? throw new InvalidDataException();
        }
        catch (Exception exception) when (exception is NotionSnapshotException or InvalidDataException)
        {
            return;
        }

        var source = Path.Combine(target, CacheDirName, "pages");
        if (!System.IO.Directory.Exists(source))
        {
            return;
        }

        var destination = Path.Combine(staging, CacheDirName, "pages");
        CreatePrivateDirectory(destination);
        foreach (var file in System.IO.Directory.EnumerateFiles(source, "*.json"))
        {
            var name = Path.GetFileName(file);
            if (name.StartsWith('.'))
            {
                continue;
            }

            var copy = Path.Combine(destination, name);
            File.Copy(file, copy, overwrite: true);
            SetFileMode(copy);
        }
    }

    private void PruneUnusedBodies()
    {
        var pages = Path.Combine(Directory, CacheDirName, "pages");
        if (!System.IO.Directory.Exists(pages))
        {
            return;
        }

        foreach (var file in System.IO.Directory.EnumerateFiles(pages, "*.json"))
        {
            var pageId = Path.GetFileNameWithoutExtension(file);
            if (!_usedBodies.ContainsKey(pageId))
            {
                File.Delete(file);
            }
        }
    }

    private static void RemoveTransientFiles(string root)
    {
        foreach (var file in System.IO.Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(".tmp", StringComparison.Ordinal) || name.EndsWith(".lock", StringComparison.Ordinal))
            {
                File.Delete(file);
            }
        }
    }

    private static void Swap(string staging, string target)
    {
        var parent = Path.GetDirectoryName(target)!;
        string? previous = null;
        if (System.IO.Directory.Exists(target))
        {
            previous = Path.Combine(parent, $".{Path.GetFileName(target)}.old-{Guid.NewGuid():N}");
            System.IO.Directory.Move(target, previous);
        }

        try
        {
            System.IO.Directory.Move(staging, target);
        }
        catch
        {
            if (previous is not null && !System.IO.Directory.Exists(target))
            {
                System.IO.Directory.Move(previous, target);
            }

            throw;
        }

        if (previous is not null)
        {
            TryDeleteDirectory(previous);
        }
    }

    private static string NormalizeTarget(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new NotionSnapshotException(
                "--notion-snapshot needs a directory.",
                DiagnosticCode.NotionSnapshotOptionInvalid);
        }

        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory.Trim()));
        if (Path.GetDirectoryName(full) is null || string.IsNullOrEmpty(Path.GetFileName(full)))
        {
            throw new NotionSnapshotException(
                "--notion-snapshot cannot be a filesystem root.",
                DiagnosticCode.NotionSnapshotOptionInvalid);
        }

        return full;
    }

    private static void CreatePrivateDirectory(string path)
    {
        if (System.IO.Directory.Exists(path))
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            System.IO.Directory.CreateDirectory(path);
        }
        else
        {
            System.IO.Directory.CreateDirectory(path, DirectoryMode);
        }
    }

    private static void SetFileMode(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, FileMode);
        }
    }

    private static void ApplyPrivatePermissions(string root)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(root, DirectoryMode);
        foreach (var directory in System.IO.Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            File.SetUnixFileMode(directory, DirectoryMode);
        }

        foreach (var file in System.IO.Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            File.SetUnixFileMode(file, FileMode);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (System.IO.Directory.Exists(path))
            {
                System.IO.Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>Identity of a committed Notion offline snapshot.</summary>
internal sealed record NotionSnapshotManifest(
    int Format,
    string BukitVersion,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> Databases);

/// <summary>A 403/404 answer the formal build recorded; replayed as the same Notion error.</summary>
internal sealed class NotionSnapshotRecordedFailure(int statusCode) : Exception($"Notion request failed: {statusCode} (recorded in the offline snapshot)")
{
    internal int StatusCode { get; } = statusCode;
}

/// <summary>Parses the Notion read endpoints a snapshot stores.</summary>
internal static class NotionApiPath
{
    private const string Prefix = "/v1/";

    internal static string Database(string id) => $"/v1/databases/{id}";

    internal static string Page(string id) => $"/v1/pages/{id}";

    internal static string? PathOf(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : null;

    internal static bool TryParseDatabase(string path, out string id) => TryParseSingle(path, "databases/", out id);

    internal static bool TryParsePage(string path, out string id) => TryParseSingle(path, "pages/", out id);

    internal static bool TryParseDatabaseQuery(string path, out string id)
    {
        id = string.Empty;
        if (!path.StartsWith(Prefix + "databases/", StringComparison.Ordinal) || !path.EndsWith("/query", StringComparison.Ordinal))
        {
            return false;
        }

        var middle = path[(Prefix.Length + "databases/".Length)..^"/query".Length];
        if (middle.Length == 0 || middle.Contains('/', StringComparison.Ordinal))
        {
            return false;
        }

        id = Uri.UnescapeDataString(middle);
        return true;
    }

    private static bool TryParseSingle(string path, string kind, out string id)
    {
        id = string.Empty;
        if (!path.StartsWith(Prefix + kind, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = path[(Prefix.Length + kind.Length)..];
        if (rest.Length == 0 || rest.Contains('/', StringComparison.Ordinal))
        {
            return false;
        }

        id = Uri.UnescapeDataString(rest);
        return true;
    }
}
