using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Importing;

namespace Bukit.Labs.Cli.Commands;

public static class ImportCommand
{
    public static async Task<int> RunAsync(CliBoundCommand command)
    {
        var result = await ImportCommandWorkflow.RunAsync(CreateOptions(command));
        foreach (var message in result.Messages)
        {
            if (message.Level.Equals("error", StringComparison.OrdinalIgnoreCase))
                Console.Error.WriteLine(message.Message);
            else
                Console.WriteLine(message.Message);
        }

        return result.ExitCode;
    }

    private static ImportCommandOptions CreateOptions(CliBoundCommand command)
    {
        var subcommand = command.GetArgument(0) ?? "";
        var workingDir = Directory.GetCurrentDirectory();
        return new ImportCommandOptions
        {
            Subcommand = subcommand,
            RootDir = workingDir,
            WorkingDir = workingDir,
            ConfigPath = command.GetString("--config"),
            Site = command.GetString("--site"),
            DemoDir = subcommand == "html-demo" ? command.GetArgument(1) : null,
            SeedDir = subcommand == "seed" ? command.GetArgument(1) : null,
            OutputDir = command.GetString("--output"),
            ThemeName = command.GetString("--theme"),
            Force = command.GetBool("--force"),
            Use = command.GetBool("--use"),
            Verify = command.GetBool("--verify"),
            ExtractContent = !command.GetBool("--no-extract-content"),
            GenerateSeed = !command.GetBool("--no-seed"),
            ContentSource = command.GetString("--content-source") ?? "notion",
            BuildSource = command.GetString("--build-source") ?? "markdown",
            SitePath = command.GetString("--site-path"),
            Language = command.GetString("--language") ?? "zh",
            DryRun = command.GetBool("--dry-run"),
            StrictMode = ResolveStrictMode(command.GetString("--strict")),
            Overwrite = command.GetBool("--overwrite"),
            PreserveHtml = !command.GetBool("--no-preserve-html"),
            GenerateReport = !command.GetBool("--no-report"),
            BaseUrl = command.GetString("--base-url"),
            RouteMapPath = command.GetString("--route-map"),
            PushNotion = command.GetBool("--push-notion"),
            NotionDatabaseId = command.GetString("--notion-database-id"),
            NotionDatabaseMap = command.GetString("--notion-database-map"),
            CreateMissingNotionDatabases = command.GetBool("--create-missing-notion-databases"),
            NotionParentPageId = command.GetString("--notion-parent-page-id"),
            NotionGeneratedDatabaseMap = command.GetString("--notion-generated-database-map"),
            NotionTokenEnv = command.GetString("--notion-token-env") ?? "NOTION_TOKEN",
            NotionReport = command.GetString("--notion-report"),
            ValidateNotionSchema = !command.GetBool("--no-validate-notion-schema")
        };
    }

    private static string? ResolveStrictMode(string? value)
        => value is null
            ? null
            : string.Equals(value, "warn", StringComparison.OrdinalIgnoreCase) ? "warn" : "fail";
}
