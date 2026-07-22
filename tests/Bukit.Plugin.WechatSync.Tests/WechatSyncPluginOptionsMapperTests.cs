using System.Text.Json;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Runtime;
using Bukit.Plugin.Abstractions.Security;
using Bukit.Plugin.WechatSync;
using Xunit;

namespace Bukit.Plugin.WechatSync.Tests;

public sealed class WechatSyncPluginOptionsMapperTests : IDisposable
{
    private readonly string _rootDir;

    public WechatSyncPluginOptionsMapperTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-plugin-wechat-sync-map-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public void Map_DryRun_PreservesCurrentWechatSyncOptionSemanticsWithoutCredentialGrant()
    {
        var request = Request(
            options: new Dictionary<string, JsonElement>
            {
                ["--output"] = Json("dist"),
                ["--manifest"] = Json("dist/agent-manifest.json"),
                ["--cache-file"] = Json(".cache/wechat-sync/cache.json"),
                ["--source-names"] = Json("notion,manual"),
                ["--content-types"] = Json("post,page"),
                ["--target"] = Json("publish"),
                ["--dry-run"] = Json(true),
                ["--passthrough"] = Json(true),
                ["--site-name"] = Json("Docs"),
                ["--site-url"] = Json("https://example.com"),
                ["--base-url"] = Json("blog")
            },
            permissions: new PluginPermissionSet());

        var invocation = WechatSyncPluginOptionsMapper.Map(request);

        Assert.True(invocation.DryRun);
        Assert.Equal(Path.Combine(_rootDir, "dist"), invocation.OutputDir);
        Assert.Equal(Path.Combine(_rootDir, "dist", "agent-manifest.json"), invocation.ManifestPath);
        Assert.Equal("publish", invocation.Options.Target);
        Assert.True(invocation.Options.Passthrough);
        Assert.Equal(new HashSet<string>(["notion", "manual"], StringComparer.OrdinalIgnoreCase), invocation.Options.SourceNames);
        Assert.Equal(new HashSet<string>(["post", "page"], StringComparer.OrdinalIgnoreCase), invocation.Options.ContentTypes);
        Assert.Equal(".cache/wechat-sync/cache.json", invocation.Options.CacheFile);
        Assert.Equal("/blog", invocation.Options.BaseUrl);
        Assert.Equal("Docs", invocation.Options.SiteName);
        Assert.Equal("https://example.com", invocation.Options.SiteUrl);
        Assert.Equal(
            new HashSet<string>(["reviewed", "verified", "approved"], StringComparer.OrdinalIgnoreCase),
            invocation.Options.DraftReviewStatuses);
        Assert.Equal(
            new HashSet<string>(["verified", "approved"], StringComparer.OrdinalIgnoreCase),
            invocation.Options.PublishReviewStatuses);
    }

    [Fact]
    public void Map_ReviewStatusPolicies_AreExplicitAndTargetSpecific()
    {
        var request = Request(
            options: new Dictionary<string, JsonElement>
            {
                ["--output"] = Json("dist"),
                ["--dry-run"] = Json(true),
                ["--draft-review-statuses"] = Json("reviewed, editorial-approved"),
                ["--publish-review-statuses"] = Json("EDITORIAL-APPROVED")
            });

        var invocation = WechatSyncPluginOptionsMapper.Map(request);

        Assert.Equal(
            new HashSet<string>(["reviewed", "editorial-approved"], StringComparer.OrdinalIgnoreCase),
            invocation.Options.DraftReviewStatuses);
        Assert.Equal(
            new HashSet<string>(["editorial-approved"], StringComparer.OrdinalIgnoreCase),
            invocation.Options.PublishReviewStatuses);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("*")]
    public void Map_ReviewStatusPolicies_RejectEmptyOrWildcardValues(string value)
    {
        var request = Request(
            options: new Dictionary<string, JsonElement>
            {
                ["--output"] = Json("dist"),
                ["--dry-run"] = Json(true),
                ["--publish-review-statuses"] = Json(value)
            });

        var ex = Assert.Throws<WechatSyncPluginOptionsException>(() => WechatSyncPluginOptionsMapper.Map(request));

        Assert.Equal("plugin.wechat-sync.invalidOption", ex.Code);
    }

    [Fact]
    public void Map_ReviewStatusPolicies_RejectPublishStatusOutsideDraftPolicy()
    {
        var request = Request(
            options: new Dictionary<string, JsonElement>
            {
                ["--output"] = Json("dist"),
                ["--dry-run"] = Json(true),
                ["--draft-review-statuses"] = Json("reviewed"),
                ["--publish-review-statuses"] = Json("verified")
            });

        var ex = Assert.Throws<WechatSyncPluginOptionsException>(() => WechatSyncPluginOptionsMapper.Map(request));

        Assert.Equal("plugin.wechat-sync.invalidOption", ex.Code);
        Assert.Contains("subset", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Map_NonDryRun_RejectsMissingNetworkGrant()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), "app");
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");

        var request = Request(
            options: new Dictionary<string, JsonElement>
            {
                ["--output"] = Json("dist"),
                ["--app-id-env"] = Json(appId.Name),
                ["--app-secret-env"] = Json(secret.Name),
                ["--force-retry-ignore-cache-env"] = Json("")
            },
            permissions: new PluginPermissionSet(Environment: new PluginEnvironmentPermission(Read: [appId.Name, secret.Name])));

