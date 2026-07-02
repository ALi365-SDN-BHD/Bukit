namespace Bukit.Importing;

internal static partial class ImportSafetyScanner
{
    internal static List<ImportDiagnostic> Scan(
        HtmlDemoImportOptions options, List<DiscoveredPage> pages)
    {
        var diagnostics = new List<ImportDiagnostic>();
        var htmlFiles = Directory.GetFiles(options.InputPath, "*.html", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(options.InputPath, path).Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        ScanSensitiveFiles(options.InputPath, diagnostics);

        foreach (var page in pages)
        {
            ScanHtmlContent(page, diagnostics);
            ScanInternalLinks(options.InputPath, page, htmlFiles, diagnostics);
        }

        return diagnostics;
    }

    private static void ScanSensitiveFiles(string inputPath, List<ImportDiagnostic> diagnostics)
    {
        foreach (var pattern in ImportSafetyPatterns.SensitiveFileNames)
        {
            var fileMatches = Directory.GetFiles(inputPath, pattern, SearchOption.AllDirectories);
            foreach (var match in fileMatches)
            {
                diagnostics.Add(new ImportDiagnostic(
                    ImportDiagnosticSeverity.Error,
                    "SENSITIVE_FILE",
                    $"发现敏感文件: {Path.GetRelativePath(inputPath, match)}",
                    match));
            }

            var dirMatches = Directory.GetDirectories(inputPath, pattern, SearchOption.AllDirectories);
            foreach (var match in dirMatches)
            {
                diagnostics.Add(new ImportDiagnostic(
                    ImportDiagnosticSeverity.Error,
                    "SENSITIVE_FILE",
                    $"发现敏感目录: {Path.GetRelativePath(inputPath, match)}",
                    match));
            }
        }

        foreach (var pattern in ImportSafetyPatterns.SensitiveFilePatterns)
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
            if (ImportSafetyPatterns.SensitiveExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
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

        if (InlineScriptPattern().IsMatch(html))
        {
            diagnostics.Add(new ImportDiagnostic(
                ImportDiagnosticSeverity.Warning,
                "INLINE_SCRIPT",
                "页面包含内联 script 标签",
                page.FilePath));
        }

        if (ExternalScriptPattern().IsMatch(html))
        {
            diagnostics.Add(new ImportDiagnostic(
                ImportDiagnosticSeverity.Warning,
                "EXTERNAL_SCRIPT",
                "页面包含外部 script，需要人工审查",
                page.FilePath));
        }

        if (ExternalFormActionPattern().IsMatch(html))
        {
            diagnostics.Add(new ImportDiagnostic(
                ImportDiagnosticSeverity.Warning,
                "EXTERNAL_FORM_ACTION",
                "页面包含外部 form action，需要人工审查",
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

        foreach (var protocol in ImportSafetyPatterns.DangerousProtocols)
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

    private static void ScanInternalLinks(
        string inputPath,
        DiscoveredPage page,
        HashSet<string> htmlFiles,
        List<ImportDiagnostic> diagnostics)
    {
        foreach (System.Text.RegularExpressions.Match match in LinkHrefPattern().Matches(page.FullHtml))
        {
            var href = match.Groups["href"].Value.Trim();
            if (string.IsNullOrWhiteSpace(href) ||
                href.StartsWith('#') ||
                href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                href.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
                href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
                href.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cleanHref = href.Split('#')[0].Split('?')[0];
            if (!cleanHref.EndsWith(".html", StringComparison.OrdinalIgnoreCase) &&
                !cleanHref.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var baseDir = Path.GetDirectoryName(page.RelativePath.Replace('\\', '/')) ?? "";
            var candidate = cleanHref.StartsWith('/')
                ? cleanHref.TrimStart('/')
                : Path.GetRelativePath(inputPath, Path.GetFullPath(Path.Combine(
                    inputPath, baseDir, cleanHref))).Replace('\\', '/');

            if (!htmlFiles.Contains(candidate))
            {
                diagnostics.Add(new ImportDiagnostic(
                    ImportDiagnosticSeverity.Warning,
                    "INVALID_INTERNAL_LINK",
                    $"内部 HTML 链接目标不存在: {href}",
                    page.FilePath));
            }
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"<script\b(?![^>]*\bsrc\s*=)[^>]*>.*?</script>", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline)]
    private static partial System.Text.RegularExpressions.Regex InlineScriptPattern();

    [System.Text.RegularExpressions.GeneratedRegex(@"<script\b[^>]*\bsrc\s*=\s*[""']https?://", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex ExternalScriptPattern();

    [System.Text.RegularExpressions.GeneratedRegex(@"<form\b[^>]*\baction\s*=\s*[""']https?://", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex ExternalFormActionPattern();

    [System.Text.RegularExpressions.GeneratedRegex(@"\bhref\s*=\s*[""'](?<href>[^""']+)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex LinkHrefPattern();
}
