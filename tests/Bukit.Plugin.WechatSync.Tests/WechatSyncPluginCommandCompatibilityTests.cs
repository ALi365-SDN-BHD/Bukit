using Bukit.Plugin.WechatSync;
using Xunit;

namespace Bukit.Plugin.WechatSync.Tests;

public sealed class WechatSyncPluginCommandCompatibilityTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly string[] SyncOptions =
    [
        "--output",
        "--manifest",
        "--cache-file",
        "--source-names",
        "--content-types",
        "--default-types-when-missing",
        "--target",
        "--author",
        "--default-thumb-media-id",
        "--default-image-url",
        "--site-name",
        "--site-url",
        "--base-url",
        "--media-download-dir",
        "--app-id-env",
        "--app-secret-env",
        "--force-retry-ignore-cache-env",
        "--max-attempts",
        "--base-delay-ms",
        "--backoff-factor",
        "--poll-max-attempts",
        "--poll-interval-seconds",
        "--force",
        "--dry-run",
        "--process-images",
        "--passthrough",
        "--need-open-comment",
        "--only-fans-can-comment"
    ];

    [Fact]
    public void Manifest_DeclaresWechatSyncCommandSurface()
    {
        var response = WechatSyncPluginManifestProvider.CreateManifestResponse("req-compat");

        var root = Assert.Single(response.Commands);
        Assert.Equal("wechat-sync", root.Name);

        var sync = Assert.Single(root.Subcommands);
        Assert.Equal("sync", sync.Name);
        Assert.Contains(sync.Options, option => option.Name == "--output" && option.Required);
        Assert.Equal(
            SyncOptions.OrderBy(value => value, StringComparer.Ordinal),
            sync.Options.Select(option => option.Name).OrderBy(value => value, StringComparer.Ordinal));

        var target = Assert.Single(sync.Options, option => option.Name == "--target");
        Assert.Equal(["draft", "publish"], target.AllowedValues);
    }

    [Fact]
    public void Manifest_RequiresPermissionsNeededByWechatDraftWorkflow()
    {
        var response = WechatSyncPluginManifestProvider.CreateManifestResponse("req-perms");

        Assert.True(response.RequiredPermissions.Network);
        Assert.Contains(".", response.RequiredPermissions.FileSystem.Read);
        Assert.Contains("./dist", response.RequiredPermissions.FileSystem.Read);
        Assert.Contains("./public", response.RequiredPermissions.FileSystem.Read);
        Assert.Contains(".cache/wechat-sync", response.RequiredPermissions.FileSystem.Write);
        Assert.Contains(".bukit/reports/plugin-output/wechat-sync", response.RequiredPermissions.FileSystem.Write);
        Assert.Contains("WECHAT_APP_ID", response.RequiredPermissions.Environment.Read);
        Assert.Contains("WECHAT_APP_SECRET", response.RequiredPermissions.Environment.Read);
        Assert.Contains("BUKIT_WECHAT_FORCE_RETRY", response.RequiredPermissions.Environment.Read);
    }

    [Fact]
    public void MinimalExample_DocumentsManifestAsCompatibilityFixtureUntilReleasePackageExists()
    {
        var readmePath = Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Plugins",
            "Bukit.Plugin.WechatSync",
            "examples",
            "minimal",
            "README.md");

        Assert.True(File.Exists(readmePath), $"Missing WeChat sync minimal fixture README: {readmePath}");
        var text = File.ReadAllText(readmePath);

        Assert.Contains("compatibility fixture", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a runnable release package", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("placeholder sha256", text, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepoRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current, "bukit-core.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
