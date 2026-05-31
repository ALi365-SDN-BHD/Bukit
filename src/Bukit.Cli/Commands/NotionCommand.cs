using System.Text;
using System.Text.Json;
using Bukit.Cli.Cli.Binding;

namespace Bukit.Cli.Commands;

public static class NotionCommand
{
    public static Task<int> RunAsync(CliBoundCommand command)
    {
        var sub = command.GetArgument(0) ?? "";
        return sub switch
        {
            "push" => PushAsync(command),
            _ => Unknown(sub)
        };
    }

    private static Task<int> PushAsync(CliBoundCommand command)
    {
        var input = command.GetString("--input");
        if (string.IsNullOrWhiteSpace(input))
        {
            Console.Error.WriteLine("缺少必填选项: --input <seed-dir>");
            return Task.FromResult(2);
        }

        var inputDir = Path.GetFullPath(input);
        if (!Directory.Exists(inputDir))
        {
            Console.Error.WriteLine($"seed 目录不存在: {inputDir}");
            return Task.FromResult(2);
        }

        var databaseId = command.GetString("--database-id");
        if (string.IsNullOrWhiteSpace(databaseId))
        {
            Console.Error.WriteLine("缺少必填选项: --database-id <id>");
            return Task.FromResult(2);
        }

        var dryRun = command.GetBool("--dry-run");
        var tokenEnv = command.GetString("--token-env") ?? "NOTION_TOKEN";
        if (!dryRun && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(tokenEnv)))
        {
            Console.Error.WriteLine($"{tokenEnv} is required for notion push. Use --dry-run to generate a local review plan.");
            return Task.FromResult(2);
        }

        var records = ImportSeedRecordReader.ReadDirectory(inputDir);
        var reportPath = command.GetString("--report");
        if (string.IsNullOrWhiteSpace(reportPath))
            reportPath = Path.Combine(inputDir, dryRun ? "notion-push-plan.json" : "notion-push-report.json");
        reportPath = Path.GetFullPath(reportPath);

        WritePlan(reportPath, databaseId, dryRun, records);
        Console.WriteLine($"notion push {(dryRun ? "dry-run" : "plan")} 完成: records={records.Count} report={reportPath}");

        if (!dryRun)
        {
            Console.WriteLine("Notion API write is staged behind the generated report in this draft implementation.");
            Console.WriteLine("Review the report, then rerun with --dry-run first in CI or implement database-specific property mapping.");
        }

        return Task.FromResult(0);
    }

    private static void WritePlan(string reportPath, string databaseId, bool dryRun, IReadOnlyList<ImportSeedRecord> records)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();
        writer.WriteBoolean("dryRun", dryRun);
        writer.WriteString("databaseId", databaseId);
        writer.WriteNumber("recordCount", records.Count);
        writer.WriteStartArray("records");
        foreach (var record in records)
        {
            writer.WriteStartObject();
            writer.WriteString("collection", record.Collection);
            writer.WriteString("title", record.Title);
            writer.WriteString("slug", record.Slug);
            writer.WriteString("language", record.Language);
            writer.WriteBoolean("published", record.Published);
            writer.WriteString("action", dryRun ? "review" : "pending");
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();

        File.WriteAllText(reportPath, Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static Task<int> Unknown(string sub)
    {
        Console.Error.WriteLine($"未知的 notion 子命令: {sub}");
        Console.Error.WriteLine("可用: push");
        return Task.FromResult(2);
    }
}
