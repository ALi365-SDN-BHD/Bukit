using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed record I18nContentSnapshot(
    string Schema,
    string Head,
    DateTimeOffset BuildStartedAt,
    IReadOnlyList<ContentDocument> Documents,
    IReadOnlyDictionary<string, ContentBody> Bodies,
    IReadOnlyDictionary<string, I18nBodyFileSnapshot> BodyFiles,
    IReadOnlyList<ContentValidationIssue> SchemaErrors,
    BodyCacheMetrics? BodyCacheMetrics);

internal sealed record I18nBodyFileSnapshot(string Path, string Sha256);

internal sealed record I18nWorkerRequest(
    string Schema,
    string Head,
    string ConfigHash,
    string ContentHash,
    string Language,
    string LogFormat,
    string ConfigPath,
    string RootDir,
    string ProjectRootDir,
    AppConfig Config,
    ConfigOverrides Overrides,
    string OutputDir,
    string CacheDir,
    string AssetsDir,
    string? ScssOutputDir,
    string MediaDownloadDir,
    string SnapshotPath,
    string ResultPath);

internal sealed record I18nDerivedRouteSnapshot(RouteInfo Route, DateTimeOffset LastModified);

internal sealed record I18nAssetOutputSnapshot(
    string Source,
    string Destination,
    AssetOutputCategory Category,
    AssetOutputOperation Operation);

internal sealed record I18nBuildManifestSnapshot(
    int Version,
    string OutputRoot,
    IReadOnlyList<string> OwnedOutputs,
    string TemplateHash,
    IReadOnlyDictionary<string, BuildManifestEntry> Entries,
    IReadOnlyDictionary<string, string> Media,
    IReadOnlyDictionary<string, string> Assets,
    IReadOnlyDictionary<string, string> Static,
    IReadOnlyDictionary<string, PluginOutputManifestEntry> PluginOutputs);

internal sealed record I18nVariantSnapshot(
    string Schema,
    string Head,
    string ConfigHash,
    string ContentHash,
    string Language,
    bool Completed,
    string OutputDir,
    string BaseUrl,
    bool SearchSnippetsEnabled,
    IReadOnlyDictionary<string, ContentBody> Bodies,
    IReadOnlyList<I18nDerivedRouteSnapshot> DerivedRoutes,
    IReadOnlyDictionary<string, SeoIndexEntry> SeoIndex,
    IReadOnlyDictionary<string, SeoModel> SeoModels,
    IReadOnlyList<PluginExecutionInfo> PluginExecutions,
    int RenderedCount,
    int SkippedCount,
    IReadOnlyDictionary<string, int> RenderReasons,
    BuildStageMetrics StageMetrics,
    IReadOnlyList<RoutedContentDocument> RoutedDocuments,
    IReadOnlyList<ListRoutePlan> ListRoutes,
    IReadOnlyList<RoutedContentDocument> DerivedDocuments,
    IReadOnlyList<PublishProjectionResult> ProjectionResults,
    IReadOnlyList<RouteInfo> StaticRoutes,
    IReadOnlyList<PluginOutputTrackingInfo> PluginOutputs,
    I18nBuildManifestSnapshot Manifest,
    string TemplateHash,
    bool IncrementalEnabled,
    IReadOnlyList<I18nAssetOutputSnapshot> PlannedOutputs,
    int WarningCount,
    int ErrorCount);

internal static class I18nWorktreeProtocol
{
    internal const string ContentSchema = "i18n-content-snapshot.v1";
    internal const string RequestSchema = "i18n-worker-request.v1";
    internal const string ResultSchema = "i18n-variant-result.v1";

    internal static string ComputeConfigHash(AppConfig config, ConfigOverrides overrides)
    {
        var identity = new I18nConfigIdentity(
            config,
            overrides.ExecutionMode,
            overrides.IsCI,
            overrides.Incremental,
            overrides.Jobs);
        return HashUtil.Sha256Hex(JsonSerializer.SerializeToUtf8Bytes(
            identity,
            I18nWorktreeJsonContext.Default.I18nConfigIdentity));
    }

