using System.Text.Json;
using Bukit.Engine.Plugins.Protocol;

namespace Bukit.Plugins.VisualFeedbackPlugin;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var plugin = new VisualFeedbackPlugin();
        await plugin.RunAsync();
        return 0;
    }
}

internal sealed class VisualFeedbackPlugin : ProcessPluginHost
{
    protected override string PluginName => "visual-feedback";
    protected override string PluginVersion => "1.0.0";
    protected override IReadOnlyList<string> SupportedHooks => new[] { "after-build" };

    protected override async Task AfterBuildAsync(
        AfterBuildRequestPayload payload,
        IReadOnlyDictionary<string, object>? pluginOptions,
        CancellationToken ct)
    {
        var config = ParseConfig(pluginOptions);
        var outputDir = Path.GetFullPath(payload.OutputDir);
        var reportDir = Path.Combine(outputDir, ".bukit");
        var screenshotDir = Path.Combine(reportDir, config.ScreenshotDir.TrimStart('.').TrimStart('/'));
        Directory.CreateDirectory(reportDir);
        Directory.CreateDirectory(screenshotDir);

        var urls = payload.RoutedPages
            .Select(p => p.Url)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(u => u, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (urls.Count == 0)
        {
            urls.Add("/");
        }

        var capturer = new ScreenshotCapturer(outputDir);
        var analyzer = new AiAnalyzer(config);
        var pageResults = new List<VisualPageResult>();

        foreach (var url in urls)
        {
            var pageResult = new VisualPageResult
            {
                Url = url,
                OutputPath = payload.RoutedPages
                    .FirstOrDefault(p => string.Equals(p.Url, url, StringComparison.OrdinalIgnoreCase))
                    ?.OutputPath ?? "",
                Title = payload.RoutedPages
                    .FirstOrDefault(p => string.Equals(p.Url, url, StringComparison.OrdinalIgnoreCase))
                    ?.Meta?.TryGetValue("title", out var titleObj) == true
                    ? titleObj?.ToString()
                    : null
            };

            var widthResults = new List<VisualWidthResult>();
            foreach (var width in config.CaptureWidths)
            {
                var screenshotPath = Path.Combine(screenshotDir, UrlToFileName(url, width));
                var widthResult = new VisualWidthResult
                {
                    Width = width,
                    ScreenshotPath = screenshotPath
                };

                if (File.Exists(screenshotPath))
                {
                    var analysis = await analyzer.AnalyzeAsync(screenshotPath, width, url, ct);
                    widthResults.Add(analysis);
                }
                else
                {
                    widthResults.Add(new VisualWidthResult
                    {
                        Width = width,
                        ScreenshotPath = screenshotPath,
                        Feedback = "Screenshot not captured. Ensure Playwright is installed: npx playwright install --with-deps chromium"
                    });
                }
            }

            pageResult = pageResult with { Widths = widthResults };
            pageResults.Add(pageResult);
        }

        var allWidths = pageResults.SelectMany(p => p.Widths).Where(w => w.LayoutScore > 0).ToList();
        var allIssues = pageResults.SelectMany(p => p.Widths).SelectMany(w => w.Issues).ToList();

        var report = new VisualReport
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            SiteUrl = config.BaseUrl,
            Summary = new VisualSummary
            {
                TotalPages = pageResults.Count,
                TotalScreenshots = allWidths.Count,
                AverageLayoutScore = allWidths.Count > 0 ? allWidths.Average(w => w.LayoutScore) : 0,
                AverageReadabilityScore = allWidths.Count > 0 ? allWidths.Average(w => w.ReadabilityScore) : 0,
                AverageColorScore = allWidths.Count > 0 ? allWidths.Average(w => w.ColorScore) : 0,
                AverageA11yScore = allWidths.Count > 0 ? allWidths.Average(w => w.A11yScore) : 0,
                AverageResponsiveScore = allWidths.Count > 0 ? allWidths.Average(w => w.ResponsiveScore) : 0,
                OverallScore = allWidths.Count > 0
                    ? allWidths.Average(w =>
                        (w.LayoutScore + w.ReadabilityScore + w.ColorScore + w.A11yScore + w.ResponsiveScore) / 5.0)
                    : 0,
                Issues = allIssues
            },
            Pages = pageResults
        };

        var reportPath = Path.Combine(reportDir, config.OutputReport.TrimStart('.').TrimStart('/'));
        var json = JsonSerializer.Serialize(report, VisualReportJsonContext.Default.VisualReport);
        await File.WriteAllTextAsync(reportPath, json, ct);

        Console.WriteLine($"[visual-feedback] Report written: {reportPath}");
        Console.WriteLine($"[visual-feedback] Pages: {report.Summary.TotalPages}, Screenshots: {report.Summary.TotalScreenshots}, Overall Score: {report.Summary.OverallScore:F1}/100");
    }

    private static VisualFeedbackConfig ParseConfig(IReadOnlyDictionary<string, object>? options)
    {
        if (options is null || options.Count == 0)
        {
            return new VisualFeedbackConfig();
        }

        try
        {
            var json = JsonSerializer.Serialize(options);
            return JsonSerializer.Deserialize(json, VisualReportJsonContext.Default.VisualFeedbackConfig)
                   ?? new VisualFeedbackConfig();
        }
        catch
        {
            return new VisualFeedbackConfig();
        }
    }

    private static string UrlToFileName(string url, int width)
    {
        var safe = url
            .Replace("://", "-")
            .Replace("/", "-")
            .Replace("?", "-")
            .Replace("&", "-")
            .Replace("=", "-")
            .Trim('-');
        if (string.IsNullOrEmpty(safe))
        {
            safe = "home";
        }

        return $"{safe}-w{width}.png";
    }
}
