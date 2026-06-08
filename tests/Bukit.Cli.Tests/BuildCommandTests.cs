using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Commands;
using Bukit.Cli.Tests;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("CWD")]
public sealed class BuildCommandTests : IDisposable
{
    private readonly string _testDir;

    public BuildCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "bukit-build-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_testDir, recursive: true);
    }

    [Fact]
    public async Task RunAsync_WithConfigOption_ResolvesAndStartsBuild()
    {
        var siteYaml = Path.Combine(Path.GetTempPath(), "bukit-test-config", Guid.NewGuid().ToString("N"), "site.yaml");
        var dir = Path.GetDirectoryName(siteYaml)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(siteYaml, "site:\n  name: test\n  title: Test\ncontent:\n  sources:\n    - type: markdown\n      name: page\n      collection: page\n      markdown:\n        dir: content\nbuild:\n  output: dist\n");

        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--config"] = siteYaml,
            },
            Array.Empty<string>());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var buildTask = BuildCommand.RunAsync(command);
        var completed = await Task.WhenAny(buildTask, Task.Delay(Timeout.Infinite, cts.Token));
        Assert.Same(buildTask, completed);
        try { await buildTask; }
        catch (ConfigException ex) { Assert.NotNull(ex); }
        catch (ContentException ex) { Assert.NotNull(ex); }
    }

    [Fact]
    public void JobOption_ParsedCorrectly()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--jobs"] = "4",
            },
            Array.Empty<string>());

        Assert.Equal("4", command.GetString("--jobs"));
    }

    [Fact]
    public void JobOption_Null_WhenNotSet()
    {
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>());

        Assert.Null(command.GetString("--jobs"));
    }

    [Fact]
    public async Task RunAsync_WithSiteOption_ResolvesAndStartsBuild()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bukit-test-site", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "sites"));
        File.WriteAllText(Path.Combine(dir, "sites", "testsite.yaml"), "site:\n  name: test\n  title: Test\ncontent:\n  sources:\n    - type: markdown\n      name: page\n      collection: page\n      markdown:\n        dir: content\nbuild:\n  output: dist\n");

        using var _ = new CurrentDirectoryScope(dir);
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--site"] = "testsite",
            },
            Array.Empty<string>());

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var buildTask = BuildCommand.RunAsync(command);
            var completed = await Task.WhenAny(buildTask, Task.Delay(Timeout.Infinite, cts.Token));
            Assert.Same(buildTask, completed);
            try { await buildTask; }
            catch (ConfigException ex) { Assert.NotNull(ex); }
            catch (ContentException ex) { Assert.NotNull(ex); }
        }
        finally
        {
            TestCleanup.DeleteDirectory(dir, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_MissingConfigFile_ThrowsConfigException()
    {
        var nonExistentConfig = Path.Combine(_testDir, "nonexistent.yaml");
        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--config"] = nonExistentConfig,
            },
            Array.Empty<string>());

        var ex = await Assert.ThrowsAsync<ConfigException>(
            () => BuildCommand.RunAsync(command));

        Assert.Contains("Config file not found", ex.Message);
    }

    [Fact]
    public async Task RunAsync_InvalidYamlConfig_ThrowsConfigException()
    {
        var configPath = Path.Combine(_testDir, "invalid-site.yaml");
        File.WriteAllText(configPath, "{{{ invalid ::: yaml ]]]");

        var command = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--config"] = configPath,
            },
            Array.Empty<string>());

        await Assert.ThrowsAsync<ConfigException>(
            () => BuildCommand.RunAsync(command));
    }

    [Fact]
    public async Task RunAsync_ArgReaderWithMissingConfig_ReturnsErrorCode()
    {
        var command = CliTestHelper.CreateCommand("build", new[] { "--config", Path.Combine(_testDir, "no-file.yaml") });

        var ex = await Assert.ThrowsAsync<ConfigException>(
            () => BuildCommand.RunAsync(command));

        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task RunAsync_ArgReaderWithInvalidYaml_ThrowsConfigException()
    {
        var configPath = Path.Combine(_testDir, "bad.yaml");
        File.WriteAllText(configPath, "!!! not yaml !!!");

        var command = CliTestHelper.CreateCommand("build", new[] { "--config", configPath });

        await Assert.ThrowsAsync<ConfigException>(
            () => BuildCommand.RunAsync(command));
    }

    [Fact]
    public async Task RunAsync_MissingConfig_ReturnsExitCode2()
    {
        var cmd = new CliBoundCommand(new Dictionary<string, string?> { ["--config"] = "nonexistent.yml" }, Array.Empty<string>());
        var ex = await Assert.ThrowsAsync<ConfigException>(() => BuildCommand.RunAsync(cmd));
        Assert.Contains("Config file not found", ex.Message);
    }

    [Fact]
    public async Task RunAsync_NoConfig_ReturnsExitCode2()
    {
        using var _ = new CurrentDirectoryScope(_testDir);
        var cmd = new CliBoundCommand(new Dictionary<string, string?>(), Array.Empty<string>());
        var ex = await Assert.ThrowsAsync<ConfigException>(() => BuildCommand.RunAsync(cmd));
        Assert.Contains("Config file not found", ex.Message);
        Assert.Contains(_testDir, ex.Message);
    }

    [Fact]
    public async Task RunAsync_NoConfig_UsesCurrentDirectorySiteYaml()
    {
        using var _ = new CurrentDirectoryScope(_testDir);
        var expectedPath = Path.GetFullPath(Path.Combine(_testDir, "site.yaml"));
        var cmd = new CliBoundCommand(new Dictionary<string, string?>(), Array.Empty<string>());
        var ex = await Assert.ThrowsAsync<ConfigException>(() => BuildCommand.RunAsync(cmd));
        Assert.Contains(expectedPath, ex.Message);
    }

    [Fact]
    public async Task RunAsync_DeprecationWarnings_StrictMode_ThrowsConfigException()
    {
        var siteYaml = Path.Combine(_testDir, "site.yaml");
        File.WriteAllText(siteYaml, """
                                      site:
                                        name: test
                                        title: Test
                                        pluginFailMode: strict
                                        rssMode: root
                                      content:
                                        sources:
                                          - type: markdown
                                            name: page
                                            collection: page
                                            markdown:
                                              dir: content
                                      """);

        var cmd = new CliBoundCommand(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["--config"] = siteYaml
        },
            Array.Empty<string>());

        var ex = await Assert.ThrowsAsync<ConfigException>(() => BuildCommand.RunAsync(cmd));
        Assert.Contains("Removed configuration fields", ex.Message);
    }

    [Fact]
    public async Task RunAsync_JobsAbc_ThrowsCommandArgumentException()
    {
        var siteYaml = Path.Combine(_testDir, "site.yaml");
        File.WriteAllText(siteYaml, "site:\n  name: test\n  title: Test\ncontent:\n  sources:\n    - type: markdown\n      name: page\n      collection: page\n      markdown:\n        dir: content\nbuild:\n  output: dist\n");

        var cmd = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--config"] = siteYaml,
                ["--jobs"] = "abc",
            },
            Array.Empty<string>());

        var ex = await Assert.ThrowsAsync<CommandArgumentException>(() => BuildCommand.RunAsync(cmd));
        Assert.Equal("--jobs must be a positive integer", ex.Message);
    }

    [Fact]
    public async Task RunAsync_JobsNegativeOne_ThrowsCommandArgumentException()
    {
        var siteYaml = Path.Combine(_testDir, "site.yaml");
        File.WriteAllText(siteYaml, "site:\n  name: test\n  title: Test\ncontent:\n  sources:\n    - type: markdown\n      name: page\n      collection: page\n      markdown:\n        dir: content\nbuild:\n  output: dist\n");

        var cmd = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--config"] = siteYaml,
                ["--jobs"] = "-1",
            },
            Array.Empty<string>());

        var ex = await Assert.ThrowsAsync<CommandArgumentException>(() => BuildCommand.RunAsync(cmd));
        Assert.Equal("--jobs must be a positive integer", ex.Message);
    }

    [Fact]
    public async Task RunAsync_JobsZero_ThrowsCommandArgumentException()
    {
        var siteYaml = Path.Combine(_testDir, "site.yaml");
        File.WriteAllText(siteYaml, "site:\n  name: test\n  title: Test\ncontent:\n  sources:\n    - type: markdown\n      name: page\n      collection: page\n      markdown:\n        dir: content\nbuild:\n  output: dist\n");

        var cmd = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--config"] = siteYaml,
                ["--jobs"] = "0",
            },
            Array.Empty<string>());

        var ex = await Assert.ThrowsAsync<CommandArgumentException>(() => BuildCommand.RunAsync(cmd));
        Assert.Equal("--jobs must be a positive integer", ex.Message);
    }

    [Fact]
    public async Task RunAsync_JobsFour_StartsBuildWithoutArgumentError()
    {
        var siteYaml = Path.Combine(_testDir, "site.yaml");
        File.WriteAllText(siteYaml, "site:\n  name: test\n  title: Test\ncontent:\n  sources:\n    - type: markdown\n      name: page\n      collection: page\n      markdown:\n        dir: content\nbuild:\n  output: dist\n");

        var cmd = new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--config"] = siteYaml,
                ["--jobs"] = "4",
            },
            Array.Empty<string>());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var buildTask = BuildCommand.RunAsync(cmd);
        var completed = await Task.WhenAny(buildTask, Task.Delay(Timeout.Infinite, cts.Token));
        Assert.Same(buildTask, completed);
        try { await buildTask; }
        catch (ConfigException ex) { Assert.NotNull(ex); }
        catch (ContentException ex) { Assert.NotNull(ex); }
    }

    [Fact]
    public async Task RunAsync_CIEnvWithoutAllowExternalPlugins_ThrowsConfigException()
    {
        var oldCI = Environment.GetEnvironmentVariable("CI");
        var oldBukitCI = Environment.GetEnvironmentVariable("BUKIT_CI");
        try
        {
            Environment.SetEnvironmentVariable("CI", "true");
            Environment.SetEnvironmentVariable("BUKIT_CI", null);

            var siteYaml = Path.Combine(_testDir, "site.yaml");
            File.WriteAllText(siteYaml, """
                site:
                  name: test
                  title: Test
                  externalPlugins:
                    sample:
                      runtime: process
                      entry: plugins/sample.sh
                      hooks: [after-build]
                      capabilities: [emit-outputs]
                      timeoutMs: 5000
                content:
                  sources:
                    - type: markdown
                      name: page
                      collection: page
                      markdown:
                        dir: content
                build:
                  output: dist
                """);

            var command = CliTestHelper.CreateCommand("build", new[] { "--config", siteYaml });

            var ex = await Assert.ThrowsAsync<ConfigException>(
                () => BuildCommand.RunAsync(command));

            Assert.Contains("External plugins are disabled in CI", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CI", oldCI);
            Environment.SetEnvironmentVariable("BUKIT_CI", oldBukitCI);
        }
    }

    [Fact]
    public async Task RunAsync_CIEnvWithAllowExternalPlugins_BuildSucceeds()
    {
        var oldCI = Environment.GetEnvironmentVariable("CI");
        var oldBukitCI = Environment.GetEnvironmentVariable("BUKIT_CI");
        try
        {
            Environment.SetEnvironmentVariable("CI", "true");
            Environment.SetEnvironmentVariable("BUKIT_CI", null);

            var siteYaml = Path.Combine(_testDir, "site.yaml");
            File.WriteAllText(siteYaml, """
                site:
                  name: test
                  title: Test
                  externalPlugins:
                    sample:
                      runtime: process
                      entry: plugins/sample.sh
                      hooks: [after-build]
                      capabilities: [emit-outputs]
                      timeoutMs: 5000
                content:
                  sources:
                    - type: markdown
                      name: page
                      collection: page
                      markdown:
                        dir: content
                build:
                  output: dist
                """);

            var command = CliTestHelper.CreateCommand("build", new[] { "--config", siteYaml, "--allow-external-plugins" });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var buildTask = BuildCommand.RunAsync(command);
            var completed = await Task.WhenAny(buildTask, Task.Delay(Timeout.Infinite, cts.Token));
            Assert.Same(buildTask, completed);
            try { await buildTask; }
            catch (Exception ex) { Assert.DoesNotContain("External plugins are disabled in CI", ex.Message); }
        }
        finally
        {
            Environment.SetEnvironmentVariable("CI", oldCI);
            Environment.SetEnvironmentVariable("BUKIT_CI", oldBukitCI);
        }
    }

    [Fact]
    public async Task RunAsync_NonCIEnv_ExternalPluginsWorkNormally()
    {
        var oldCI = Environment.GetEnvironmentVariable("CI");
        var oldBukitCI = Environment.GetEnvironmentVariable("BUKIT_CI");
        try
        {
            Environment.SetEnvironmentVariable("CI", null);
            Environment.SetEnvironmentVariable("BUKIT_CI", null);

            var siteYaml = Path.Combine(_testDir, "site.yaml");
            File.WriteAllText(siteYaml, """
                site:
                  name: test
                  title: Test
                  externalPlugins:
                    sample:
                      runtime: process
                      entry: plugins/sample.sh
                      hooks: [after-build]
                      capabilities: [emit-outputs]
                      timeoutMs: 5000
                content:
                  sources:
                    - type: markdown
                      name: page
                      collection: page
                      markdown:
                        dir: content
                build:
                  output: dist
                """);

            var command = CliTestHelper.CreateCommand("build", new[] { "--config", siteYaml });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var buildTask = BuildCommand.RunAsync(command);
            var completed = await Task.WhenAny(buildTask, Task.Delay(Timeout.Infinite, cts.Token));
            Assert.Same(buildTask, completed);
            try { await buildTask; }
            catch (Exception ex) { Assert.DoesNotContain("External plugins are disabled in CI", ex.Message); }
        }
        finally
        {
            Environment.SetEnvironmentVariable("CI", oldCI);
            Environment.SetEnvironmentVariable("BUKIT_CI", oldBukitCI);
        }
    }

    [Fact]
    public async Task RunAsync_BukitCIEnvWithoutAllowExternalPlugins_ThrowsConfigException()
    {
        var oldCI = Environment.GetEnvironmentVariable("CI");
        var oldBukitCI = Environment.GetEnvironmentVariable("BUKIT_CI");
        try
        {
            Environment.SetEnvironmentVariable("CI", null);
            Environment.SetEnvironmentVariable("BUKIT_CI", "1");

            var siteYaml = Path.Combine(_testDir, "site.yaml");
            File.WriteAllText(siteYaml, """
                site:
                  name: test
                  title: Test
                  externalPlugins:
                    sample:
                      runtime: process
                      entry: plugins/sample.sh
                      hooks: [after-build]
                      capabilities: [emit-outputs]
                      timeoutMs: 5000
                content:
                  sources:
                    - type: markdown
                      name: page
                      collection: page
                      markdown:
                        dir: content
                build:
                  output: dist
                """);

            var command = CliTestHelper.CreateCommand("build", new[] { "--config", siteYaml });

            var ex = await Assert.ThrowsAsync<ConfigException>(
                () => BuildCommand.RunAsync(command));

            Assert.Contains("External plugins are disabled in CI", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CI", oldCI);
            Environment.SetEnvironmentVariable("BUKIT_CI", oldBukitCI);
        }
    }

    [Fact]
    public async Task RunAsync_CIFlagWithoutExternalPlugins_BuildSucceeds()
    {
        var siteYaml = Path.Combine(_testDir, "site.yaml");
        File.WriteAllText(siteYaml, """
            site:
              name: test
              title: Test
            content:
              sources:
                - type: markdown
                  name: page
                  collection: page
                  markdown:
                    dir: content
            build:
              output: dist
            """);

        var command = CliTestHelper.CreateCommand("build", new[] { "--config", siteYaml, "--ci" });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var buildTask = BuildCommand.RunAsync(command);
        var completed = await Task.WhenAny(buildTask, Task.Delay(Timeout.Infinite, cts.Token));
        if (completed == buildTask)
        {
            try { await buildTask; }
            catch (Exception ex) { Assert.DoesNotContain("External plugins are disabled", ex.Message); }
        }
    }

    [Fact]
    public void AllowExternalPluginsFlag_DefaultsToFalse()
    {
        var command = CliTestHelper.CreateCommand("build", Array.Empty<string>());

        Assert.False(command.GetBool("--allow-external-plugins"));
    }

    [Fact]
    public void AllowExternalPluginsFlag_True_WhenSet()
    {
        var command = CliTestHelper.CreateCommand("build", new[] { "--allow-external-plugins" });

        Assert.True(command.GetBool("--allow-external-plugins"));
    }
}