    internal static async Task<I18nContentSnapshot> CaptureContentAsync(
        string head,
        DateTimeOffset buildStartedAt,
        ContentPipelineResult content,
        string sourceRoot,
        string identityRoot,
        CancellationToken cancellationToken)
    {
        var bodies = new Dictionary<string, ContentBody>(StringComparer.Ordinal);
        foreach (var document in content.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (document.Body.Html is null && document.Body.BodyKey is { Length: > 0 } key)
            {
                bodies[MapPathIdentity(key, sourceRoot, identityRoot)] =
                    await content.BodyStore.GetAsync(document, cancellationToken).ConfigureAwait(false);
            }
        }

        var documents = content.Documents
            .Select(document => CanonicalizeDocumentPaths(document, sourceRoot, identityRoot))
            .ToArray();

        return new I18nContentSnapshot(
            ContentSchema,
            head,
            buildStartedAt,
            documents,
            bodies,
            new Dictionary<string, I18nBodyFileSnapshot>(StringComparer.Ordinal),
            content.SchemaErrors,
            content.BodyCacheMetrics);
    }

    private static ContentDocument CanonicalizeDocumentPaths(
        ContentDocument document,
        string sourceRoot,
        string identityRoot)
    {
        IReadOnlyDictionary<string, ContentField>? fields = document.CustomFields;
        if (fields is not null && fields.TryGetValue("sourcePath", out var sourcePathField) &&
            sourcePathField.Value is string sourcePath)
        {
            var mapped = MapPathIdentity(sourcePath, sourceRoot, identityRoot);
            if (!string.Equals(mapped, sourcePath, StringComparison.Ordinal))
            {
                var updated = new Dictionary<string, ContentField>(fields, StringComparer.OrdinalIgnoreCase)
                {
                    ["sourcePath"] = sourcePathField with { Value = mapped }
                };
                fields = updated;
            }
        }

        return document with
        {
            Body = document.Body with
            {
                BodyKey = document.Body.BodyKey is { Length: > 0 } key
                    ? MapPathIdentity(key, sourceRoot, identityRoot)
                    : document.Body.BodyKey
            },
            Source = document.Source with
            {
                SourcePath = document.Source.SourcePath is { Length: > 0 } path
                    ? MapPathIdentity(path, sourceRoot, identityRoot)
                    : document.Source.SourcePath
            },
            CustomFields = fields
        };
    }

    private static string MapPathIdentity(string path, string sourceRoot, string identityRoot)
    {
        if (!Path.IsPathRooted(path))
        {
            return path;
        }

        var fullPath = Path.GetFullPath(path);
        return PathUtils.IsSameOrSubPathOf(fullPath, sourceRoot)
            ? Path.GetFullPath(Path.Combine(identityRoot, Path.GetRelativePath(sourceRoot, fullPath)))
            : path;
    }

    internal static async Task<I18nVariantSnapshot> CaptureVariantAsync(
        string head,
        string configHash,
        string contentHash,
        BuildVariantResult result,
        int warningCount,
        int errorCount,
        CancellationToken cancellationToken)
    {
        var bodies = new Dictionary<string, ContentBody>(StringComparer.Ordinal);
        var documents = result.RoutedDocuments.Select(x => x.Document)
            .Concat(result.DerivedDocuments.Select(x => x.Document))
            .Concat(result.ContentGraph?.Documents ?? [])
            .DistinctBy(x => (x.Id, x.Body.BodyKey));
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (document.Body.Html is null && document.Body.BodyKey is { Length: > 0 } key)
            {
                bodies[key] = await result.BodyStore.GetAsync(document, cancellationToken).ConfigureAwait(false);
            }
        }

