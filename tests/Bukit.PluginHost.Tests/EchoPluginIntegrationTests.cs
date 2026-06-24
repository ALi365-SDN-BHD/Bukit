using System.Security.Cryptography;
using System.Text.Json;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Runtime;
using Bukit.Plugin.Abstractions.Security;
using Bukit.Plugin.Echo;
using Bukit.PluginHost;
using Xunit;

namespace Bukit.PluginHost.Tests;

public sealed class EchoPluginIntegrationTests
{
    [Fact]
    public async Task EchoPlugin_LoadsAndRunsThroughProtocolClient()
    {
        using var directory = TestDirectory.Create();
        var resolver = new PluginPlatformResolver();
        string rid = resolver.GetCurrentRid();
        string pluginRoot = System.IO.Path.Combine(directory.Path, "plugins/echo");
        string binRoot = System.IO.Path.Combine(pluginRoot, "bin", rid);
        Directory.CreateDirectory(binRoot);

        string executablePath = CopyEchoPlugin(binRoot);
        string sha256 = await Sha256Async(executablePath);
        directory.Write("plugins/echo/plugin.yaml",
            $$"""
            id: echo
            name: Bukit Echo Plugin
            version: 1.0.0
            protocol: bukit-plugin-v1
            kind: process
            distribution: external
            platforms:
              {{rid}}:
                entry: bin/{{rid}}/{{System.IO.Path.GetFileName(executablePath)}}
                sha256: {{sha256}}
            """);

        var manifest = await new PluginManifestLoader().LoadAsync(pluginRoot, CancellationToken.None);
        Assert.Equal("echo", manifest.Id);

        var hash = await new PluginHashVerifier().VerifySha256Async(executablePath, sha256, CancellationToken.None);
        Assert.True(hash.Success);

        var client = new PluginProtocolClient(new PluginProcessInvoker(new SystemProcessRunner()), new PluginRequestIdFactory());
        var plugin = new ResolvedPlugin(
            Id: "echo",
            Version: "1.0.0",
            Platform: rid,
            ExecutablePath: executablePath,
            WorkingDirectory: pluginRoot,
            Host: new PluginHostInfo("Bukit", "1.0.0", rid));

        PluginHandshakeResponse handshake = await client.HandshakeAsync(plugin, CancellationToken.None);
        Assert.Equal("Bukit Echo Plugin", handshake.Plugin?.Name);
        Assert.Contains("cli-command", handshake.Plugin?.Capabilities ?? []);

        PluginManifestResponse runtimeManifest = await client.GetManifestAsync(plugin, CancellationToken.None);
        Assert.Equal("echo", Assert.Single(runtimeManifest.Commands).Name);

        PluginInvokeResponse invoke = await client.InvokeAsync(
            plugin,
            new PluginInvokeRequest(
                Type: "",
                Protocol: "",
                RequestId: "",
                Host: new PluginHostInfo("", "", ""),
                Command: new PluginInvokeCommand("echo", Arguments: ["hello"]),
                Context: new PluginInvokeContext(directory.Path, directory.Path),
                Permissions: new PluginPermissionSet()),
            CancellationToken.None);

        string message = Assert.Single(invoke.Messages).Message;
        using JsonDocument document = JsonDocument.Parse(message);
        Assert.Equal("hello", document.RootElement.GetProperty("arguments")[0].GetString());
        Assert.Equal(directory.Path, document.RootElement.GetProperty("context").GetProperty("rootDir").GetString());
    }

    private static string CopyEchoPlugin(string destinationDirectory)
    {
        string echoAssemblyPath = typeof(EchoPluginMarker).Assembly.Location;
        string echoOutputDirectory = System.IO.Path.GetDirectoryName(echoAssemblyPath)!;
        string executableName = OperatingSystem.IsWindows() ? "bukit-plugin-echo.exe" : "bukit-plugin-echo";

        foreach (string file in Directory.EnumerateFiles(echoOutputDirectory))
        {
            string target = System.IO.Path.Combine(destinationDirectory, System.IO.Path.GetFileName(file));
            File.Copy(file, target, overwrite: true);
        }

        string executablePath = System.IO.Path.Combine(destinationDirectory, executableName);
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
}
