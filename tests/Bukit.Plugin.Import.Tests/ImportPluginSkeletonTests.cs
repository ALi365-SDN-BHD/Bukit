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
    public void Invoker_ReturnsUnsupportedCommandDiagnostic()
    {
        var response = ImportPluginInvoker.InvokeUnsupportedCommand("req-2");

        Assert.False(response.Success);
        Assert.Equal(1, response.ExitCode);
        var diagnostic = Assert.Single(response.Diagnostics);
        Assert.Equal("plugin.import.unsupportedCommand", diagnostic.Code);
        Assert.Equal("error", diagnostic.Severity);
        Assert.Equal("Unsupported import command path. Supported commands: import seed, import html-demo.", diagnostic.Message);
    }
}