        var ex = Assert.Throws<WechatSyncPluginOptionsException>(() => WechatSyncPluginOptionsMapper.Map(request));

        Assert.Equal("plugin.wechat-sync.networkDenied", ex.Code);
    }

    [Fact]
    public void Map_NonDryRun_RejectsMissingCredentialEnvironmentValue()
    {
        using var appId = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_APP_ID_" + Guid.NewGuid().ToString("N"), null);
        using var secret = new EnvironmentVariableScope("BUKIT_TEST_WECHAT_SECRET_" + Guid.NewGuid().ToString("N"), "secret");

        var request = Request(
            options: new Dictionary<string, JsonElement>
            {
                ["--output"] = Json("dist"),
                ["--app-id-env"] = Json(appId.Name),
                ["--app-secret-env"] = Json(secret.Name),
                ["--force-retry-ignore-cache-env"] = Json("")
            },
            permissions: new PluginPermissionSet(
                Network: true,
                Environment: new PluginEnvironmentPermission(Read: [appId.Name, secret.Name])));

        var ex = Assert.Throws<WechatSyncPluginOptionsException>(() => WechatSyncPluginOptionsMapper.Map(request));

        Assert.Equal("plugin.wechat-sync.envMissing", ex.Code);
    }

    [Fact]
    public void Map_RejectsCacheFileOutsideWechatSyncCacheDirectory()
    {
        var request = Request(
            options: new Dictionary<string, JsonElement>
            {
                ["--output"] = Json("dist"),
                ["--cache-file"] = Json("site.yaml"),
                ["--dry-run"] = Json(true)
            });

        var ex = Assert.Throws<WechatSyncPluginOptionsException>(() => WechatSyncPluginOptionsMapper.Map(request));

        Assert.Equal("plugin.wechat-sync.pathDenied", ex.Code);
        Assert.Contains(".cache/wechat-sync", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Map_RejectsOutputPathEscapingProjectRoot()
    {
        var request = Request(
            options: new Dictionary<string, JsonElement>
            {
                ["--output"] = Json("../dist"),
                ["--dry-run"] = Json(true)
            });

        var ex = Assert.Throws<WechatSyncPluginOptionsException>(() => WechatSyncPluginOptionsMapper.Map(request));

        Assert.Equal("plugin.wechat-sync.pathDenied", ex.Code);
    }

    [Fact]
    public void Map_RejectsOutputPathSymlinkEscapingProjectRoot()
    {
        var outsideDir = Path.Combine(Path.GetTempPath(), "bukit-plugin-wechat-map-output-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(_rootDir, "dist"), outsideDir);
            var request = Request(
                options: new Dictionary<string, JsonElement>
                {
                    ["--output"] = Json("dist"),
                    ["--dry-run"] = Json(true)
                });

            var ex = Assert.Throws<WechatSyncPluginOptionsException>(() => WechatSyncPluginOptionsMapper.Map(request));

            Assert.Equal("plugin.wechat-sync.pathDenied", ex.Code);
        }
        finally
        {
            Directory.Delete(outsideDir, recursive: true);
        }
    }

    [Fact]
    public void Map_RejectsCacheFileWhenCacheDirectoryIsSymlinkOutsideProject()
    {
        var outsideDir = Path.Combine(Path.GetTempPath(), "bukit-plugin-wechat-map-cache-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        Directory.CreateDirectory(Path.Combine(_rootDir, ".cache"));
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(_rootDir, ".cache", "wechat-sync"), outsideDir);
            var request = Request(
                options: new Dictionary<string, JsonElement>
                {
                    ["--output"] = Json("dist"),
                    ["--cache-file"] = Json(".cache/wechat-sync/sync-cache.json"),
                    ["--dry-run"] = Json(true)
                });

            var ex = Assert.Throws<WechatSyncPluginOptionsException>(() => WechatSyncPluginOptionsMapper.Map(request));

            Assert.Equal("plugin.wechat-sync.pathDenied", ex.Code);
        }
        finally
        {
            Directory.Delete(outsideDir, recursive: true);
        }
    }

    private PluginInvokeRequest Request(
        IReadOnlyDictionary<string, JsonElement> options,
        PluginPermissionSet? permissions = null)
        => new(
            Type: "invoke",
            Protocol: "bukit-plugin-v1",
            RequestId: "req",
            Host: new PluginHostInfo("Bukit", "1.0.0", "test"),
            Command: new PluginInvokeCommand("sync", Path: ["wechat-sync", "sync"], Options: options),
            Context: new PluginInvokeContext(_rootDir, _rootDir),
            Permissions: permissions ?? new PluginPermissionSet());

    private static JsonElement Json(string value)
        => JsonSerializer.SerializeToElement(value);

    private static JsonElement Json(bool value)
        => JsonSerializer.SerializeToElement(value);

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string? _previous;

        public EnvironmentVariableScope(string name, string? value)
        {
            Name = name;
            _previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public string Name { get; }

        public void Dispose()
            => Environment.SetEnvironmentVariable(Name, _previous);
    }
}
