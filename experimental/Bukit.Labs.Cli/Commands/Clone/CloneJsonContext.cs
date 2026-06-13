using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bukit.Cli.Commands;

internal static class CloneJson
{
    public static string Serialize(object value)
        => JsonSerializer.Serialize(value, value.GetType(), CloneJsonContext.Default);

    public static string SerializeIndented(object value)
        => JsonSerializer.Serialize(value, value.GetType(), CloneIndentedJsonContext.Default);
}

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CloneTokens))]
[JsonSerializable(typeof(CloneTokens.CloneTokensWrapper))]
[JsonSerializable(typeof(CloneLayoutInfo))]
[JsonSerializable(typeof(ClonePageInfo))]
[JsonSerializable(typeof(List<CloneSectionInfo>))]
[JsonSerializable(typeof(CloneSectionsDocument))]
[JsonSerializable(typeof(CloneBehaviors))]
[JsonSerializable(typeof(List<CloneIcon>))]
[JsonSerializable(typeof(List<CloneAsset>))]
internal sealed partial class CloneInputJsonContext : JsonSerializerContext;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CloneBehaviors))]
[JsonSerializable(typeof(List<CloneSectionButton>))]
[JsonSerializable(typeof(List<CloneSectionItem>))]
[JsonSerializable(typeof(List<CloneComponentInfo>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(CloneBox))]
[JsonSerializable(typeof(List<CloneInteractionInfo>))]
[JsonSerializable(typeof(List<SectionState>))]
[JsonSerializable(typeof(SectionResponsiveInfo))]
[JsonSerializable(typeof(List<CloneAssetManifestEntry>))]
[JsonSerializable(typeof(CloneVerifyReportJson))]
internal sealed partial class CloneJsonContext : JsonSerializerContext;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CloneBehaviors))]
[JsonSerializable(typeof(CloneBox))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(SectionResponsiveInfo))]
[JsonSerializable(typeof(List<SectionState>))]
[JsonSerializable(typeof(List<CloneInteractionInfo>))]
[JsonSerializable(typeof(List<CloneComponentInfo>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(CloneVerifyReportJson))]
internal sealed partial class CloneIndentedJsonContext : JsonSerializerContext;

internal sealed record CloneAssetManifestEntry(
    string Type,
    string Src,
    string? Alt,
    string? Media,
    string? Width,
    string? Height,
    string? LocalPath,
    string? Integrity,
    string? Failure);

internal sealed record CloneVerifyReportJson(
    bool BuildPassed,
    string ConfigPath,
    double VisualThreshold,
    bool Passed,
    CloneVerifyReportSummary Summary,
    IReadOnlyList<CloneVerifyScreenshotComparison> Comparisons,
    IReadOnlyList<CloneVerifyMissingScreenshot> MissingScreenshots,
    IReadOnlyList<CloneVerifyAffectedSection> AffectedSections);

internal sealed record CloneVerifyReportSummary(
    int Comparisons,
    int FailedComparisons,
    int MissingScreenshots,
    int AffectedSections);

internal sealed record CloneVerifyScreenshotComparison(
    string Name,
    bool Passed,
    string Status,
    int ComparedPixels,
    int MismatchedPixels,
    double DiffRatio,
    int TargetWidth,
    int TargetHeight,
    int LocalWidth,
    int LocalHeight,
    CloneVerifyMismatchBounds? MismatchBounds);

internal sealed record CloneVerifyMismatchBounds(
    int? MinX,
    int? MinY,
    int? MaxX,
    int? MaxY);

internal sealed record CloneVerifyMissingScreenshot(
    string Viewport,
    string TargetPath,
    string LocalPath,
    bool TargetExists,
    bool LocalExists);

internal sealed record CloneVerifyAffectedSection(
    string Screenshot,
    string Viewport,
    int SectionIndex,
    string SectionKey,
    string? SectionId,
    string? SectionType,
    int? SectionOrder,
    string SectionLabel,
    string DataPath,
    string SpecPath,
    double SectionY,
    double SectionHeight,
    int MismatchMinY,
    int MismatchMaxY);
