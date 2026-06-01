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
        Console.WriteLine($"  {SeedLabel(options),-16}{(result.SeedGenerated ? "已生成" : "跳过")}");

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

        if (options.ContentSource.Equals("notion", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            Console.WriteLine("  提示: content 已配置为 notion provider，使用 bukit notion push 推送内容到 Notion。");
        }

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

        if (result.ReportPages.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Pages");
            sb.AppendLine();
            sb.AppendLine("| Source | Route | Type | Template | Status |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var page in result.ReportPages)
                sb.AppendLine($"| {EscapeCell(page.Source)} | {EscapeCell(page.Route)} | {EscapeCell(page.Type)} | {EscapeCell(page.Template)} | {EscapeCell(page.Status)} |");
        }

        if (result.ReportComponents.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Components");
            sb.AppendLine();
            sb.AppendLine("| Component | Source | Status |");
            sb.AppendLine("|---|---|---|");
            foreach (var component in result.ReportComponents)
                sb.AppendLine($"| {EscapeCell(component.Name)} | {EscapeCell(component.Source)} | {EscapeCell(component.Status)} |");
        }

        if (result.ReportSeedFiles.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Content Seeds");
            sb.AppendLine();
            sb.AppendLine("| Seed File | Count |");
            sb.AppendLine("|---|---:|");
            foreach (var seed in result.ReportSeedFiles)
                sb.AppendLine($"| {EscapeCell(seed.FileName)} | {seed.Count} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Build/Data Source Relationship");
        sb.AppendLine();
        if (options.ContentSource.Equals("notion", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("- Build uses the Notion API (`provider: notion`). Ensure `NOTION_TOKEN` is set before running `bukit build` or `--verify`.");
            sb.AppendLine("- Seed files in `notion-seed/` are for push only and do not serve as a build source.");
        }
        else
        {
            sb.AppendLine("- Build uses the generated Markdown draft under `content/` so `bukit build` and `--verify` do not require external credentials.");
        }
        sb.AppendLine($"- `{options.ContentSource}` seed files are generated for review/import and are not treated as a live build provider in this step.");

        sb.AppendLine();
        sb.AppendLine("## Hardcoded Content Residue");
        sb.AppendLine();

        var hardcodedReport = result.HardcodedContentReport;
        if (hardcodedReport != null && hardcodedReport.Residues.Count > 0)
        {
            sb.AppendLine($"**Overall Score:** {hardcodedReport.OverallScore}/100 (lower is better)");
            sb.AppendLine();
            sb.AppendLine($"**Total Residual Text Count:** {hardcodedReport.TotalResidualCount}");
            sb.AppendLine();
            sb.AppendLine("| Template | Residual Text Count | Severity |");
            sb.AppendLine("|---|---:|---|");
            foreach (var r in hardcodedReport.Residues.OrderByDescending(r => r.ResidualTextCount))
                sb.AppendLine($"| {EscapeCell(r.TemplatePath)} | {r.ResidualTextCount} | {r.Severity} |");

            if (hardcodedReport.Residues.Any(r => r.ResidualSamples.Count > 0))
            {
                sb.AppendLine();
                sb.AppendLine("### Sample Residuals");
                sb.AppendLine();
                foreach (var r in hardcodedReport.Residues.Where(r => r.ResidualSamples.Count > 0).Take(3))
                {
                    sb.AppendLine($"**{EscapeCell(r.TemplatePath)}:**");
                    foreach (var sample in r.ResidualSamples.Take(3))
                        sb.AppendLine($"  - `{EscapeCell(sample.Trim().Length > 80 ? sample.Trim()[..80] + "..." : sample.Trim())}`");
                }
            }
        }
        else
        {
            sb.AppendLine("- No significant hardcoded content residues detected in generated templates.");
        }

        sb.AppendLine();
        sb.AppendLine("## Extraction Coverage");
        sb.AppendLine();
        sb.AppendLine("| Collection | Extracted | Coverage |");
        sb.AppendLine("|---|---:|---:|");
        sb.AppendLine($"| Pages | {result.RecordsExtracted} | — |");

        if (options.ContentSource.Equals("notion", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine();
            sb.AppendLine("- Notion push seed files are in `notion-seed/`. Run `bukit notion validate-schema` then `bukit notion push --mode upsert` to sync.");
            sb.AppendLine();
            sb.AppendLine("## Notion Provider Status");
            sb.AppendLine();
            var dbStatus = string.IsNullOrWhiteSpace(options.NotionDatabaseId)
                ? "${NOTION_DATABASE_ID} (environment variable)"
                : options.NotionDatabaseId;
            sb.AppendLine($"- provider: notion ✓");
            sb.AppendLine($"- databaseId: {dbStatus}");
            sb.AppendLine("- bukit build requires valid NOTION_TOKEN environment variable");
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

    private static string SeedLabel(HtmlDemoImportOptions options)
        => options.ContentSource.Equals("notion", StringComparison.OrdinalIgnoreCase)
            ? "notion-seed:"
            : $"{options.ContentSource}-seed:";

    private static string EscapeCell(string value)
        => value.Replace("|", "\\|");
}
