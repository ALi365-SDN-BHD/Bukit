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
    public void Invoker_ReturnsNotImplementedDiagnostic()
    {
        var response = ImportPluginInvoker.InvokeNotImplemented("req-2");

        Assert.False(response.Success);
        Assert.Equal(1, response.ExitCode);
        var diagnostic = Assert.Single(response.Diagnostics);
        Assert.Equal("plugin.import.notImplemented", diagnostic.Code);
    }
}
