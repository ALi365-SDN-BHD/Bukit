using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Runtime;
using Bukit.Plugin.Abstractions.Security;
using Bukit.Plugin.Import;
using Xunit;

namespace Bukit.Plugin.Import.Tests;

public sealed class ImportPluginSkeletonTests
{
    [Fact]
    public void ManifestProvider_ReturnsImportCommand()
    {
        var manifest = ImportPluginManifestProvider.CreateManifestResponse("req-1");

        Assert.True(manifest.Success);
        Assert.Equal("import", Assert.Single(manifest.Commands).Name);
        Assert.Contains("cli-command", manifest.Capabilities);
    }

    [Fact]
    public async Task Invoker_UnsupportedCommandReturnsDiagnostic()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-plugin-import-skeleton-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var response = await ImportPluginInvoker.InvokeAsync(new PluginInvokeRequest(
                Type: "invoke",
                Protocol: "bukit-plugin-v1",
                RequestId: "req-2",
                Host: new PluginHostInfo("Bukit", "1.0.0", "test"),
                Command: new PluginInvokeCommand("mystery", Path: ["import", "mystery"]),
                Context: new PluginInvokeContext(root, root),
                Permissions: new PluginPermissionSet()));

            Assert.False(response.Success);
            Assert.Equal(2, response.ExitCode);
            var diagnostic = Assert.Single(response.Diagnostics);
            Assert.Equal("plugin.import.unsupportedCommand", diagnostic.Code);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