        var pending = result.PendingManifest
            ?? throw new InvalidOperationException("Worker variant did not produce an incremental manifest.");
        return new I18nVariantSnapshot(
            ResultSchema,
            head,
            configHash,
            contentHash,
            result.Language,
            Completed: true,
            result.OutputDir,
            result.BaseUrl,
            result.SearchSnippetsEnabled,
            bodies,
            result.DerivedRoutes.Select(x => new I18nDerivedRouteSnapshot(x.Route, x.LastModified)).ToArray(),
            result.SeoIndex,
            result.SeoModels,
            result.PluginExecutions,
            result.RenderedCount,
            result.SkippedCount,
            result.RenderReasons,
            result.StageMetrics,
            result.RoutedDocuments,
            result.ListRouteGraph?.Routes ?? [],
            result.DerivedDocuments,
            result.ProjectionResults,
            result.StaticRoutes,
            result.PluginOutputs,
            FromManifest(pending.Manifest),
            pending.TemplateHash,
            pending.IncrementalEnabled,
            result.PlannedOutputs.Select(x => new I18nAssetOutputSnapshot(x.Source, x.Destination, x.Category, x.Operation)).ToArray(),
            warningCount,
            errorCount);
    }

    internal static BuildVariantResult RestoreVariant(
        I18nVariantSnapshot snapshot,
        CanonicalContentGraph contentGraph,
        string expectedHead,
        string expectedConfigHash,
        string expectedContentHash,
        string expectedLanguage,
        string expectedOutputDir,
        string manifestPath)
    {
        if (snapshot.Schema != ResultSchema || !snapshot.Completed ||
            !string.Equals(snapshot.Head, expectedHead, StringComparison.Ordinal) ||
            !string.Equals(snapshot.ConfigHash, expectedConfigHash, StringComparison.Ordinal) ||
            !string.Equals(snapshot.ContentHash, expectedContentHash, StringComparison.Ordinal) ||
            !string.Equals(snapshot.Language, expectedLanguage, StringComparison.Ordinal) ||
            !PathEquals(snapshot.OutputDir, expectedOutputDir))
        {
            throw new InvalidDataException("Worker result identity or protocol version is invalid.");
        }

        ValidateOutputPaths(expectedOutputDir, snapshot);
        var bodyStore = new DictionaryContentBodyStore(snapshot.Bodies);
        return new BuildVariantResult(
            snapshot.Language,
            Path.GetFullPath(expectedOutputDir),
            snapshot.BaseUrl,
            snapshot.SearchSnippetsEnabled,
            bodyStore,
            snapshot.DerivedRoutes.Select(x => (x.Route, x.LastModified)).ToArray(),
            snapshot.SeoIndex,
            snapshot.SeoModels,
            snapshot.PluginExecutions,
            snapshot.RenderedCount,
            snapshot.SkippedCount,
            snapshot.RenderReasons,
            snapshot.StageMetrics,
            snapshot.RoutedDocuments,
            contentGraph,
            ListRouteGraph.Create(snapshot.ListRoutes),
            snapshot.DerivedDocuments,
            snapshot.ProjectionResults,
            snapshot.StaticRoutes,
            snapshot.PluginOutputs)
        {
            PendingManifest = new ManifestSetupResult(
                ToManifest(snapshot.Manifest),
                snapshot.TemplateHash,
                manifestPath,
                ManifestEntries: null,
                snapshot.IncrementalEnabled),
            PlannedOutputs = snapshot.PlannedOutputs.Select(x => new AssetOutputItem(
                x.Source,
                x.Destination,
                x.Category,
                Operation: x.Operation)).ToArray()
        };
    }

    internal static async Task WriteContentAsync(string path, I18nContentSnapshot snapshot, CancellationToken cancellationToken)
    {
        var manifestDirectory = Path.GetDirectoryName(Path.GetFullPath(path))
            ?? throw new InvalidOperationException("Content snapshot path has no parent directory.");
        var bodyDirectory = Path.Combine(manifestDirectory, "bodies");
        Directory.CreateDirectory(bodyDirectory);
        var files = new Dictionary<string, I18nBodyFileSnapshot>(StringComparer.Ordinal);
        var index = 0;
        foreach (var (key, body) in snapshot.Bodies.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.Combine("bodies", $"{index++:D8}.html");
            var bodyPath = Path.Combine(manifestDirectory, relativePath);
            await File.WriteAllTextAsync(bodyPath, body.Html, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            RestrictPermissions(bodyPath);
            files[key] = new I18nBodyFileSnapshot(
                relativePath.Replace(Path.DirectorySeparatorChar, '/'),
                HashUtil.Sha256Hex(Encoding.UTF8.GetBytes(body.Html)));
        }

        await WriteAsync(
            path,
            snapshot with
            {
                Bodies = new Dictionary<string, ContentBody>(StringComparer.Ordinal),
                BodyFiles = files
            },
            I18nWorktreeJsonContext.Default.I18nContentSnapshot,
            cancellationToken).ConfigureAwait(false);
    }

    internal static Task WriteRequestAsync(string path, I18nWorkerRequest request, CancellationToken cancellationToken)
        => WriteAsync(path, request, I18nWorktreeJsonContext.Default.I18nWorkerRequest, cancellationToken);

    internal static Task WriteResultAsync(string path, I18nVariantSnapshot result, CancellationToken cancellationToken)
        => WriteAsync(path, result, I18nWorktreeJsonContext.Default.I18nVariantSnapshot, cancellationToken);

    internal static async Task<I18nContentSnapshot> ReadContentAsync(string path, CancellationToken cancellationToken)
    {
        var snapshot = await ReadAsync(path, I18nWorktreeJsonContext.Default.I18nContentSnapshot, cancellationToken).ConfigureAwait(false);
        var manifestDirectory = Path.GetDirectoryName(Path.GetFullPath(path))
            ?? throw new InvalidDataException("Content snapshot path has no parent directory.");
        var bodies = new Dictionary<string, ContentBody>(StringComparer.Ordinal);
        foreach (var (key, file) in snapshot.BodyFiles)
        {
            var bodyPath = FileWriter.GetSafeFullPath(manifestDirectory, file.Path);
            var html = await File.ReadAllTextAsync(bodyPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            var actualHash = HashUtil.Sha256Hex(Encoding.UTF8.GetBytes(html));
            if (!string.Equals(actualHash, file.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Content snapshot body hash is invalid: {file.Path}");
            }
            bodies[key] = new ContentBody(html);
        }
        return snapshot with { Bodies = bodies };
    }

    internal static Task<I18nWorkerRequest> ReadRequestAsync(string path, CancellationToken cancellationToken)
        => ReadAsync(path, I18nWorktreeJsonContext.Default.I18nWorkerRequest, cancellationToken);

    internal static Task<I18nVariantSnapshot> ReadResultAsync(string path, CancellationToken cancellationToken)
        => ReadAsync(path, I18nWorktreeJsonContext.Default.I18nVariantSnapshot, cancellationToken);

    internal static async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    private static async Task WriteAsync<T>(string path, T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Protocol path has no parent directory.");
        Directory.CreateDirectory(directory);
        var tempPath = fullPath + ".tmp";
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, value, typeInfo, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            RestrictPermissions(tempPath);
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private static async Task<T> ReadAsync<T>(string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Protocol payload is empty.");
    }

    private static I18nBuildManifestSnapshot FromManifest(BuildManifest manifest)
        => new(
            manifest.Version,
            manifest.OutputRoot,
            manifest.OwnedOutputs.Order(StringComparer.Ordinal).ToArray(),
            manifest.TemplateHash,
            manifest.Entries,
            manifest.Media,
            manifest.Assets,
            manifest.Static,
            manifest.PluginOutputs);

    private static BuildManifest ToManifest(I18nBuildManifestSnapshot snapshot)
        => new()
        {
            Version = snapshot.Version,
            OutputRoot = snapshot.OutputRoot,
            OwnedOutputs = snapshot.OwnedOutputs.ToHashSet(StringComparer.Ordinal),
            TemplateHash = snapshot.TemplateHash,
            Entries = new Dictionary<string, BuildManifestEntry>(snapshot.Entries, StringComparer.Ordinal),
            Media = new Dictionary<string, string>(snapshot.Media, StringComparer.Ordinal),
            Assets = new Dictionary<string, string>(snapshot.Assets, StringComparer.Ordinal),
            Static = new Dictionary<string, string>(snapshot.Static, StringComparer.Ordinal),
            PluginOutputs = new Dictionary<string, PluginOutputManifestEntry>(snapshot.PluginOutputs, StringComparer.Ordinal)
        };

    private static void ValidateOutputPaths(string outputDir, I18nVariantSnapshot snapshot)
    {
        foreach (var path in snapshot.PlannedOutputs.Select(x => x.Destination)
                     .Concat(snapshot.PluginOutputs.Select(x => x.Path))
                     .Concat(snapshot.RoutedDocuments.Select(x => x.Route.OutputPath))
                     .Concat(snapshot.ListRoutes.Select(x => x.OutputPath))
                     .Concat(snapshot.DerivedDocuments.Select(x => x.Route.OutputPath))
                     .Concat(snapshot.ProjectionResults.SelectMany(x => x.Outputs).Select(x => x.Path))
                     .Concat(snapshot.StaticRoutes.Select(x => x.OutputPath))
                     .Concat(snapshot.Manifest.OwnedOutputs)
                     .Concat(snapshot.Manifest.Entries.Values.Select(x => x.OutputPath))
                     .Concat(snapshot.Manifest.Media.Keys)
                     .Concat(snapshot.Manifest.Assets.Keys)
                     .Concat(snapshot.Manifest.Static.Keys)
                     .Concat(snapshot.Manifest.PluginOutputs.Values.Select(x => x.Path)))
        {
            _ = FileWriter.GetSafeFullPath(outputDir, path);
        }
    }

    private static bool PathEquals(string left, string right)
        => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), Shared.PlatformPathHelper.PathComparison);

    private static void RestrictPermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}

internal sealed record I18nConfigIdentity(
    AppConfig Config,
    BuildExecutionMode ExecutionMode,
    bool IsCI,
    bool? Incremental,
    int? Jobs);

internal sealed class I18nObjectJsonConverter : JsonConverter<object>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return ReadElement(document.RootElement);
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case JsonElement element:
                element.WriteTo(writer);
                return;
            case string text:
                writer.WriteStringValue(text);
                return;
            case bool boolean:
                writer.WriteBooleanValue(boolean);
                return;
            case byte number:
                writer.WriteNumberValue(number);
                return;
            case short number:
                writer.WriteNumberValue(number);
                return;
            case int number:
                writer.WriteNumberValue(number);
                return;
            case long number:
                writer.WriteNumberValue(number);
                return;
            case float number:
                writer.WriteNumberValue(number);
                return;
            case double number:
                writer.WriteNumberValue(number);
                return;
            case decimal number:
                writer.WriteNumberValue(number);
                return;
            case DateTime timestamp:
                writer.WriteStringValue(timestamp);
                return;
            case DateTimeOffset timestamp:
                writer.WriteStringValue(timestamp);
                return;
            case IReadOnlyDictionary<string, object?> map:
                writer.WriteStartObject();
                foreach (var (key, item) in map)
                {
                    writer.WritePropertyName(key);
                    WriteValue(writer, item, options);
                }
                writer.WriteEndObject();
                return;
            case IDictionary dictionary:
                writer.WriteStartObject();
                foreach (DictionaryEntry item in dictionary)
                {
                    writer.WritePropertyName(item.Key?.ToString() ?? string.Empty);
                    WriteValue(writer, item.Value, options);
                }
                writer.WriteEndObject();
                return;
            case IEnumerable sequence:
                writer.WriteStartArray();
                foreach (var item in sequence)
                {
                    WriteValue(writer, item, options);
                }
                writer.WriteEndArray();
                return;
            default:
                writer.WriteStringValue(value.ToString());
                return;
        }
    }

    private static object? ReadElement(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.Array => element.EnumerateArray().Select(ReadElement).ToArray(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(x => x.Name, x => ReadElement(x.Value), StringComparer.OrdinalIgnoreCase),
            _ => element.GetRawText()
        };

    private static void WriteValue(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            new I18nObjectJsonConverter().Write(writer, value, options);
        }
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    Converters = [typeof(I18nObjectJsonConverter)])]
[JsonSerializable(typeof(I18nContentSnapshot))]
[JsonSerializable(typeof(I18nConfigIdentity))]
[JsonSerializable(typeof(I18nWorkerRequest))]
[JsonSerializable(typeof(I18nVariantSnapshot))]
internal sealed partial class I18nWorktreeJsonContext : JsonSerializerContext;
