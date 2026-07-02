using System.Diagnostics;
using System.Text;

namespace Bukit.Plugins.VisualFeedbackPlugin;

internal sealed class ScreenshotCapturer
{
    private readonly string _playwrightScriptDir;

    internal ScreenshotCapturer(string outputDir)
    {
        _playwrightScriptDir = Path.Combine(outputDir, ".bukit", "visual-capture");
        Directory.CreateDirectory(_playwrightScriptDir);
    }

    internal async Task<IReadOnlyList<string>> CaptureAsync(
        string baseUrl,
        IReadOnlyList<string> urls,
        IReadOnlyList<int> widths,
        string screenshotDir,
        CancellationToken ct = default)
    {
        var captures = new List<string>();
        Directory.CreateDirectory(screenshotDir);

        var scriptPath = WriteCaptureScript(baseUrl, urls, widths, screenshotDir);

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "npx",
                    Arguments = $"playwright test \"{scriptPath}\" --reporter=line",
                    WorkingDirectory = _playwrightScriptDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    Console.WriteLine($"[playwright] {e.Data}");
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    Console.Error.WriteLine($"[playwright:err] {e.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                Console.Error.WriteLine($"[visual-feedback] Playwright exited with code {process.ExitCode}. Ensure @playwright/test is installed: npx playwright install --with-deps chromium");
            }

            foreach (var url in urls)
            {
                foreach (var width in widths)
                {
                    var safeFile = UrlToFileName(url, width);
                    var path = Path.Combine(screenshotDir, safeFile);
                    if (File.Exists(path))
                    {
                        captures.Add(path);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[visual-feedback] Screenshot capture failed: {ex.Message}");
        }

        return captures;
    }

    private string WriteCaptureScript(string baseUrl, IReadOnlyList<string> urls, IReadOnlyList<int> widths, string screenshotDir)
    {
        var sb = new StringBuilder();
        sb.AppendLine("const { test, expect } = require('@playwright/test');");
        sb.AppendLine();

        foreach (var url in urls)
        {
            foreach (var width in widths)
            {
                var safeFileName = UrlToFileName(url, width);
                var screenshotPath = Path.Combine(screenshotDir, safeFileName).Replace('\\', '/');
                var testName = $"{url} @ {width}px";

                sb.AppendLine($"test('{testName}', async ({{ page }}) => {{");
                sb.AppendLine($"  await page.setViewportSize({{ width: {width}, height: 800 }});");
                sb.AppendLine($"  await page.goto('{baseUrl}{url}', {{ waitUntil: 'networkidle' }});");
                sb.AppendLine($"  await page.screenshot({{ path: '{screenshotPath}', fullPage: true }});");
                sb.AppendLine("});");
                sb.AppendLine();
            }
        }

        var scriptPath = Path.Combine(_playwrightScriptDir, "visual-capture.spec.js");
        File.WriteAllText(scriptPath, sb.ToString());
        return scriptPath;
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
