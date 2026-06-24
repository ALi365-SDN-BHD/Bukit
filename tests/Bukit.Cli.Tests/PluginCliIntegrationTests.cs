using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Bukit.Cli.Commands;
using Bukit.Cli.Shared.Cli.Metadata;
using Bukit.Plugin.Echo;
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
    public async Task Main_EchoCommand_InvokesEchoPlugin()
    {
        using var cwd = new CurrentDirectoryScope(_tempDir);
        await InstallEchoPluginAsync(enabled: true);

        var result = await InvokeEntryPointAsync(["echo", "hello"]);

        Assert.Equal(0, result.ExitCode);
        using JsonDocument document = JsonDocument.Parse(result.StdOut.Trim());
        Assert.Equal("hello", document.RootElement.GetProperty("arguments")[0].GetString());
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

    private async Task InstallEchoPluginAsync(bool enabled, bool includeStaticCommand = false)
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

        File.WriteAllText(Path.Combine(pluginRoot, "plugin.yaml"),
            $$"""
            id: echo
            name: Bukit Echo Plugin
            version: 1.0.0
            protocol: bukit-plugin-v1
            kind: process
            distribution: external
            platforms:
              {{rid}}:
                entry: bin/{{rid}}/{{Path.GetFileName(executablePath)}}
                sha256: {{sha256}}
            {{commands}}
            """);
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
}
