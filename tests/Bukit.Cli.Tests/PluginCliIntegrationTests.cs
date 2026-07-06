using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Bukit.Cli.Commands;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Cli.Shared.Cli.Metadata;
using Bukit.Plugin.Abstractions.Config;
using Bukit.Plugin.Abstractions.Manifest;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;
using Bukit.Plugin.Abstractions.Security;
using Bukit.Plugin.Echo;
using Bukit.Plugin.Import;
using Bukit.PluginHost;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class PluginCliIntegrationTests : IDisposable
{
    private readonly string _tempDir;

    public PluginCliIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-plugin-cli-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Fact]
    public void BukitCliComposer_RejectsPluginCommandConflictWithCore()
    {
        var core = BukitCliDescriptors.CreateDescriptors();
        var plugin = new CommandDescriptor(new CliCommandSpec("build", "Plugin build"), _ => Task.FromResult(0));

        ConfigException exception = Assert.Throws<ConfigException>(() => BukitCliComposer.Compose(core, [plugin]));

        Assert.Contains("Plugin command conflicts with core command", exception.Message);
    }

    [Fact]
    public async Task DisabledPluginCommand_ReturnsTwoAndPrintsDisabledMessage()
    {
        var descriptor = PluginCommandDescriptorFactory.CreateDisabled("echo", "echo");
        var result = await descriptor.DispatchAsync(
            Bukit.Cli.Shared.Cli.Parsing.CliParser.Parse(descriptor.Spec, ["hello"]));

        Assert.Equal(2, result);
    }

    [Fact]
    public async Task Main_PluginList_PrintsEnabledEchoPlugin()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        await InstallEchoPluginAsync(enabled: true);

        var result = await InvokeEntryPointAsync(["plugin", "list"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Plugins:", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("echo@1.0.0 enabled=true", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("commands=echo", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_PluginList_PrintsErrorRecordWhenOneEnabledPluginIsBad()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        await InstallEchoPluginAsync(enabled: true);
        File.WriteAllText(Path.Combine(_tempDir, ".bukit", "plugins.yaml"),
            """
            version: 1
            plugins:
              echo:
                enabled: true
                source: plugins/echo
                exposeCommands:
                  - echo
                allowInCi: true
                permissions:
                  network: false
              broken:
                enabled: true
                source: plugins/missing
                exposeCommands:
                  - broken
                allowInCi: true
                permissions:
                  network: false
            """);

        var result = await InvokeEntryPointAsync(["plugin", "list"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("echo@1.0.0 enabled=true", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("broken@error enabled=true", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("status=error", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("commands=broken", result.StdOut, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_PluginValidateConfig_PrintsOkForValidConfig()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, ".bukit"));
        File.WriteAllText(Path.Combine(_tempDir, ".bukit", "plugins.yaml"),
            """
            version: 1
            plugins:
              echo:
                enabled: false
                source: plugins/echo
                exposeCommands:
                  - echo
                permissions: {}
            """);

        var result = await InvokeEntryPointAsync(["plugin", "validate-config"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Plugin config OK:", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task Main_PluginValidateConfig_ReturnsTwoForInvalidConfig()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, ".bukit"));
        File.WriteAllText(Path.Combine(_tempDir, ".bukit", "plugins.yaml"),
            """
            version: 1
            plugins:
              echo:
                enabled: false
                source: plugins/echo
                exposeCommands:
                  - echo
            """);

        var result = await InvokeEntryPointAsync(["plugin", "validate-config"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Invalid plugin config:", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("plugins.echo.permissions", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_PluginValidateManifest_PrintsOkForValidManifest()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        WriteRuntimePermissionPluginConfig(
            pluginId: "echo",
            source: "plugins/echo",
            exposeCommands: ["echo"],
            staticCommands:
            """
            commands:
              - name: echo
                summary: Echo command
            """);

        var result = await InvokeEntryPointAsync(["plugin", "validate-manifest", "plugins/echo"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Plugin manifest OK:", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task Main_PluginValidateManifest_ReturnsTwoForInvalidManifest()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "plugins", "echo"));
        File.WriteAllText(Path.Combine(_tempDir, "plugins", "echo", "plugin.yaml"),
            """
            id: echo
            name: Echo
            version: 1.0.0
            protocol: bukit-plugin-v0
            kind: process
            distribution: self-contained
            platforms:
              test-rid:
                entry: bin/test-rid/plugin
                sha256: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
            """);

        var result = await InvokeEntryPointAsync(["plugin", "validate-manifest", "plugins/echo"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Invalid plugin manifest:", result.StdErr, StringComparison.Ordinal);
        Assert.Contains("protocol must be bukit-plugin-v1", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_EchoCommand_InvokesEchoPlugin()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        await InstallEchoPluginAsync(enabled: true);

        var result = await InvokeEntryPointAsync(["echo", "hello"]);

        Assert.Equal(0, result.ExitCode);
        using JsonDocument document = JsonDocument.Parse(result.StdOut.Trim());
        Assert.Equal("hello", document.RootElement.GetProperty("arguments")[0].GetString());
        Assert.Single(Directory.EnumerateFiles(
            Path.Combine(_tempDir, ".bukit", "reports", "plugin-executions"),
            "echo-invoke-*.json"));
        string lockText = File.ReadAllText(Path.Combine(_tempDir, ".bukit", "plugins.lock.yaml"));
        Assert.Contains("resolved:", lockText, StringComparison.Ordinal);
        Assert.Contains("entry: plugins/echo/bin/", lockText, StringComparison.Ordinal);
        Assert.Contains("protocol: bukit-plugin-v1", lockText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_DisabledEchoCommand_PrintsDisabledMessage()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        await InstallEchoPluginAsync(enabled: false, includeStaticCommand: true);

        var result = await InvokeEntryPointAsync(["echo", "hello"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Command disabled by plugin config: echo", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_DisabledPluginWithMissingSource_PrintsDisabledMessageFromExposeCommands()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, ".bukit"));
        File.WriteAllText(Path.Combine(_tempDir, ".bukit", "plugins.yaml"),
            """
            version: 1
            plugins:
              echo:
                enabled: false
                source: plugins/echo
                exposeCommands:
                  - echo
                permissions: {}
            """);

        var result = await InvokeEntryPointAsync(["echo", "hello"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Command disabled by plugin config: echo", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_OfficialImportFixture_ValidateManifest_PrintsOk()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        await InstallImportFixtureAsync(enabled: true);

        var result = await InvokeEntryPointAsync(["plugin", "validate-manifest", "plugins/import"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Plugin manifest OK:", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task Main_OfficialImportFixture_SeedCommand_WritesContentLockAndReport()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        await InstallImportFixtureAsync(enabled: true);
        CreateSeedInput("seed");

        var result = await InvokeEntryPointAsync(["import", "seed", "seed", "--output", "content", "--force"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("seed import 完成: records=1 written=1", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
        Assert.True(File.Exists(Path.Combine(_tempDir, "content", "posts", "hello.md")));
        string lockText = ReadImportLock();
        Assert.Contains("resolved:", lockText, StringComparison.Ordinal);
        Assert.Contains("  import:", lockText, StringComparison.Ordinal);
        Assert.Contains("source: plugins/import", lockText, StringComparison.Ordinal);
        Assert.Contains("entry: plugins/import/bin/", lockText, StringComparison.Ordinal);
        Assert.Contains("protocol: bukit-plugin-v1", lockText, StringComparison.Ordinal);
        string report = ReadSingleImportExecutionReport();
        Assert.Contains("\"pluginId\": \"import\"", report, StringComparison.Ordinal);
        Assert.Contains("\"commandPath\": [", report, StringComparison.Ordinal);
        Assert.Contains("\"seed\"", report, StringComparison.Ordinal);
        Assert.Contains("\"success\": true", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_OfficialImportFixture_NotionPushDryRun_RoutesThroughImportPlugin()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        await InstallImportFixtureAsync(enabled: true);
        CreateSeedInput("seed");

        var result = await InvokeEntryPointAsync([
            "notion",
            "push",
            "--input",
            "seed",
            "--database-id",
            "db-single",
            "--dry-run",
            "--report",
            "reports/notion-plan.json"
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("notion push dry-run 完成: records=1", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
        Assert.True(File.Exists(Path.Combine(_tempDir, "reports", "notion-plan.json")));
        string lockText = ReadImportLock();
        Assert.Contains("  import:", lockText, StringComparison.Ordinal);
        Assert.Contains("source: plugins/import", lockText, StringComparison.Ordinal);
        string report = ReadSingleImportExecutionReport();
        Assert.Contains("\"pluginId\": \"import\"", report, StringComparison.Ordinal);
        Assert.Contains("\"commandPath\": [", report, StringComparison.Ordinal);
        Assert.Contains("\"notion\"", report, StringComparison.Ordinal);
        Assert.Contains("\"push\"", report, StringComparison.Ordinal);
        Assert.Contains("\"success\": true", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_OfficialImportFixture_ExposeImportOnlyDoesNotExposeNotion()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        await InstallImportFixtureAsync(enabled: true);
        string configPath = Path.Combine(_tempDir, ".bukit", "plugins.yaml");
        string config = File.ReadAllText(configPath)
            .Replace("      - import\n      - notion", "      - import", StringComparison.Ordinal);
        File.WriteAllText(configPath, config);

        var result = await InvokeEntryPointAsync(["notion", "push", "--input", "seed", "--dry-run"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Unknown command: notion", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_OfficialImportFixture_HtmlDemoDryRun_ReturnsSuccessLockAndReport()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        await InstallImportFixtureAsync(enabled: true);
        CreateMinimalDemo("demo");

        var result = await InvokeEntryPointAsync(["import", "html-demo", "demo", "--theme", "demo", "--dry-run"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("未提取到共享布局", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "themes", "demo")));
        string lockText = ReadImportLock();
        Assert.Contains("source: plugins/import", lockText, StringComparison.Ordinal);
        Assert.Contains("entry: plugins/import/bin/", lockText, StringComparison.Ordinal);
        string report = ReadSingleImportExecutionReport();
        Assert.Contains("\"pluginId\": \"import\"", report, StringComparison.Ordinal);
        Assert.Contains("\"html-demo\"", report, StringComparison.Ordinal);
        Assert.Contains("\"success\": true", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Main_OfficialImportFixture_HtmlDemoPushNotionDryRun_ReturnsTwoAndMasksEnvInReport()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        await InstallImportFixtureAsync(enabled: true);
        CreateMinimalDemo("demo");
        string? originalToken = Environment.GetEnvironmentVariable("NOTION_TOKEN");
        Environment.SetEnvironmentVariable("NOTION_TOKEN", "task9-secret-token");

        try
        {
            var result = await InvokeEntryPointAsync(["import", "html-demo", "demo", "--theme", "demo", "--push-notion", "--dry-run"]);

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("--push-notion 不能与 --dry-run 同时使用。先生成草稿后再执行实际推送。", result.StdErr, StringComparison.Ordinal);
            string lockText = ReadImportLock();
            Assert.Contains("source: plugins/import", lockText, StringComparison.Ordinal);
            string report = ReadSingleImportExecutionReport();
            Assert.Contains("\"pluginId\": \"import\"", report, StringComparison.Ordinal);
            Assert.Contains("\"success\": false", report, StringComparison.Ordinal);
            Assert.Contains("\"NOTION_TOKEN\": \"***\"", report, StringComparison.Ordinal);
            Assert.DoesNotContain("task9-secret-token", report, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NOTION_TOKEN", originalToken);
        }
    }

    [Fact]
    public async Task Main_OfficialImportFixture_DisabledCommandDoesNotWriteLockOrReport()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        await InstallImportFixtureAsync(enabled: false);

        var result = await InvokeEntryPointAsync(["import"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Command disabled by plugin config: import", result.StdErr, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_tempDir, ".bukit", "plugins.lock.yaml")));
        Assert.False(Directory.Exists(Path.Combine(_tempDir, ".bukit", "reports", "plugin-executions")));
    }

    [Fact]
    public async Task LoadAsync_ExposeCommands_FiltersRuntimeManifestCommands()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        WriteRuntimePermissionPluginConfig(
            exposeCommands: ["allowed"],
            staticCommands:
            """
            commands:
              - name: allowed
                summary: Allowed command
              - name: hidden
                summary: Hidden command
            """);
        var client = new RuntimePermissionProtocolClient(
            new PluginPermissionSet(),
            commands: [new PluginCommandSpec("allowed", "Allowed"), new PluginCommandSpec("hidden", "Hidden")]);
        var loader = new PluginCliLoader(
            new PluginConfigLoader(),
            new PluginManifestLoader(),
            new PluginPathValidator(),
            new FixedPlatformResolver("test-rid"),
            new PassingHashVerifier(),
            client);

        PluginCliLoadResult result = await loader.LoadAsync(_tempDir, CancellationToken.None);

        Assert.NotNull(BukitCliDescriptors.ResolveDescriptor(result.Descriptors, "allowed"));
        Assert.Null(BukitCliDescriptors.ResolveDescriptor(result.Descriptors, "hidden"));
    }

    [Fact]
    public async Task LoadAsync_ExposeCommandMissingFromRuntimeManifest_ThrowsConfigException()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        WriteRuntimePermissionPluginConfig(
            exposeCommands: ["missing"],
            staticCommands:
            """
            commands:
              - name: allowed
                summary: Allowed command
            """);
        var client = new RuntimePermissionProtocolClient(
            new PluginPermissionSet(),
            commands: [new PluginCommandSpec("allowed", "Allowed")]);
        var loader = new PluginCliLoader(
            new PluginConfigLoader(),
            new PluginManifestLoader(),
            new PluginPathValidator(),
            new FixedPlatformResolver("test-rid"),
            new PassingHashVerifier(),
            client);

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(_tempDir, CancellationToken.None));

        Assert.Contains("exposeCommands contains unknown command: missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_RuntimeCommandNotDeclaredInStaticManifest_ThrowsConfigException()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        WriteRuntimePermissionPluginConfig(
            exposeCommands: ["hidden"],
            staticCommands:
            """
            commands:
              - name: allowed
                summary: Allowed command
            """);
        var client = new RuntimePermissionProtocolClient(
            new PluginPermissionSet(),
            commands: [new PluginCommandSpec("hidden", "Hidden")]);
        var loader = new PluginCliLoader(
            new PluginConfigLoader(),
            new PluginManifestLoader(),
            new PluginPathValidator(),
            new FixedPlatformResolver("test-rid"),
            new PassingHashVerifier(),
            client);

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(_tempDir, CancellationToken.None));

        Assert.Contains("runtime manifest command is not declared in plugin.yaml: hidden", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_EnabledPluginWithoutStaticCommands_ThrowsConfigException()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        WriteRuntimePermissionPluginConfig(staticCommands: string.Empty);
        var client = new RuntimePermissionProtocolClient(
            new PluginPermissionSet(),
            commands: [new PluginCommandSpec("runtime", "Runtime command")]);
        var loader = new PluginCliLoader(
            new PluginConfigLoader(),
            new PluginManifestLoader(),
            new PluginPathValidator(),
            new FixedPlatformResolver("test-rid"),
            new PassingHashVerifier(),
            client);

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(_tempDir, CancellationToken.None));

        Assert.Contains("plugin.yaml commands must contain at least one command", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_RuntimeOnlyManifestPolicy_AllowsMissingStaticCommands()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        WriteRuntimePermissionPluginConfig(staticCommands: string.Empty, manifestPolicy: "runtime-only");
        var client = new RuntimePermissionProtocolClient(
            new PluginPermissionSet(),
            commands: [new PluginCommandSpec("runtime", "Runtime command")]);
        var loader = new PluginCliLoader(
            new PluginConfigLoader(PluginRuntimeOnlyContext.Test),
            new PluginManifestLoader(),
            new PluginPathValidator(),
            new FixedPlatformResolver("test-rid"),
            new PassingHashVerifier(),
            client);

        PluginCliLoadResult result = await loader.LoadAsync(_tempDir, CancellationToken.None);

        Assert.NotNull(BukitCliDescriptors.ResolveDescriptor(result.Descriptors, "runtime"));
    }

    [Fact]
    public async Task LoadAsync_RuntimeOnlyManifestPolicy_InDefaultContext_ThrowsConfigException()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        WriteRuntimePermissionPluginConfig(staticCommands: string.Empty, manifestPolicy: "runtime-only");
        var client = new RuntimePermissionProtocolClient(
            new PluginPermissionSet(),
            commands: [new PluginCommandSpec("runtime", "Runtime command")]);
        var loader = new PluginCliLoader(
            new PluginConfigLoader(),
            new PluginManifestLoader(),
            new PluginPathValidator(),
            new FixedPlatformResolver("test-rid"),
            new PassingHashVerifier(),
            client);

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(_tempDir, CancellationToken.None));

        Assert.Contains("runtime-only is only allowed in development, Labs, or test contexts", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_SourceLeafDoesNotMatchConfiguredPluginId_ThrowsConfigException()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        WriteRuntimePermissionPluginConfig(
            pluginId: "import",
            source: "plugins/echo",
            manifestId: "import",
            exposeCommands: ["import"],
            staticCommands:
            """
            commands:
              - name: import
                summary: Import command
            """);
        var client = new RuntimePermissionProtocolClient(
            new PluginPermissionSet(),
            commands: [new PluginCommandSpec("import", "Import command")]);
        var loader = new PluginCliLoader(
            new PluginConfigLoader(),
            new PluginManifestLoader(),
            new PluginPathValidator(),
            new FixedPlatformResolver("test-rid"),
            new PassingHashVerifier(),
            client);

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(_tempDir, CancellationToken.None));

        Assert.Contains("Plugin import source plugins/echo does not match plugin id.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_ManifestIdDoesNotMatchConfiguredPluginId_ThrowsConfigException()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        WriteRuntimePermissionPluginConfig(
            pluginId: "import",
            source: "plugins/import",
            manifestId: "echo",
            exposeCommands: ["import"],
            staticCommands:
            """
            commands:
              - name: import
                summary: Import command
            """);
        var client = new RuntimePermissionProtocolClient(
            new PluginPermissionSet(),
            commands: [new PluginCommandSpec("import", "Import command")]);
        var loader = new PluginCliLoader(
            new PluginConfigLoader(),
            new PluginManifestLoader(),
            new PluginPathValidator(),
            new FixedPlatformResolver("test-rid"),
            new PassingHashVerifier(),
            client);

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(_tempDir, CancellationToken.None));

        Assert.Contains("Plugin import manifest id echo does not match configured plugin id.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_HandshakeIdDoesNotMatchManifestId_ThrowsConfigException()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        WriteRuntimePermissionPluginConfig(
            pluginId: "import",
            source: "plugins/import",
            manifestId: "import",
            exposeCommands: ["import"],
            staticCommands:
            """
            commands:
              - name: import
                summary: Import command
            """);
        var client = new RuntimePermissionProtocolClient(
            new PluginPermissionSet(),
            commands: [new PluginCommandSpec("import", "Import command")],
            handshakePluginId: "echo");
        var loader = new PluginCliLoader(
            new PluginConfigLoader(),
            new PluginManifestLoader(),
            new PluginPathValidator(),
            new FixedPlatformResolver("test-rid"),
            new PassingHashVerifier(),
            client);

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(_tempDir, CancellationToken.None));

        Assert.Contains("Plugin import handshake id echo does not match plugin.yaml id.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_ExposeCommandsMissing_ThrowsConfigException()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        WriteRuntimePermissionPluginConfig(exposeCommands: null, declareExposeCommands: false);
        var client = new RuntimePermissionProtocolClient(
            new PluginPermissionSet(),
            commands: [new PluginCommandSpec("allowed", "Allowed")]);
        var loader = new PluginCliLoader(
            new PluginConfigLoader(),
            new PluginManifestLoader(),
            new PluginPathValidator(),
            new FixedPlatformResolver("test-rid"),
            new PassingHashVerifier(),
            client);

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(_tempDir, CancellationToken.None));

        Assert.Contains("exposeCommands must be declared", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_ExposeCommandsEmpty_ExposesNoCommands()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        WriteRuntimePermissionPluginConfig(
            exposeCommands: [],
            staticCommands:
            """
            commands:
              - name: hidden
                summary: Hidden command
            """);
        var client = new RuntimePermissionProtocolClient(
            new PluginPermissionSet(),
            commands: [new PluginCommandSpec("hidden", "Hidden")]);
        var loader = new PluginCliLoader(
            new PluginConfigLoader(),
            new PluginManifestLoader(),
            new PluginPathValidator(),
            new FixedPlatformResolver("test-rid"),
            new PassingHashVerifier(),
            client);

        PluginCliLoadResult result = await loader.LoadAsync(_tempDir, CancellationToken.None);

        Assert.Null(BukitCliDescriptors.ResolveDescriptor(result.Descriptors, "hidden"));
        Assert.Empty(Assert.Single(result.Plugins).Commands);
    }

    [Fact]
    public async Task LoadAsync_DisabledPluginWithInvalidSource_ThrowsConfigException()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, ".bukit"));
        File.WriteAllText(Path.Combine(_tempDir, ".bukit", "plugins.yaml"),
            """
            version: 1
            plugins:
              echo:
                enabled: false
                source: ../plugins/echo
                exposeCommands:
                  - echo
                permissions: {}
            """);

        var loader = PluginCliLoader.CreateDefault();

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(_tempDir, CancellationToken.None));

        Assert.Contains("Path must not contain traversal segments", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_StaticRequiredPermissionNotGranted_ThrowsConfigException()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        await InstallEchoPluginAsync(enabled: true, requiredPermissions: "network: true");

        var loader = PluginCliLoader.CreateDefault();

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(_tempDir, CancellationToken.None));

        Assert.Contains("requires network permission", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_RuntimeRequiredPermissionNotGranted_ThrowsConfigException()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        WriteRuntimePermissionPluginConfig();
        var client = new RuntimePermissionProtocolClient(
            new PluginPermissionSet(Network: true));
        var loader = new PluginCliLoader(
            new PluginConfigLoader(),
            new PluginManifestLoader(),
            new PluginPathValidator(),
            new FixedPlatformResolver("test-rid"),
            new PassingHashVerifier(),
            client);

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(_tempDir, CancellationToken.None));

        Assert.Contains("requires network permission", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_SendsGrantedPermissionsToPlugin()
    {
        var client = new RuntimePermissionProtocolClient(new PluginPermissionSet());
        var granted = new PluginPermissionSet(
            Network: true,
            Environment: new PluginEnvironmentPermission(Read: ["BUKIT_ALLOWED"]));
        var plugin = new ResolvedPlugin(
            "echo",
            "1.0.0",
            "test-rid",
            "/tmp/echo",
            _tempDir,
            new PluginHostInfo("Bukit", "1.0.0", "test-rid"),
            GrantedPermissions: granted);
        var command = new PluginCommandSpec("echo", "Echo");

        int exitCode = await PluginCommandInvoker.InvokeAsync(
            new CliBoundCommand(new Dictionary<string, string?>(), ["hello"]),
            plugin,
            command,
            client);

        Assert.Equal(0, exitCode);
        Assert.Same(granted, client.LastInvokeRequest?.Permissions);
    }

    [Fact]
    public async Task InvokeAsync_PrintsPluginDiagnosticsAndReturnsPluginExitCode()
    {
        var client = new RuntimePermissionProtocolClient(
            new PluginPermissionSet(),
            invokeResponse: new PluginInvokeResponse(
                "invokeResponse",
                "bukit-plugin-v1",
                "req-3",
                Success: false,
                ExitCode: 2,
                Diagnostics: [new PluginDiagnostic("plugin.input.invalid", "error", "Invalid input")]));
        var plugin = new ResolvedPlugin(
            "echo",
            "1.0.0",
            "test-rid",
            "/tmp/echo",
            _tempDir,
            new PluginHostInfo("Bukit", "1.0.0", "test-rid"));

        var result = await CaptureConsoleAsync(() => PluginCommandInvoker.InvokeAsync(
            new CliBoundCommand(new Dictionary<string, string?>(), ["hello"]),
            plugin,
            new PluginCommandSpec("echo", "Echo"),
            client));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("plugin.input.invalid: Invalid input", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginDescriptorFactory_MapsRequiredPluginOptionToCliOption()
    {
        var plugin = new ResolvedPlugin(
            "import",
            "1.0.0",
            "test-rid",
            "/tmp/import",
            _tempDir,
            new PluginHostInfo("Bukit", "1.0.0", "test-rid"));
        var descriptor = PluginCommandDescriptorFactory.Create(
            plugin,
            new PluginCommandSpec(
                "import",
                "Import",
                Options: [new PluginOptionSpec("--source", "string", "Source", Required: true)]),
            new RuntimePermissionProtocolClient(new PluginPermissionSet()));

        CliOptionSpec option = Assert.Single(descriptor.Spec.Options!);
        Assert.True(option.Required);
    }

    [Fact]
    public async Task PluginSubcommandInvoke_UsesFullCommandPathAndSubcommandOptions()
    {
        var client = new RuntimePermissionProtocolClient(new PluginPermissionSet());
        var plugin = new ResolvedPlugin(
            "import",
            "1.0.0",
            "test-rid",
            "/tmp/import",
            _tempDir,
            new PluginHostInfo("Bukit", "1.0.0", "test-rid"));
        var command = new PluginCommandSpec(
            "import",
            "Import",
            Subcommands:
            [
                new PluginCommandSpec(
                    "html-demo",
                    "Import HTML demo",
                    Arguments: [new PluginArgumentSpec("source", "Source directory", Required: true)],
                    Options: [new PluginOptionSpec("--theme", "string", "Theme", Required: true)])
            ]);
        var descriptor = PluginCommandDescriptorFactory.Create(plugin, command, client);
        var parsed = Bukit.Cli.Shared.Cli.Parsing.CliParser.Parse(
            descriptor.Spec,
            ["html-demo", "./demo", "--theme", "x"]);

        Assert.True(parsed.IsSuccess);
        int exitCode = await descriptor.DispatchAsync(parsed);

        Assert.Equal(0, exitCode);
        Assert.NotNull(client.LastInvokeRequest);
        Assert.Equal("html-demo", client.LastInvokeRequest!.Command.Name);
        Assert.Equal(["import", "html-demo"], client.LastInvokeRequest.Command.Path);
        Assert.Equal(["./demo"], client.LastInvokeRequest.Command.Arguments);
        Assert.Equal("x", client.LastInvokeRequest.Command.Options["--theme"].GetString());
    }

    [Fact]
    public async Task PluginInvoke_UsesTypedJsonOptionValues()
    {
        var client = new RuntimePermissionProtocolClient(new PluginPermissionSet());
        var plugin = new ResolvedPlugin(
            "import",
            "1.0.0",
            "test-rid",
            "/tmp/import",
            _tempDir,
            new PluginHostInfo("Bukit", "1.0.0", "test-rid"));
        var command = new PluginCommandSpec(
            "import",
            "Import",
            Options:
            [
                new PluginOptionSpec("--force", "flag", "Force"),
                new PluginOptionSpec("--jobs", "integer", "Jobs"),
                new PluginOptionSpec("--ratio", "number", "Ratio"),
                new PluginOptionSpec("--theme", "string", "Theme")
            ]);
        var descriptor = PluginCommandDescriptorFactory.Create(plugin, command, client);
        var parsed = Bukit.Cli.Shared.Cli.Parsing.CliParser.Parse(
            descriptor.Spec,
            ["--force", "--jobs", "4", "--ratio", "1.5", "--theme", "x"]);

        Assert.True(parsed.IsSuccess);
        int exitCode = await descriptor.DispatchAsync(parsed);

        Assert.Equal(0, exitCode);
        Assert.NotNull(client.LastInvokeRequest);
        IReadOnlyDictionary<string, JsonElement> options = client.LastInvokeRequest!.Command.Options;
        Assert.Equal(JsonValueKind.True, options["--force"].ValueKind);
        Assert.Equal(JsonValueKind.Number, options["--jobs"].ValueKind);
        Assert.Equal(4, options["--jobs"].GetInt32());
        Assert.Equal(JsonValueKind.Number, options["--ratio"].ValueKind);
        Assert.Equal(1.5, options["--ratio"].GetDouble());
        Assert.Equal(JsonValueKind.String, options["--theme"].ValueKind);
        Assert.Equal("x", options["--theme"].GetString());
    }

    [Fact]
    public async Task PluginInvoke_RejectsNonFiniteNumberOptionValues()
    {
        var client = new RuntimePermissionProtocolClient(new PluginPermissionSet());
        var plugin = new ResolvedPlugin(
            "import",
            "1.0.0",
            "test-rid",
            "/tmp/import",
            _tempDir,
            new PluginHostInfo("Bukit", "1.0.0", "test-rid"));
        var command = new PluginCommandSpec(
            "import",
            "Import",
            Options:
            [
                new PluginOptionSpec("--ratio", "number", "Ratio")
            ]);

        CommandArgumentException exception = await Assert.ThrowsAsync<CommandArgumentException>(
            () => PluginCommandInvoker.InvokeAsync(
                new CliBoundCommand(new Dictionary<string, string?> { ["--ratio"] = "NaN" }, []),
                plugin,
                command,
                client));

        Assert.Contains("Invalid value for --ratio", exception.Message, StringComparison.Ordinal);
    }

    private async Task InstallEchoPluginAsync(bool enabled, bool includeStaticCommand = true, string? requiredPermissions = null)
    {
        var resolver = new PluginPlatformResolver();
        string rid = resolver.GetCurrentRid();
        string pluginRoot = Path.Combine(_tempDir, "plugins/echo");
        string binRoot = Path.Combine(pluginRoot, "bin", rid);
        Directory.CreateDirectory(binRoot);
        string executablePath = CopyEchoPlugin(binRoot);
        string sha256 = await Sha256Async(executablePath);

        Directory.CreateDirectory(Path.Combine(_tempDir, ".bukit"));
        File.WriteAllText(Path.Combine(_tempDir, ".bukit", "plugins.yaml"),
            $$"""
            version: 1
            plugins:
              echo:
                enabled: {{enabled.ToString().ToLowerInvariant()}}
                source: plugins/echo
                exposeCommands:
                  - echo
                allowInCi: true
                permissions:
                  network: false
            """);

        string commands = includeStaticCommand
            ? """
            commands:
              - name: echo
                summary: Echo command
            """
            : string.Empty;

        string permissions = requiredPermissions is null
            ? string.Empty
            : $$"""
            requiredPermissions:
              {{requiredPermissions}}
            """;

        File.WriteAllText(Path.Combine(pluginRoot, "plugin.yaml"),
            $$"""
            id: echo
            name: Bukit Echo Plugin
            version: 1.0.0
            protocol: bukit-plugin-v1
            kind: process
            distribution: self-contained
            platforms:
              {{rid}}:
                entry: bin/{{rid}}/{{Path.GetFileName(executablePath)}}
                sha256: {{sha256}}
            {{commands}}
            {{permissions}}
            """);
    }

    private void WriteRuntimePermissionPluginConfig(
        string pluginId = "runtime",
        string source = "plugins/runtime",
        string? manifestId = null,
        IReadOnlyList<string>? exposeCommands = null,
        bool declareExposeCommands = true,
        string? staticCommands = null,
        string? manifestPolicy = null)
    {
        string pluginRoot = Path.Combine(_tempDir, source.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.Combine(_tempDir, ".bukit"));
        Directory.CreateDirectory(Path.Combine(pluginRoot, "bin", "test-rid"));
        IReadOnlyList<string> effectiveExposeCommands = exposeCommands ?? ["runtime"];
        string exposeBlock = !declareExposeCommands
            ? string.Empty
            : effectiveExposeCommands.Count == 0
                ? "    exposeCommands: []"
                : $"""
                exposeCommands:
            {string.Join(Environment.NewLine, effectiveExposeCommands.Select(command => $"    - {command}"))}
            """;
        string manifestPolicyLine = manifestPolicy is null
            ? string.Empty
            : $"    manifestPolicy: {manifestPolicy}";
        File.WriteAllText(Path.Combine(_tempDir, ".bukit", "plugins.yaml"),
            $$"""
            version: 1
            plugins:
              {{pluginId}}:
                enabled: true
                source: {{source}}
            {{exposeBlock}}
            {{manifestPolicyLine}}
                allowInCi: true
                permissions:
                  network: false
            """);
        string commands = staticCommands ??
            """
            commands:
              - name: runtime
                summary: Runtime command
            """;

        File.WriteAllText(Path.Combine(pluginRoot, "plugin.yaml"),
            $$"""
            id: {{manifestId ?? pluginId}}
            name: Runtime Permission Plugin
            version: 1.0.0
            protocol: bukit-plugin-v1
            kind: process
            distribution: self-contained
            platforms:
              test-rid:
                entry: bin/test-rid/plugin
                sha256: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
            {{commands}}
            """);
    }

    private async Task InstallImportFixtureAsync(bool enabled)
    {
        var resolver = new PluginPlatformResolver();
        string rid = resolver.GetCurrentRid();
        CopyDirectory(FindImportFixtureRoot(), _tempDir);
        string pluginRoot = Path.Combine(_tempDir, "plugins/import");
        string binRoot = Path.Combine(pluginRoot, "bin", rid);
        Directory.CreateDirectory(binRoot);
        string executablePath = CopyImportPlugin(binRoot);
        string sha256 = await Sha256Async(executablePath);

        string configPath = Path.Combine(_tempDir, ".bukit", "plugins.yaml");
        string config = File.ReadAllText(configPath);
        config = config.Replace("enabled: true", $"enabled: {enabled.ToString().ToLowerInvariant()}", StringComparison.Ordinal);
        File.WriteAllText(configPath, config);

        string manifestPath = Path.Combine(pluginRoot, "plugin.yaml");
        string manifest = File.ReadAllText(manifestPath);
        File.WriteAllText(manifestPath, ReplaceImportFixturePlatform(manifest, rid, Path.GetFileName(executablePath), sha256));
    }

    private string ReadImportLock()
        => File.ReadAllText(Path.Combine(_tempDir, ".bukit", "plugins.lock.yaml"));

    private string ReadSingleImportExecutionReport()
    {
        string reportPath = Assert.Single(Directory.EnumerateFiles(
            Path.Combine(_tempDir, ".bukit", "reports", "plugin-executions"),
            "import-invoke-*.json"));
        return File.ReadAllText(reportPath);
    }

    private void CreateSeedInput(string relativePath)
    {
        string seedDir = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(seedDir);
        File.WriteAllText(Path.Combine(seedDir, "posts.json"), """
[
  { "title": "Hello", "slug": "hello", "content": "Body" }
]
""");
    }

    private void CreateMinimalDemo(string relativePath)
    {
        string demoDir = Path.Combine(_tempDir, relativePath);
        Directory.CreateDirectory(demoDir);
        File.WriteAllText(Path.Combine(demoDir, "index.html"), """
<!doctype html>
<html>
<head><title>Demo</title></head>
<body><main><h1>Hello</h1><p>World</p></main></body>
</html>
""");
    }

    private static string FindImportFixtureRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "src",
                "Bukit-Plugins",
                "Bukit.Plugin.Import",
                "examples",
                "minimal");
            if (File.Exists(Path.Combine(candidate, "plugins", "import", "plugin.yaml")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Bukit.Plugin.Import examples/minimal fixture.");
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (string directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destinationDirectory, Path.GetRelativePath(sourceDirectory, directory)));
        }

        foreach (string file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destinationDirectory, Path.GetRelativePath(sourceDirectory, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static string ReplaceImportFixturePlatform(string manifest, string rid, string executableName, string sha256)
    {
        const string fixturePlatform = """
        platforms:
          osx-arm64:
            entry: bin/osx-arm64/bukit-plugin-import
            sha256: 0000000000000000000000000000000000000000000000000000000000000000
        """;
        string runtimePlatform = $$"""
        platforms:
          {{rid}}:
            entry: bin/{{rid}}/{{executableName}}
            sha256: {{sha256}}
        """;
        string replaced = manifest.Replace(fixturePlatform, runtimePlatform, StringComparison.Ordinal);
        if (ReferenceEquals(replaced, manifest) || replaced == manifest)
        {
            throw new InvalidOperationException("Import fixture platform block was not replaced.");
        }

        return replaced;
    }

    private static string CopyEchoPlugin(string destinationDirectory)
    {
        string echoAssemblyPath = typeof(EchoPluginMarker).Assembly.Location;
        string echoOutputDirectory = Path.GetDirectoryName(echoAssemblyPath)!;
        string executableName = OperatingSystem.IsWindows() ? "bukit-plugin-echo.exe" : "bukit-plugin-echo";

        foreach (string file in Directory.EnumerateFiles(echoOutputDirectory))
        {
            string target = Path.Combine(destinationDirectory, Path.GetFileName(file));
            File.Copy(file, target, overwrite: true);
        }

        string executablePath = Path.Combine(destinationDirectory, executableName);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                executablePath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        return executablePath;
    }

    private static string CopyImportPlugin(string destinationDirectory)
    {
        string importAssemblyPath = typeof(ImportPluginApp).Assembly.Location;
        string importOutputDirectory = Path.GetDirectoryName(importAssemblyPath)!;
        string executableName = OperatingSystem.IsWindows() ? "bukit-plugin-import.exe" : "bukit-plugin-import";

        foreach (string file in Directory.EnumerateFiles(importOutputDirectory))
        {
            string target = Path.Combine(destinationDirectory, Path.GetFileName(file));
            File.Copy(file, target, overwrite: true);
        }

        string executablePath = Path.Combine(destinationDirectory, executableName);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                executablePath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        return executablePath;
    }

    private static async Task<string> Sha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> InvokeEntryPointAsync(string[] args)
    {
        var entryPoint = typeof(VersionCommand).Assembly.EntryPoint ?? throw new InvalidOperationException("Missing Bukit.Cli entry point.");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var result = entryPoint.Invoke(null, [args]);
            var exitCode = result switch
            {
                Task<int> task => await task,
                Task task => await AwaitAndReturnZeroAsync(task),
                int code => code,
                _ => throw new InvalidOperationException($"Unsupported entry point return type: {result?.GetType().FullName ?? "null"}")
            };

            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    private static async Task<int> AwaitAndReturnZeroAsync(Task task)
    {
        await task;
        return 0;
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> CaptureConsoleAsync(Func<Task<int>> action)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            int exitCode = await action();
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    private sealed class FixedPlatformResolver : IPluginPlatformResolver
    {
        private readonly string _rid;

        public FixedPlatformResolver(string rid)
        {
            _rid = rid;
        }

        public string GetCurrentRid() => _rid;
    }

    private sealed class PassingHashVerifier : IPluginHashVerifier
    {
        public Task<PluginHashVerificationResult> VerifySha256Async(
            string filePath,
            string expectedSha256,
            CancellationToken cancellationToken)
            => Task.FromResult(new PluginHashVerificationResult(true, expectedSha256, expectedSha256));
    }

    private sealed class RuntimePermissionProtocolClient : IPluginProtocolClient
    {
        private readonly PluginPermissionSet _runtimePermissions;
        private readonly IReadOnlyList<PluginCommandSpec> _commands;
        private readonly PluginInvokeResponse _invokeResponse;
        private readonly string? _handshakePluginId;

        public RuntimePermissionProtocolClient(
            PluginPermissionSet runtimePermissions,
            IReadOnlyList<PluginCommandSpec>? commands = null,
            PluginInvokeResponse? invokeResponse = null,
            string? handshakePluginId = null)
        {
            _runtimePermissions = runtimePermissions;
            _commands = commands ?? [new PluginCommandSpec("runtime", "Runtime command")];
            _invokeResponse = invokeResponse ?? new PluginInvokeResponse(
                "invokeResponse",
                "bukit-plugin-v1",
                "req-3",
                Success: true,
                ExitCode: 0);
            _handshakePluginId = handshakePluginId;
        }

        public PluginInvokeRequest? LastInvokeRequest { get; private set; }

        public Task<PluginHandshakeResponse> HandshakeAsync(ResolvedPlugin plugin, CancellationToken cancellationToken)
            => Task.FromResult(new PluginHandshakeResponse(
                "handshakeResponse",
                "bukit-plugin-v1",
                "req-1",
                Success: true,
                Plugin: new PluginIdentity(_handshakePluginId ?? plugin.Id, plugin.Id, plugin.Version, plugin.Platform)));

        public Task<PluginManifestResponse> GetManifestAsync(ResolvedPlugin plugin, CancellationToken cancellationToken)
            => Task.FromResult(new PluginManifestResponse(
                "manifestResponse",
                "bukit-plugin-v1",
                "req-2",
                Success: true,
                Commands: _commands,
                RequiredPermissions: _runtimePermissions));

        public Task<PluginInvokeResponse> InvokeAsync(
            ResolvedPlugin plugin,
            PluginInvokeRequest request,
            CancellationToken cancellationToken)
        {
            LastInvokeRequest = request;
            return Task.FromResult(_invokeResponse);
        }
    }
}
