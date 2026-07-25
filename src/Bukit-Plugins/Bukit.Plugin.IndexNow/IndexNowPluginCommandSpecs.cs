using Bukit.Plugin.Abstractions.Manifest;
using Bukit.Plugin.Abstractions.Security;

namespace Bukit.Plugin.IndexNow;

public static class IndexNowPluginCommandSpecs
{
    public static IReadOnlyList<PluginCommandSpec> CreateCommands()
        =>
        [
            new PluginCommandSpec(
                "indexnow",
                "Submit verified deployed URL changes through IndexNow.",
                Subcommands:
                [
                    new PluginCommandSpec(
                        "submit",
                        "Verify deployed URL changes and submit them through IndexNow.",
                        Options:
                        [
                            StringOption("--change-set", "Publish URL change-set path.", required: true),
                            StringOption("--snapshot", "Candidate publish URL snapshot path.", required: true),
                            StringOption("--site-url", "Exact production site URL.", required: true),
                            StringOption("--state-dir", "IndexNow state directory.", required: true),
                            new PluginOptionSpec("--dry-run", "flag", "Validate local inputs without network or writes.")
                        ])
                ])
        ];

    public static PluginPermissionSet CreateRequiredPermissions()
        => new(
            FileSystem: new PluginFileSystemPermission(
                Read: ["."],
                Write: [".cache/indexnow", "./dist"]),
            Network: true,
            Environment: new PluginEnvironmentPermission(Read: ["INDEXNOW_KEY"]));

    private static PluginOptionSpec StringOption(string name, string description, bool required)
        => new(name, "string", description, Required: required);
}
