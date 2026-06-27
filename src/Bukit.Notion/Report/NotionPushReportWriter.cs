using System.Text.Json;
using System.Text;
using Bukit.Notion.Push;

namespace Bukit.Notion.Report;

public static class NotionPushReportWriter
{
    public static void WriteJson(string path, NotionPushReport report)
    {
        EnsureDirectory(path);

        using FileStream stream = File.Create(path);
        JsonSerializer.Serialize(stream, report, NotionReportJsonSerializerContext.Default.NotionPushReport);
    }

    public static void WriteMarkdown(string path, NotionPushReport report)
    {
        EnsureDirectory(path);

        var builder = new StringBuilder();
        builder.AppendLine("# Notion Push Report");
        builder.AppendLine();
        builder.AppendLine($"- Dry run: {report.DryRun.ToString().ToLowerInvariant()}");
        builder.AppendLine($"- Mode: {report.Mode}");
        builder.AppendLine($"- Planned create: {report.PlannedCreate}");
        builder.AppendLine($"- Planned update: {report.PlannedUpdate}");
        builder.AppendLine($"- Planned upsert: {report.PlannedUpsert}");
        builder.AppendLine($"- Planned replace: {report.PlannedReplace}");
        builder.AppendLine($"- Records planned: {report.Planned}");
        builder.AppendLine($"- Created: {report.Created}");
        builder.AppendLine($"- Updated: {report.Updated}");
        builder.AppendLine($"- Replaced: {report.Replaced}");
        builder.AppendLine($"- Failed: {report.Failed}");
        builder.AppendLine($"- Skipped: {report.Skipped}");
        builder.AppendLine();
        builder.AppendLine("| Status | Operation | Collection | Seed file | Title | Unique field | Unique value | Data source | Remote page | Error code | Error message |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (NotionPushRecordResult record in report.Records)
        {
            builder.AppendLine(
                $"| {Escape(record.Status)} | {Escape(record.Operation)} | {Escape(record.Collection)} | {Escape(record.SeedFile)} | {Escape(record.Title)} | {Escape(record.UniqueField)} | {Escape(record.UniqueValue)} | {Escape(record.DataSourceId)} | {Escape(record.RemotePageId)} | {Escape(record.ErrorCode)} | {Escape(record.ErrorMessage)} |");
        }

        if (report.Diagnostics.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Diagnostics");
            builder.AppendLine();
            builder.AppendLine("| Severity | Code | Message | Path |");
            builder.AppendLine("| --- | --- | --- | --- |");
            foreach (NotionPushDiagnostic diagnostic in report.Diagnostics)
            {
                builder.AppendLine(
                    $"| {Escape(diagnostic.Severity)} | {Escape(diagnostic.Code)} | {Escape(diagnostic.Message)} | {Escape(diagnostic.Path)} |");
            }
        }

        File.WriteAllText(path, builder.ToString());
    }

    public static NotionPushReport CreateReport(
        NotionPushMode mode,
        bool dryRun,
        IReadOnlyList<NotionPushRecordResult> records,
        IReadOnlyList<NotionPushDiagnostic>? diagnostics = null)
    {
        string operation = ToOperation(mode);
        return new NotionPushReport(
            DryRun: dryRun,
            Mode: operation,
            PlannedCreate: records.Count(static record => string.Equals(record.Operation, "create", StringComparison.Ordinal)),
            PlannedUpdate: records.Count(static record => string.Equals(record.Operation, "update", StringComparison.Ordinal)),
            PlannedReplace: records.Count(static record => string.Equals(record.Operation, "replace", StringComparison.Ordinal)),
            Records: records,
            Diagnostics: diagnostics)
        {
            PlannedUpsert = records.Count(static record => string.Equals(record.Operation, "upsert", StringComparison.Ordinal)),
            Planned = CountStatus(records, NotionPushRecordStatus.Planned),
            Created = CountStatus(records, NotionPushRecordStatus.Created),
            Updated = CountStatus(records, NotionPushRecordStatus.Updated),
            Replaced = CountStatus(records, NotionPushRecordStatus.Replaced),
            Failed = CountStatus(records, NotionPushRecordStatus.Failed),
            Skipped = CountStatus(records, NotionPushRecordStatus.Skipped)
        };
    }

    public static string ToOperation(NotionPushMode mode)
        => mode switch
        {
            NotionPushMode.Create => "create",
            NotionPushMode.Upsert => "upsert",
            NotionPushMode.Replace => "replace",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };

    private static void EnsureDirectory(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static int CountStatus(IReadOnlyList<NotionPushRecordResult> records, string status)
        => records.Count(record => string.Equals(record.Status, status, StringComparison.Ordinal));

    private static string Escape(string? value)
        => (value ?? string.Empty).Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");
}
