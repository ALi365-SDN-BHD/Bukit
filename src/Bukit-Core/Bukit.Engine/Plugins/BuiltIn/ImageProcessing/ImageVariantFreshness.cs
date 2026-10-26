using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using static Bukit.Engine.Plugins.BuiltIn.ImageProcessing.ImageVariantOwnership;

namespace Bukit.Engine.Plugins.BuiltIn.ImageProcessing;

/// <summary>
/// Freshness sidecar records that prove a variant was produced by Bukit from
/// the current source bytes and settings.
/// </summary>
internal static class ImageVariantFreshness
{
    internal const string FreshnessSuffix = ".bukit-freshness.json";
    internal const int FreshnessSchemaVersion = 1;
    internal const string FreshnessOwner = "bukit:image-processing";

    internal static bool TryReadFreshness(
        string path,
        string outputDir,
        string sourceFile,
        string variantFile,
        int expectedSize,
        out VariantFreshness freshness)
    {
        freshness = null!;
        try
        {
            if (!TryReadFreshnessRecord(path, out var candidate) ||
                !string.Equals(candidate.SourcePath, GetRelativeIdentity(outputDir, sourceFile), StringComparison.Ordinal) ||
                !string.Equals(candidate.VariantPath, GetRelativeIdentity(outputDir, variantFile), StringComparison.Ordinal) ||
                candidate.Size != expectedSize ||
                !string.Equals(candidate.Format, Path.GetExtension(variantFile).ToLowerInvariant(), StringComparison.Ordinal) ||
                !MatchesVariantIdentity(candidate, variantFile))
            {
                return false;
            }

            freshness = candidate;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    internal static bool TryReadFreshnessRecord(string path, out VariantFreshness freshness)
    {
        freshness = null!;
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("schemaVersion", out var schemaVersionEl) ||
                !schemaVersionEl.TryGetInt32(out var schemaVersion) ||
                schemaVersion != FreshnessSchemaVersion ||
                !root.TryGetProperty("owner", out var ownerEl) ||
                ownerEl.ValueKind != JsonValueKind.String ||
                !string.Equals(ownerEl.GetString(), FreshnessOwner, StringComparison.Ordinal) ||
                !root.TryGetProperty("sourcePath", out var sourcePathEl) ||
                sourcePathEl.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("variantPath", out var variantPathEl) ||
                variantPathEl.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("sourceSize", out var sourceSizeEl) ||
                !sourceSizeEl.TryGetInt64(out var sourceSize) ||
                sourceSize < 0 ||
                !root.TryGetProperty("sourceMtime", out var sourceMtimeEl) ||
                !sourceMtimeEl.TryGetInt64(out var sourceMtime) ||
                !root.TryGetProperty("sourceSha256", out var sourceSha256El) ||
                sourceSha256El.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("variantLength", out var variantLengthEl) ||
                !variantLengthEl.TryGetInt64(out var variantLength) ||
                variantLength < 0 ||
                !root.TryGetProperty("variantSha256", out var variantSha256El) ||
                variantSha256El.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("quality", out var qualityEl) ||
                !qualityEl.TryGetInt32(out var quality) ||
                quality <= 0 ||
                !root.TryGetProperty("size", out var sizeEl) ||
                !sizeEl.TryGetInt32(out var size) ||
                size <= 0 ||
                !root.TryGetProperty("format", out var formatEl) ||
                formatEl.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("tool", out var toolEl) ||
                toolEl.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var sourcePath = sourcePathEl.GetString() ?? string.Empty;
            var variantPath = variantPathEl.GetString() ?? string.Empty;
            var sourceSha256 = sourceSha256El.GetString() ?? string.Empty;
            var variantSha256 = variantSha256El.GetString() ?? string.Empty;
            var format = formatEl.GetString() ?? string.Empty;
            var tool = toolEl.GetString() ?? string.Empty;
            if (!IsNormalizedRelativeIdentity(sourcePath) ||
                !IsNormalizedRelativeIdentity(variantPath) ||
                !IsSha256(sourceSha256) ||
                !IsSha256(variantSha256) ||
                string.IsNullOrWhiteSpace(format) ||
                string.IsNullOrWhiteSpace(tool))
            {
                return false;
            }

            freshness = new VariantFreshness(
                schemaVersion,
                ownerEl.GetString()!,
                sourcePath,
                variantPath,
                sourceSize,
                sourceMtime,
                sourceSha256,
                variantLength,
                variantSha256,
                quality,
                size,
                format,
                tool);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);

    internal static bool MatchesVariantIdentity(VariantFreshness freshness, string variantFile)
    {
        try
        {
            var info = new FileInfo(variantFile);
            return info.Exists &&
                   info.Length == freshness.VariantLength &&
                   string.Equals(ComputeSha256(variantFile), freshness.VariantSha256, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static void WriteFreshness(string path, VariantFreshness freshness)
    {
        var json = JsonSerializer.Serialize(freshness, VariantFreshnessJsonContext.Default.VariantFreshness);
        File.WriteAllText(path, json);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.SequentialScan);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    internal static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }
}

internal sealed record VariantFreshness(
    int SchemaVersion,
    string Owner,
    string SourcePath,
    string VariantPath,
    long SourceSize,
    long SourceMtime,
    string SourceSha256,
    long VariantLength,
    string VariantSha256,
    int Quality,
    int Size,
    string Format,
    string Tool)
{
    public bool Matches(
        FileInfo source,
        string sourceSha256,
        int quality,
        int size,
        string format,
        string tool)
        => SourceSize == source.Length
           && SourceMtime == source.LastWriteTimeUtc.Ticks
           && string.Equals(SourceSha256, sourceSha256, StringComparison.Ordinal)
           && Quality == quality
           && Size == size
           && string.Equals(Format, format.ToLowerInvariant(), StringComparison.Ordinal)
           && string.Equals(Tool, tool, StringComparison.Ordinal);
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(VariantFreshness))]
internal sealed partial class VariantFreshnessJsonContext : JsonSerializerContext;
