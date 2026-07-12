namespace Bukit.Importing;

public sealed record ImportGeneratedNotionPushOptions
{
    public required ImportResult ImportResult { get; init; }
    public required string RootDir { get; init; }
    public required string ThemeName { get; init; }
    public string ContentSource { get; init; } = "notion";
    public string? DatabaseId { get; init; }
    public string? DatabaseMap { get; init; }
    public bool CreateMissingDatabases { get; init; }
    public string? ParentPageId { get; init; }
    public string? GeneratedDatabaseMap { get; init; }
    public string TokenEnv { get; init; } = "NOTION_TOKEN";
    public string? ReportPath { get; init; }
    public bool ValidateSchema { get; init; } = true;
    public bool DryRun { get; init; }
    public bool GenerateSeed { get; init; } = true;
}

public sealed record ImportNotionSeedPushOptions
{
    public required string InputDir { get; init; }
    public string? DatabaseId { get; init; }
    public string? DatabaseMapPath { get; init; }
    public bool CreateMissingDatabases { get; init; }
    public string? ParentPageId { get; init; }
    public string? GeneratedDatabaseMapPath { get; init; }
    public string TokenEnv { get; init; } = "NOTION_TOKEN";
    public string Mode { get; init; } = "create";
    public string UniqueField { get; init; } = "Slug";
    public string UpdateContent { get; init; } = "";
    public bool DryRun { get; init; }
    public string? ReportPath { get; init; }
    public bool ValidateSchema { get; init; } = true;
}

public sealed record ImportNotionSchemaValidationOptions
{
    public string? DatabaseId { get; init; }
    public string TokenEnv { get; init; } = "NOTION_TOKEN";
    public string? ReportPath { get; init; }
}

internal sealed record NotionDatabaseTarget(
    string Key,
    string Title,
    string SeedFile,
    string Collection,
    string? DatabaseId,
    string UniqueField,
    IReadOnlyDictionary<string, string>? Schema = null);
