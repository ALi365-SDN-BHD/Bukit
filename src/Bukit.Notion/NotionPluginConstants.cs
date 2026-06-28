namespace Bukit.Notion;

public static class NotionPluginConstants
{
    public const string Id = "notion";
    public const string Name = "Bukit Notion Plugin";
    public const string Version = "0.1.0";
    public const string CommandName = "notion";
    public const string TokenEnvironmentVariable = "NOTION_TOKEN";
    public const string RemoteSchemaReportFileName = "notion-schema-validation-report.json";
    public const string ReportOutputDirectory = "./.bukit/reports/plugin-output/notion";
    public const string TemporaryOutputDirectory = "./.bukit/tmp/notion";

    public static IReadOnlyList<string> AllowedTokenEnvironmentVariables { get; } =
        [TokenEnvironmentVariable];

    public static bool IsAllowedTokenEnvironmentVariable(string name)
        => AllowedTokenEnvironmentVariables.Contains(name, StringComparer.Ordinal);
}
