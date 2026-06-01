using System.Text;

namespace Bukit.Importing;

internal static class ImportReportWriter
{
    internal static void Write(HtmlDemoImportOptions options,
        ImportResult result, List<ImportDiagnostic> diagnostics)
    {
        if (options.DryRun)
        {
            WriteDryRunSummary(result);
            return;
        }

        var errors = diagnostics.Count(d => d.Severity == ImportDiagnosticSeverity.Error);
        var warnCount = diagnostics.Count(d => d.Severity == ImportDiagnosticSeverity.Warning);

        Console.WriteLine();
        Console.WriteLine($"迁移完成: {options.ThemeName}");
        Console.WriteLine($"  HTML 页面扫描:   {result.PagesFound}");
        Console.WriteLine($"  模板生成:        {result.TemplatesGenerated}");
        Console.WriteLine($"  局部模板生成:    {result.PartialsGenerated}");
        Console.WriteLine($"  组件识别:        {result.ComponentsGenerated}");
        Console.WriteLine($"  内容记录抽取:    {result.RecordsExtracted}");
        Console.WriteLine($"  资源复制:        {result.AssetsCopied}");
        Console.WriteLine($"  错误:            {errors}");
        Console.WriteLine($"  警告:            {warnCount}");
        Console.WriteLine($"  site.yaml:        {(result.SiteYamlCreated ? "已创建" : "已跳过（已存在）")}");
        Console.WriteLine($"  bukit.templates.yaml: {(result.TemplatesSynced ? "已创建" : "待同步")}");
        Console.WriteLine($"  notion-seed:      {(result.SeedGenerated ? "已生成" : "跳过")}");

        foreach (var w in result.Warnings)
            Console.WriteLine($"  注意: {w}");

        if (diagnostics.Count > 0)
        {
            Console.WriteLine();
            foreach (var d in diagnostics.Where(d => d.Severity >= ImportDiagnosticSeverity.Warning))
            {
                var prefix = d.Severity switch
                {
                    ImportDiagnosticSeverity.Error => "错误",
                    ImportDiagnosticSeverity.Warning => "警告",
                    _ => "信息"
                };
                var location = d.FilePath is not null ? $" ({d.FilePath}" +
                    (d.LineNumber.HasValue ? $":L{d.LineNumber}" : "") + ")" : "";
                Console.WriteLine($"  [{prefix}] {d.Code}: {d.Message}{location}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("后续步骤:");
        Console.WriteLine("  bukit dev");
        Console.WriteLine("  bukit build");
        Console.WriteLine("  bukit doctor");

        if (options.GenerateReport)
            WriteReportFile(options, result, diagnostics);
    }

    private static void WriteDryRunSummary(ImportResult result)
    {
        Console.WriteLine();
        Console.WriteLine("=== DRY-RUN 分析结果 ===");
        Console.WriteLine($"  HTML 页面扫描:   {result.PagesFound}");
        Console.WriteLine($"  将生成模板:      {result.TemplatesGenerated}");
        Console.WriteLine($"  将生成局部模板:  {result.PartialsGenerated}");
        Console.WriteLine($"  将生成组件:      {result.ComponentsGenerated}");
        Console.WriteLine($"  将抽取记录:      {result.RecordsExtracted}");
        Console.WriteLine($"  将复制资源:      {result.AssetsCopied}");
        Console.WriteLine("  (未写入任何文件)");
    }

    private static void WriteReportFile(HtmlDemoImportOptions options,
        ImportResult result, List<ImportDiagnostic> diagnostics)
    {
        var siteDir = options.SitePath ?? Path.Combine(options.RootDir, "sites", options.ThemeName);
        Directory.CreateDirectory(siteDir);
        var reportPath = Path.Combine(siteDir, "import-report.md");

        var errors = diagnostics.Count(d => d.Severity == ImportDiagnosticSeverity.Error);
        var warnCount = diagnostics.Count(d => d.Severity == ImportDiagnosticSeverity.Warning);

        var sb = new StringBuilder();
        sb.AppendLine("# HTML Demo Import Report");
        sb.AppendLine();
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine($"- Input: {options.InputPath}");
        sb.AppendLine($"- Theme: {options.ThemeName}");
        sb.AppendLine($"- Content Source: {options.ContentSource}");
        sb.AppendLine($"- Pages Found: {result.PagesFound}");
        sb.AppendLine($"- Templates Generated: {result.TemplatesGenerated}");
        sb.AppendLine($"- Partials Generated: {result.PartialsGenerated}");
        sb.AppendLine($"- Components Generated: {result.ComponentsGenerated}");
        sb.AppendLine($"- Records Extracted: {result.RecordsExtracted}");
        sb.AppendLine($"- Assets Copied: {result.AssetsCopied}");
        sb.AppendLine($"- Errors: {errors}");
        sb.AppendLine($"- Warnings: {warnCount}");

        if (result.Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Warnings");
            sb.AppendLine();
            foreach (var w in result.Warnings)
                sb.AppendLine($"- {w}");
        }

        if (diagnostics.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Diagnostics");
            sb.AppendLine();
            foreach (var d in diagnostics.Where(d => d.Severity >= ImportDiagnosticSeverity.Warning))
            {
                var prefix = d.Severity == ImportDiagnosticSeverity.Error ? "ERROR" : "WARNING";
                sb.AppendLine($"- [{prefix}] {d.Code}: {d.Message}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Build/Data Source Relationship");
        sb.AppendLine();
        sb.AppendLine("- Build uses the generated Markdown draft under `content/` so `bukit build` and `--verify` do not require external credentials.");
        sb.AppendLine($"- `{options.ContentSource}` seed files are generated for review/import and are not treated as a live build provider in this step.");

        sb.AppendLine();
        sb.AppendLine("## Hardcoded Residuals");
        sb.AppendLine();
        var residuals = result.Warnings
            .Where(w => w.Contains("硬编码", StringComparison.OrdinalIgnoreCase) ||
                        w.Contains("手动", StringComparison.OrdinalIgnoreCase) ||
                        w.Contains("审查", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (result.ComponentsGenerated > 0)
        {
            residuals.Add("Generated component templates preserve demo HTML structure; review links, button labels, and non-text attributes before publishing.");
        }
        if (diagnostics.Any(d => d.Code is "INLINE_SCRIPT" or "INLINE_EVENT_HANDLER" or "EXTERNAL_SCRIPT"))
        {
            residuals.Add("Script or event-handler diagnostics require manual review before production use.");
        }
        if (residuals.Count == 0)
        {
            sb.AppendLine("- No high-confidence hardcoded residuals detected automatically in generated templates.");
        }
        else
        {
            foreach (var residual in residuals)
                sb.AppendLine($"- {residual}");
        }

        sb.AppendLine();
        sb.AppendLine("## Manual Review Required");
        sb.AppendLine();
        if (diagnostics.Any(d => d.Severity >= ImportDiagnosticSeverity.Warning))
        {
            foreach (var d in diagnostics.Where(d => d.Severity >= ImportDiagnosticSeverity.Warning))
                sb.AppendLine($"- Review {d.Code}: {d.Message}");
        }
        else
        {
            sb.AppendLine("- Confirm extracted slugs, SEO descriptions, collection classification, and visual parity before publishing.");
        }

        sb.AppendLine();
        sb.AppendLine("## Next Steps");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("bukit dev");
        sb.AppendLine("bukit build");
        sb.AppendLine("bukit doctor");
        sb.AppendLine("```");

        File.WriteAllText(reportPath, sb.ToString());
        Console.WriteLine($"  导入报告已生成: {reportPath}");
    }
}
