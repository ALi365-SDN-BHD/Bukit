using System.Collections;
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
using Bukit.Theme;

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
