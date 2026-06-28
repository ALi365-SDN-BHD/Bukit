using System.Text.Json;
using System.Text;

namespace Bukit.Notion.RemoteSchema;

public static class NotionRemoteSchemaReportWriter
{
    public static void WriteJson(string path, NotionRemoteSchemaReport report)
    {
        EnsureDirectory(path);
        using FileStream stream = File.Create(path);
        JsonSerializer.Serialize(
            stream,
            report,
            NotionRemoteSchemaJsonSerializerContext.Default.NotionRemoteSchemaReport);
    }

    public static void WriteMarkdown(string path, NotionRemoteSchemaReport report)
    {
        EnsureDirectory(path);
        var builder = new StringBuilder();
        builder.AppendLine("# Notion Remote Schema Validation Report");
        builder.AppendLine();
        builder.AppendLine($"- Success: {report.Success.ToString().ToLowerInvariant()}");
        builder.AppendLine($"- Database map: {Escape(report.DatabaseMap)}");
        builder.AppendLine();
        builder.AppendLine("| Entry | Collection | Data source | Identifier source | Success | Title property | Unique field |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
        foreach (NotionRemoteSchemaDataSourceResult dataSource in report.DataSources)
        {
            builder.AppendLine(
                $"| {Escape(dataSource.Entry)} | {Escape(dataSource.Collection)} | {Escape(dataSource.DataSourceId)} | {Escape(dataSource.IdentifierSource)} | {dataSource.Success.ToString().ToLowerInvariant()} | {Escape(dataSource.TitleProperty)} | {Escape(dataSource.UniqueField)} |");
        }

        foreach (NotionRemoteSchemaDataSourceResult dataSource in report.DataSources)
        {
            builder.AppendLine();
            builder.AppendLine($"## Properties: {Escape(dataSource.Entry)}");
            builder.AppendLine();
            builder.AppendLine("| Property | Expected type | Actual type | Status |");
            builder.AppendLine("| --- | --- | --- | --- |");
            foreach (NotionRemoteSchemaPropertyResult property in dataSource.Properties)
            {
                builder.AppendLine(
                    $"| {Escape(property.Name)} | {Escape(property.ExpectedType)} | {Escape(property.ActualType)} | {Escape(property.Status)} |");
            }
        }

        if (report.Diagnostics.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Diagnostics");
            builder.AppendLine();
            builder.AppendLine("| Severity | Code | Message | Path |");
            builder.AppendLine("| --- | --- | --- | --- |");
            foreach (NotionRemoteSchemaDiagnostic diagnostic in report.Diagnostics)
            {
                builder.AppendLine(
                    $"| {Escape(diagnostic.Severity)} | {Escape(diagnostic.Code)} | {Escape(diagnostic.Message)} | {Escape(diagnostic.Path)} |");
            }
        }

        File.WriteAllText(path, builder.ToString());
    }

    private static void EnsureDirectory(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string Escape(string? value)
        => (value ?? string.Empty)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .ReplaceLineEndings(" ");
}
