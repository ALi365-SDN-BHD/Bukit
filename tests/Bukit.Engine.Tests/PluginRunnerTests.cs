using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using System.Security.Cryptography;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class PluginRunnerTests
{
    [Fact]
    public void RunDerivePages_RecordsPluginExecutionInfo()
    {
        var ctx = CreateContext(plugins: DisableAfterBuildPlugins());

        PluginRunner.RunDerivePages(ctx);

        Assert.NotEmpty(ctx.PluginExecutions);
        var deriveExecs = ctx.PluginExecutions.Where(e => e.Hook == "derive-pages").ToList();
        Assert.NotEmpty(deriveExecs);
        Assert.All(deriveExecs, e =>
        {
            Assert.NotNull(e.Name);
            Assert.Equal("derive-pages", e.Hook);
            Assert.True(e.DurationMs >= 0);
        });
    }

    [Fact]
    public void RunAfterBuild_RecordsPluginExecutionInfo()
    {
        var ctx = CreateContext(root: CreateTempRoot(), siteUrl: "https://example.com", plugins: DisableDerivePlugins());

        PluginRunner.RunAfterBuild(ctx);

        Assert.NotEmpty(ctx.PluginExecutions);
        var afterBuildExecs = ctx.PluginExecutions.Where(e => e.Hook == "after-build").ToList();
        Assert.NotEmpty(afterBuildExecs);
        Assert.All(afterBuildExecs, e =>
        {
            Assert.NotNull(e.Name);
            Assert.Equal("after-build", e.Hook);
            Assert.True(e.DurationMs >= 0);
        });
    }

    // DESKTOP-REMOVED: ExternalAssembly plugin tests disabled (AOT-only).
#if false
    [Fact]
    public void FailedPlugin_InWarnMode_LogsButDoesNotThrow()
    {
#if AOT
        return; // ExternalAssemblyPluginSource not available in AOT
#endif
        var (root, pluginsDir) = SetupThrowingPlugin();
        Assert.NotNull(pluginsDir); // ThrowingPlugin.dll must be copied to test output

        var allowlist = BuildAllowlist(pluginsDir!);
        var ctx = CreateContext(root!, siteUrl: "https://example.com", pluginFailMode: "warn", plugins: DisableDerivePlugins(), externalAssemblyTrustMode: "strict", externalAssemblyAllowlist: allowlist);

        PluginRunner.RunAfterBuild(ctx);

        var throwingExec = ctx.PluginExecutions.FirstOrDefault(e => e.Name == "throwing-test");
        Assert.NotNull(throwingExec);
        Assert.False(throwingExec.Success);
        Assert.NotNull(throwingExec.Error);
        Assert.Contains("intentionally fails", throwingExec.Error);
    }

    [Fact]
    public void FailedPlugin_InStrictMode_Throws()
    {
#if AOT
        return; // ExternalAssemblyPluginSource not available in AOT
#endif
        var (root, pluginsDir) = SetupThrowingPlugin();
        Assert.NotNull(pluginsDir);

        var allowlist = BuildAllowlist(pluginsDir!);
        var ctx = CreateContext(root!, siteUrl: "https://example.com", pluginFailMode: "strict", plugins: DisableDerivePlugins(), externalAssemblyTrustMode: "strict", externalAssemblyAllowlist: allowlist);

        var ex = Assert.Throws<InvalidOperationException>(() => PluginRunner.RunAfterBuild(ctx));
        Assert.Contains("intentionally fails", ex.Message);
    }

    [Fact]
    public void ExternalAssemblyPlugin_DisallowedHash_InWarnMode_SkipsPlugin()
    {
#if AOT
        return;
#endif
        var (root, pluginsDir) = SetupThrowingPlugin();
        Assert.NotNull(pluginsDir);
        var allowlist = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ThrowingPlugin.dll"] = new string('f', 64)
        };
        var ctx = CreateContext(root!, siteUrl: "https://example.com", pluginFailMode: "warn", plugins: DisableDerivePlugins(), externalAssemblyTrustMode: "warn", externalAssemblyAllowlist: allowlist);

        PluginRunner.RunAfterBuild(ctx);

        Assert.DoesNotContain(ctx.PluginExecutions, x => x.Name == "throwing-test");
    }

    [Fact]
    public void ExternalAssemblyPlugin_DisallowedHash_InStrictMode_Throws()
    {
#if AOT
        return;
#endif
        var (root, pluginsDir) = SetupThrowingPlugin();
        Assert.NotNull(pluginsDir);
        var allowlist = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ThrowingPlugin.dll"] = new string('f', 64)
        };
        var ctx = CreateContext(root!, siteUrl: "https://example.com", pluginFailMode: "warn", plugins: DisableDerivePlugins(), externalAssemblyTrustMode: "strict", externalAssemblyAllowlist: allowlist);

        var ex = Assert.Throws<InvalidOperationException>(() => PluginRunner.RunAfterBuild(ctx));
        Assert.Contains("hash", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
#endif

    [Fact]
    public void Plugins_OrderedByOrderThenNameThenVersion()
    {
        var ctx = CreateContext(plugins: DisableAfterBuildPlugins());

        PluginRunner.RunDerivePages(ctx);

        var names = ctx.PluginExecutions.Where(e => e.Hook == "derive-pages").Select(e => e.Name!).ToList();
        var sorted = names.OrderBy(static x => x, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(sorted, names);
    }

    [Fact]
    public void PluginDisabledViaConfig_Skipped()
    {
        var plugins = new Dictionary<string, PluginToggleConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["pages-index"] = new PluginToggleConfig { Enabled = false },
            ["taxonomy"] = new PluginToggleConfig { Enabled = false },
            ["sitemap"] = new PluginToggleConfig { Enabled = false },
            ["feed"] = new PluginToggleConfig { Enabled = false },
            ["search-index"] = new PluginToggleConfig { Enabled = false },
            ["pagination"] = new PluginToggleConfig { Enabled = false },
            ["archive"] = new PluginToggleConfig { Enabled = false }
        };
        var ctx = CreateContext(plugins: plugins);

        PluginRunner.RunDerivePages(ctx);
        PluginRunner.RunAfterBuild(ctx);

        Assert.DoesNotContain(ctx.PluginExecutions, e => e.Name == "pages-index");
        Assert.DoesNotContain(ctx.PluginExecutions, e => e.Name == "sitemap");
    }

    [Fact]
    public void GetAllPlugins_UsesSingleRegistryBuild_ForSameContext()
    {
        PluginRegistry.ResetCacheForTests();
        var ctx = CreateContext();

        _ = PluginRegistry.GetAllPlugins(ctx).ToList();
        var first = PluginRegistry.CacheBuildCountForTests;
        _ = PluginRegistry.GetAllPlugins(ctx).ToList();
        var second = PluginRegistry.CacheBuildCountForTests;

        Assert.Equal(first, second);
    }

    private static BuildContext CreateContext(
        string? root = null,
        string? siteUrl = null,
        string pluginFailMode = "strict",
        IReadOnlyDictionary<string, PluginToggleConfig>? plugins = null)
    {
        root ??= CreateTempRoot();
        var outputDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(outputDir);

        var site = new SiteConfig
        {
            Name = "t",
            Title = "t",
            Url = siteUrl ?? "",
            PluginFailMode = pluginFailMode,
            Plugins = plugins
        };

        return new BuildContext
        {
            Config = new AppConfig
            {
                Site = site,
                Content = TestContent.Markdown()
            },
            RootDir = root,
            OutputDir = outputDir,
            BaseUrl = "/",
            LayoutsDir = Path.Combine(root, "layouts"),
            RoutedDocuments = Array.Empty<RoutedContentDocument>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };
    }

    // DESKTOP-REMOVED: ExternalAssembly tests disabled (AOT-only).
#if false
    private static (string root, string? pluginsDir) SetupThrowingPlugin()
    {
#if AOT
        return (CreateTempRoot(), null);
#else
        var root = CreateTempRoot();
        var pluginsDir = Path.Combine(root, "plugins");
        Directory.CreateDirectory(pluginsDir);

        var baseDir = AppContext.BaseDirectory;
        var dllPath = Path.Combine(baseDir, "ThrowingPlugin.dll");
        if (!File.Exists(dllPath))
            return (root, null);

        File.Copy(dllPath, Path.Combine(pluginsDir, "ThrowingPlugin.dll"), overwrite: true);
        return (root, pluginsDir);
#endif
    }

    private static IReadOnlyDictionary<string, string> BuildAllowlist(string pluginsDir)
    {
        var pluginPath = Path.Combine(pluginsDir, "ThrowingPlugin.dll");
        using var stream = File.OpenRead(pluginPath);
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ThrowingPlugin.dll"] = hash
        };
    }
#endif

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static IReadOnlyDictionary<string, PluginToggleConfig> DisableAfterBuildPlugins()
    {
        return new Dictionary<string, PluginToggleConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["sitemap"] = new PluginToggleConfig { Enabled = false },
            ["feed"] = new PluginToggleConfig { Enabled = false },
            ["search-index"] = new PluginToggleConfig { Enabled = false },
            ["taxonomy"] = new PluginToggleConfig { Enabled = false }
        };
    }

    private static IReadOnlyDictionary<string, PluginToggleConfig> DisableDerivePlugins()
    {
        return new Dictionary<string, PluginToggleConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["pages-index"] = new PluginToggleConfig { Enabled = false },
            ["taxonomy"] = new PluginToggleConfig { Enabled = false },
            ["pagination"] = new PluginToggleConfig { Enabled = false },
            ["archive"] = new PluginToggleConfig { Enabled = false }
        };
    }
}
