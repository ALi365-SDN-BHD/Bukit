namespace Bukit.Importing;

internal static class ImportSafetyScanner
{
    private static readonly string[] SensitiveFileNames =
    [
        ".env", ".npmrc", ".git", "node_modules", ".vscode", "dist", "build"
    ];

    private static readonly string[] SensitiveExtensions =
    [
        ".key", ".pfx", ".p12", ".pem", ".crt", ".cert"
    ];

    private static readonly string[] DangerousProtocols =
    [
        "javascript:", "vbscript:", "file:"
    ];

    internal static List<ImportDiagnostic> Scan(
        HtmlDemoImportOptions options, List<DiscoveredPage> pages)
    {
        var diagnostics = new List<ImportDiagnostic>();

        ScanSensitiveFiles(options.InputPath, diagnostics);

        foreach (var page in pages)
            ScanHtmlContent(page, diagnostics);

        return diagnostics;
    }

    private static void ScanSensitiveFiles(string inputPath, List<ImportDiagnostic> diagnostics)
    {
        foreach (var pattern in SensitiveFileNames)
        {
            var matches = Directory.GetFiles(inputPath, pattern, SearchOption.AllDirectories);
            foreach (var match in matches)
            {
                diagnostics.Add(new ImportDiagnostic(
                    ImportDiagnosticSeverity.Error,
                    "SENSITIVE_FILE",
                    $"发现敏感文件: {Path.GetRelativePath(inputPath, match)}",
                    match));
            }
        }

        var allFiles = Directory.GetFiles(inputPath, "*.*", SearchOption.AllDirectories);
        foreach (var file in allFiles)
        {
            var ext = Path.GetExtension(file);
            if (SensitiveExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                diagnostics.Add(new ImportDiagnostic(
                    ImportDiagnosticSeverity.Error,
                    "SENSITIVE_FILE",
                    $"发现敏感文件: {Path.GetRelativePath(inputPath, file)}",
                    file));
            }
        }
    }

    private static void ScanHtmlContent(DiscoveredPage page, List<ImportDiagnostic> diagnostics)
    {
        var html = page.FullHtml;
        if (string.IsNullOrEmpty(html)) return;

        if (html.Contains("<script", StringComparison.OrdinalIgnoreCase) &&
            !html.Contains("<script src=", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new ImportDiagnostic(
                ImportDiagnosticSeverity.Warning,
                "INLINE_SCRIPT",
                "页面包含内联 script 标签",
                page.FilePath));
        }

        if (html.Contains("<iframe", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new ImportDiagnostic(
                ImportDiagnosticSeverity.Warning,
                "IFRAME_DETECTED",
                "页面包含 iframe 标签",
                page.FilePath));
        }

        foreach (var protocol in DangerousProtocols)
        {
            if (html.Contains(protocol, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new ImportDiagnostic(
                    ImportDiagnosticSeverity.Warning,
                    "DANGEROUS_PROTOCOL",
                    $"页面包含危险协议: {protocol}",
                    page.FilePath));
                break;
            }
        }

        if (html.Contains("onclick=", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("onload=", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("onerror=", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new ImportDiagnostic(
                ImportDiagnosticSeverity.Warning,
                "INLINE_EVENT_HANDLER",
                "页面包含内联事件处理器 (onclick/onload/onerror)",
                page.FilePath));
        }

        if (string.IsNullOrWhiteSpace(page.Title))
        {
            diagnostics.Add(new ImportDiagnostic(
                ImportDiagnosticSeverity.Warning,
                "MISSING_TITLE",
                "页面缺少 <title> 标签",
                page.FilePath));
        }
    }
}
