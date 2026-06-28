using Bukit.Notion;
using Bukit.Plugin.Abstractions.Manifest;

namespace Bukit.Plugin.Notion;

public static class NotionCommandSpecFactory
{
    public static PluginCommandSpec CreateNotionCommand()
        => new(
            Name: NotionPluginConstants.CommandName,
            Description: "Validate and push Bukit handoff seed data to Notion.",
            Subcommands:
            [
                CreateValidateSeedCommand(),
                CreateValidateDatabaseMapCommand(),
                CreateSchemaCommand(),
                CreatePushCommand()
            ]);

    private static PluginCommandSpec CreateValidateSeedCommand()
        => new(
            Name: "validate-seed",
            Description: "Validate Import-generated notion seed artifacts.",
            Arguments:
            [
                new PluginArgumentSpec(
                    Name: "seed-dir",
                    Description: "Directory containing notion seed JSON files.",
                    Required: true)
            ]);

    private static PluginCommandSpec CreateValidateDatabaseMapCommand()
        => new(
            Name: "validate-database-map",
            Description: "Validate a notion-database-map.yaml handoff file.",
            Arguments:
            [
                new PluginArgumentSpec(
                    Name: "database-map",
                    Description: "Path to notion-database-map.yaml.",
                    Required: true)
            ]);

    private static PluginCommandSpec CreateSchemaCommand()
        => new(
            Name: "schema",
            Description: "Inspect and validate remote Notion data-source schemas.",
            Subcommands: [CreateSchemaValidateCommand()]);

    private static PluginCommandSpec CreateSchemaValidateCommand()
        => new(
            Name: "validate",
            Description: "Validate a local database map against remote Notion schemas.",
            Options:
            [
                new PluginOptionSpec(
                    Name: "--database-map",
                    Type: "string",
                    Description: "Path to notion-database-map.yaml.",
                    Required: true),
                new PluginOptionSpec(
                    Name: "--token-env",
                    Type: "string",
                    Description: "Allowlisted environment variable containing the Notion token.",
                    AllowedValues: [NotionPluginConstants.TokenEnvironmentVariable]),
                new PluginOptionSpec(
                    Name: "--report",
                    Type: "string",
                    Description: "Optional JSON report output path.")
            ]);

    private static PluginCommandSpec CreatePushCommand()
        => new(
            Name: "push",
            Description: "Push validated handoff seed data to Notion.",
            Options:
            [
                new PluginOptionSpec(
                    Name: "--seed",
                    Type: "string",
                    Description: "Directory containing notion seed JSON files.",
                    Required: true),
                new PluginOptionSpec(
                    Name: "--database-map",
                    Type: "string",
                    Description: "Path to notion-database-map.yaml.",
                    Required: true),
                new PluginOptionSpec(
                    Name: "--token-env",
                    Type: "string",
                    Description: "Allowlisted environment variable that contains the Notion token.",
                    AllowedValues: [NotionPluginConstants.TokenEnvironmentVariable]),
                new PluginOptionSpec(
                    Name: "--mode",
                    Type: "string",
                    Description: "Push mode: create, upsert, or replace.",
                    Required: true,
                    AllowedValues: ["create", "upsert", "replace"]),
                new PluginOptionSpec(
                    Name: "--dry-run",
                    Type: "flag",
                    Description: "Validate and report planned changes without writing to Notion."),
                new PluginOptionSpec(
                    Name: "--confirm-replace",
                    Type: "flag",
                    Description: "Required confirmation for destructive replace mode."),
                new PluginOptionSpec(
                    Name: "--report",
                    Type: "string",
                    Description: "Optional report output path.")
            ]);
}
