namespace Bukit.Notion.Push;

public sealed record NotionPushOptions(
    string ProjectRoot,
    string SeedDirectory,
    string DatabaseMapPath,
    NotionPushMode Mode,
    bool DryRun,
    string ReportPath,
    string TokenEnvironmentVariable = NotionPluginConstants.TokenEnvironmentVariable,
    bool ConfirmReplace = false);
