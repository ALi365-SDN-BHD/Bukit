namespace Bukit.Importing;

public sealed record ImportCommandOptions
{
    public required string Subcommand { get; init; }
    public required string RootDir { get; init; }
    public required string WorkingDir { get; init; }
    public string? ConfigPath { get; init; }
    public string? Site { get; init; }
    public string? DemoDir { get; init; }
    public string? SeedDir { get; init; }
    public string? OutputDir { get; init; }
    public string? ThemeName { get; init; }
    public bool Force { get; init; }
    public bool Use { get; init; }
    public bool Verify { get; init; }
    public bool ExtractContent { get; init; } = true;
    public bool GenerateSeed { get; init; } = true;
    public string ContentSource { get; init; } = "notion";
    public string BuildSource { get; init; } = "markdown";
    public string? SitePath { get; init; }
    public string Language { get; init; } = "zh";
    public bool DryRun { get; init; }
    public string? StrictMode { get; init; }
    public bool Overwrite { get; init; }
    public bool PreserveHtml { get; init; } = true;
    public bool GenerateReport { get; init; } = true;
    public string? BaseUrl { get; init; }
    public string? RouteMapPath { get; init; }
    public bool PushNotion { get; init; }
    public string? NotionDatabaseId { get; init; }
    public string? NotionDatabaseMap { get; init; }
    public bool CreateMissingNotionDatabases { get; init; }
    public string? NotionParentPageId { get; init; }
    public string? NotionGeneratedDatabaseMap { get; init; }
    public string NotionTokenEnv { get; init; } = "NOTION_TOKEN";
    public string? NotionReport { get; init; }
    public bool ValidateNotionSchema { get; init; } = true;
}
