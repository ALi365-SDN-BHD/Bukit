using Bukit.Plugin.Abstractions.Manifest;
using Bukit.Plugin.Abstractions.Security;

namespace Bukit.Plugin.WechatSync;

public static class WechatSyncPluginCommandSpecs
{
    public static IReadOnlyList<PluginCommandSpec> CreateCommands()
        =>
        [
            new PluginCommandSpec(
                Name: "wechat-sync",
                Description: "Sync Bukit build output to WeChat drafts or publish.",
                Subcommands:
                [
                    new PluginCommandSpec(
                        Name: "sync",
                        Description: "Sync built content to WeChat.",
                        Options:
                        [
                            StringOption("--output", "Bukit build output directory.", required: true),
                            StringOption("--manifest", "agent-manifest.json path."),
                            StringOption("--cache-file", "Sync cache path."),
                            StringOption("--source-names", "Comma-separated source names."),
                            StringOption("--content-types", "Comma-separated content types."),
                            StringOption("--default-types-when-missing", "Comma-separated fallback content types."),
                            StringOption("--target", "Target mode.", allowed: ["draft", "publish"]),
                            StringOption("--author", "WeChat author."),
                            StringOption("--default-thumb-media-id", "Fallback WeChat thumb media id."),
                            StringOption("--default-image-url", "Fallback image URL or project-relative path."),
                            StringOption("--site-name", "Site name."),
                            StringOption("--site-url", "Site absolute URL."),
                            StringOption("--base-url", "Site base URL."),
                            StringOption("--media-download-dir", "Bukit media cache directory."),
                            StringOption("--app-id-env", "Environment variable containing WeChat app id."),
                            StringOption("--app-secret-env", "Environment variable containing WeChat app secret."),
                            StringOption("--force-retry-ignore-cache-env", "Environment variable that forces cache bypass."),
                            StringOption("--max-attempts", "Draft retry attempts."),
                            StringOption("--base-delay-ms", "Retry base delay in milliseconds."),
                            StringOption("--backoff-factor", "Retry backoff factor."),
                            StringOption("--poll-max-attempts", "Publish status poll attempts."),
                            StringOption("--poll-interval-seconds", "Publish status poll interval."),
                            FlagOption("--force", "Ignore sync cache."),
                            FlagOption("--dry-run", "Load candidates without calling WeChat."),
                            FlagOption("--process-images", "Upload inline images to WeChat."),
                            FlagOption("--passthrough", "Skip HTML processing."),
                            FlagOption("--need-open-comment", "Enable comments."),
                            FlagOption("--only-fans-can-comment", "Restrict comments to followers.")
                        ])
                ])
        ];

    public static PluginPermissionSet CreateRequiredPermissions()
        => new(
            FileSystem: new PluginFileSystemPermission(
                Read: [".", "./dist", "./public", "./output"],
                Write: [".cache/wechat-sync", ".bukit/reports/plugin-output/wechat-sync"]),
            Network: true,
            Environment: new PluginEnvironmentPermission(Read: ["WECHAT_APP_ID", "WECHAT_APP_SECRET", "BUKIT_WECHAT_FORCE_RETRY"]));

    private static PluginOptionSpec StringOption(
        string name,
        string description,
        bool required = false,
        IReadOnlyList<string>? allowed = null)
        => new(name, "string", description, Required: required, AllowedValues: allowed);

    private static PluginOptionSpec FlagOption(string name, string description)
        => new(name, "flag", description);
}
