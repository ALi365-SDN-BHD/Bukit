using System.Text.Json.Serialization;

namespace Bukit.Plugins.VisualFeedbackPlugin;

public sealed record VisualFeedbackConfig
{
    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; init; } = "http://localhost:4173";

    [JsonPropertyName("aiProvider")]
    public string AiProvider { get; init; } = "openai";

    [JsonPropertyName("aiModel")]
    public string AiModel { get; init; } = "gpt-4o";

    [JsonPropertyName("aiApiKey")]
    public string? AiApiKey { get; init; }

    [JsonPropertyName("aiEndpoint")]
    public string? AiEndpoint { get; init; }

    [JsonPropertyName("captureWidths")]
    public IReadOnlyList<int> CaptureWidths { get; init; } = new[] { 375, 768, 1440 };

    [JsonPropertyName("outputReport")]
    public string OutputReport { get; init; } = ".bukit/visual-report.json";

    [JsonPropertyName("screenshotDir")]
    public string ScreenshotDir { get; init; } = ".bukit/screenshots";
}

public sealed record VisualReport
{
    [JsonPropertyName("schema")]
    public string Schema { get; init; } = "https://bukit.dev/schemas/visual-report.v1.json";

    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = "1.0";

    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; init; }

    [JsonPropertyName("siteUrl")]
    public string SiteUrl { get; init; } = "";

    [JsonPropertyName("summary")]
    public VisualSummary Summary { get; init; } = new();

    [JsonPropertyName("pages")]
    public IReadOnlyList<VisualPageResult> Pages { get; init; } = Array.Empty<VisualPageResult>();
}

public sealed record VisualSummary
{
    [JsonPropertyName("totalPages")]
    public int TotalPages { get; init; }

    [JsonPropertyName("totalScreenshots")]
    public int TotalScreenshots { get; init; }

    [JsonPropertyName("averageLayoutScore")]
    public double AverageLayoutScore { get; init; }

    [JsonPropertyName("averageReadabilityScore")]
    public double AverageReadabilityScore { get; init; }

    [JsonPropertyName("averageColorScore")]
    public double AverageColorScore { get; init; }

    [JsonPropertyName("averageA11yScore")]
    public double AverageA11yScore { get; init; }

    [JsonPropertyName("averageResponsiveScore")]
    public double AverageResponsiveScore { get; init; }

    [JsonPropertyName("overallScore")]
    public double OverallScore { get; init; }

    [JsonPropertyName("issues")]
    public IReadOnlyList<VisualIssue> Issues { get; init; } = Array.Empty<VisualIssue>();
}

public sealed record VisualPageResult
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = "";

    [JsonPropertyName("outputPath")]
    public string OutputPath { get; init; } = "";

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("widths")]
    public IReadOnlyList<VisualWidthResult> Widths { get; init; } = Array.Empty<VisualWidthResult>();
}

public sealed record VisualWidthResult
{
    [JsonPropertyName("width")]
    public int Width { get; init; }

    [JsonPropertyName("screenshotPath")]
    public string ScreenshotPath { get; init; } = "";

    [JsonPropertyName("layoutScore")]
    public double LayoutScore { get; init; }

    [JsonPropertyName("readabilityScore")]
    public double ReadabilityScore { get; init; }

    [JsonPropertyName("colorScore")]
    public double ColorScore { get; init; }

    [JsonPropertyName("a11yScore")]
    public double A11yScore { get; init; }

    [JsonPropertyName("responsiveScore")]
    public double ResponsiveScore { get; init; }

    [JsonPropertyName("feedback")]
    public string? Feedback { get; init; }

    [JsonPropertyName("issues")]
    public IReadOnlyList<VisualIssue> Issues { get; init; } = Array.Empty<VisualIssue>();
}

public sealed record VisualIssue
{
    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "warning";

    [JsonPropertyName("category")]
    public string Category { get; init; } = "";

    [JsonPropertyName("message")]
    public string Message { get; init; } = "";

    [JsonPropertyName("suggestion")]
    public string? Suggestion { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(VisualReport))]
[JsonSerializable(typeof(VisualSummary))]
[JsonSerializable(typeof(VisualPageResult))]
[JsonSerializable(typeof(VisualWidthResult))]
[JsonSerializable(typeof(VisualIssue))]
[JsonSerializable(typeof(VisualFeedbackConfig))]
internal sealed partial class VisualReportJsonContext : JsonSerializerContext;
