using System.Text;
using System.Text.Json;

namespace Bukit.Plugins.VisualFeedbackPlugin;

internal sealed class AiAnalyzer
{
    private readonly HttpClient _httpClient;
    private readonly VisualFeedbackConfig _config;

    internal AiAnalyzer(VisualFeedbackConfig config)
    {
        _config = config;
        _httpClient = new HttpClient();
    }

    internal async Task<VisualWidthResult> AnalyzeAsync(string screenshotPath, int width, string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_config.AiApiKey))
        {
            return CreateFallbackResult(screenshotPath, width, "No AI API key configured. Set plugins.visual-feedback.options.aiApiKey in site.yaml.");
        }

        try
        {
            var imageBase64 = await ReadImageAsBase64Async(screenshotPath, ct);
            if (imageBase64 is null)
            {
                return CreateFallbackResult(screenshotPath, width, $"Screenshot not found: {screenshotPath}");
            }

            var requestBody = BuildRequest(imageBase64, width, url);
            var response = await SendRequestAsync(requestBody, ct);
            return ParseResponse(response, screenshotPath, width);
        }
        catch (Exception ex)
        {
            return CreateFallbackResult(screenshotPath, width, $"AI analysis failed: {ex.Message}");
        }
    }

    private static async Task<string?> ReadImageAsBase64Async(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(path, ct);
        return Convert.ToBase64String(bytes);
    }

    private object BuildRequest(string imageBase64, int width, string url)
    {
        var endpoint = _config.AiEndpoint ?? "https://api.openai.com/v1/chat/completions";
        var model = _config.AiModel;
        var apiKey = _config.AiApiKey;

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var systemPrompt = @"You are a web design QA expert. Analyze the screenshot of a web page and return a JSON object with these fields:
- layoutScore: 0-100 rating of visual hierarchy, spacing, and alignment
- readabilityScore: 0-100 rating of text size, line height, and contrast
- colorScore: 0-100 rating of color harmony, palette consistency
- a11yScore: 0-100 rating of accessibility signals (contrast, font size, semantic layout clues)
- responsiveScore: 0-100 rating of how well the layout fits the given viewport width
- feedback: 1-2 sentences of actionable feedback in English
- issues: array of { severity: 'error'|'warning', category: string, message: string, suggestion: string }

Return ONLY the JSON object, no markdown fences, no explanation.";

        var userPrompt = $"Analyze this web page screenshot at {width}px viewport width (URL: {url}).";

        return new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = userPrompt },
                        new { type = "image_url", image_url = new { url = $"data:image/png;base64,{imageBase64}", detail = "high" } }
                    }
                }
            },
            max_tokens = 1024,
            temperature = 0.3
        };
    }

    private async Task<string> SendRequestAsync(object body, CancellationToken ct)
    {
        var endpoint = _config.AiEndpoint ?? "https://api.openai.com/v1/chat/completions";
        var json = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(endpoint, content, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(ct);
    }

    private VisualWidthResult ParseResponse(string responseJson, string screenshotPath, int width)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
            {
                return CreateFallbackResult(screenshotPath, width, "Empty AI response.");
            }

            var trimmed = content.Trim();
            if (trimmed.StartsWith("```"))
            {
                var start = trimmed.IndexOf('\n');
                var end = trimmed.LastIndexOf("```", StringComparison.Ordinal);
                if (start >= 0 && end > start)
                {
                    trimmed = trimmed[(start + 1)..end].Trim();
                }
            }

            using var resultDoc = JsonDocument.Parse(trimmed);
            var root = resultDoc.RootElement;

            var issues = new List<VisualIssue>();
            if (root.TryGetProperty("issues", out var issuesArray))
            {
                foreach (var issue in issuesArray.EnumerateArray())
                {
                    issues.Add(new VisualIssue
                    {
                        Severity = GetString(issue, "severity") ?? "warning",
                        Category = GetString(issue, "category") ?? "general",
                        Message = GetString(issue, "message") ?? "",
                        Suggestion = GetString(issue, "suggestion")
                    });
                }
            }

            return new VisualWidthResult
            {
                Width = width,
                ScreenshotPath = screenshotPath,
                LayoutScore = GetDouble(root, "layoutScore"),
                ReadabilityScore = GetDouble(root, "readabilityScore"),
                ColorScore = GetDouble(root, "colorScore"),
                A11yScore = GetDouble(root, "a11yScore"),
                ResponsiveScore = GetDouble(root, "responsiveScore"),
                Feedback = GetString(root, "feedback"),
                Issues = issues
            };
        }
        catch (JsonException)
        {
            return CreateFallbackResult(screenshotPath, width, "Failed to parse AI response.");
        }
    }

    private static double GetDouble(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var prop) && prop.TryGetDouble(out var value) ? value : 0;
    }

    private static string? GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var prop) ? prop.GetString() : null;
    }

    private static VisualWidthResult CreateFallbackResult(string screenshotPath, int width, string feedback)
    {
        return new VisualWidthResult
        {
            Width = width,
            ScreenshotPath = screenshotPath,
            LayoutScore = 0,
            ReadabilityScore = 0,
            ColorScore = 0,
            A11yScore = 0,
            ResponsiveScore = 0,
            Feedback = feedback,
            Issues = new[]
            {
                new VisualIssue
                {
                    Severity = "warning",
                    Category = "ai-analysis",
                    Message = feedback,
                    Suggestion = "Set plugins.visual-feedback.options.aiApiKey to enable AI visual analysis."
                }
            }
        };
    }
}
