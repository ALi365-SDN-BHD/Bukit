using Bukit.Cli.Cli.Binding;

namespace Bukit.Cli.Commands;

public static class NotionCommand
{
    internal static Func<HttpClient> CreateHttpClient { get; set; } = () => new HttpClient();

    public static Task<int> RunAsync(CliBoundCommand command)
    {
        var sub = command.GetArgument(0) ?? "";
        return sub switch
        {
            "push" => PushAsync(command),
            "validate-schema" => ValidateSchemaAsync(command),
            _ => Unknown(sub)
        };
    }

    private static async Task<int> PushAsync(CliBoundCommand command)
    {
        var input = command.GetString("--input");
        if (string.IsNullOrWhiteSpace(input))
        {
            Console.Error.WriteLine("缺少必填选项: --input <seed-dir>");
            return 2;
        }

        var inputDir = Path.GetFullPath(input);
        if (!Directory.Exists(inputDir))
        {
            Console.Error.WriteLine($"seed 目录不存在: {inputDir}");
            return 2;
        }

        var databaseId = command.GetString("--database-id");
        if (string.IsNullOrWhiteSpace(databaseId))
        {
            Console.Error.WriteLine("缺少必填选项: --database-id <id>");
            return 2;
        }

        var dryRun = command.GetBool("--dry-run");
        var tokenEnv = command.GetString("--token-env") ?? "NOTION_TOKEN";
        var mode = command.GetString("--mode") ?? "create";
        if (mode is not ("create" or "upsert"))
        {
            Console.Error.WriteLine($"不支持的推送模式: {mode}，可用: create | upsert");
            return 2;
        }
        var uniqueField = command.GetString("--unique-field") ?? "Slug";
        var updateContent = command.GetString("--update-content") ?? "";

        if (!dryRun && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(tokenEnv)))
        {
            Console.Error.WriteLine($"{tokenEnv} is required for notion push. Use --dry-run to generate a local review plan.");
            return 2;
        }

        var records = ImportSeedRecordReader.ReadDirectory(inputDir);
        var reportPath = command.GetString("--report");
        if (string.IsNullOrWhiteSpace(reportPath))
            reportPath = Path.Combine(inputDir, dryRun ? "notion-push-plan.json" : "notion-push-report.json");
        reportPath = Path.GetFullPath(reportPath);

        using var http = CreateHttpClient();
        var result = await NotionSeedPusher.PushAsync(http, records, new NotionPushOptions(
            DatabaseId: databaseId,
            Token: Environment.GetEnvironmentVariable(tokenEnv) ?? "",
            ReportPath: reportPath,
            DryRun: dryRun,
            Mode: mode,
            UniqueField: uniqueField,
            UpdateContent: updateContent));
        Console.WriteLine($"notion push {(dryRun ? "dry-run" : "api")} 完成: records={result.Total} created={result.Created} updated={result.Updated} failed={result.Failed} report={reportPath}");
        if (result.Failed > 0)
        {
            Console.Error.WriteLine("Notion push failed for one or more records. See report for details.");
            return 1;
        }

        return 0;
    }

    private static async Task<int> ValidateSchemaAsync(CliBoundCommand command)
    {
        var databaseId = command.GetString("--database-id");
        if (string.IsNullOrWhiteSpace(databaseId))
        {
            Console.Error.WriteLine("缺少必填选项: --database-id <id>");
            return 2;
        }

        var tokenEnv = command.GetString("--token-env") ?? "NOTION_TOKEN";
        var token = Environment.GetEnvironmentVariable(tokenEnv);
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.Error.WriteLine($"{tokenEnv} is required for notion validate-schema.");
            return 2;
        }

        var reportPath = command.GetString("--report");

        using var http = CreateHttpClient();
        var report = await NotionSchemaValidator.ValidateAsync(http, databaseId, token, reportPath);

        Console.WriteLine($"schema validation: {(report.Success ? "PASSED" : "FAILED")}");
        foreach (var f in report.FieldResults)
            Console.WriteLine($"  {f.Name,-18} {f.ExpectedType,-10} {f.Result}");
        if (!report.Success)
        {
            foreach (var e in report.Errors)
                Console.Error.WriteLine($"  ERROR: {e}");
            return 1;
        }

        return 0;
    }

    private static Task<int> Unknown(string sub)
    {
        Console.Error.WriteLine($"未知的 notion 子命令: {sub}");
        Console.Error.WriteLine("可用: push, validate-schema");
        return Task.FromResult(2);
    }
}
