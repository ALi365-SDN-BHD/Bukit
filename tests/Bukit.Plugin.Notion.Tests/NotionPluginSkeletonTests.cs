using Bukit.Plugin.Notion;
using Xunit;

namespace Bukit.Plugin.Notion.Tests;

public sealed class NotionPluginSkeletonTests
{
    [Fact]
    public void HandshakeProvider_ReturnsV1RcVersion()
    {
        var handshake = NotionPluginManifestProvider.CreateHandshakeResponse("req-handshake");

        Assert.Equal("1.0.0-rc.1", handshake.Plugin?.Version);
    }

    [Fact]
    public void ManifestProvider_ReturnsNotionCommand()
    {
        var manifest = NotionPluginManifestProvider.CreateManifestResponse("req-1");

        Assert.True(manifest.Success);
        Assert.Equal("notion", Assert.Single(manifest.Commands).Name);
        Assert.Contains("cli-command", manifest.Capabilities);
    }

    [Fact]
    public void Invoker_ReturnsUnsupportedCommandDiagnostic()
    {
        var response = NotionPluginInvoker.InvokeUnsupportedCommand("req-2");

        Assert.False(response.Success);
        Assert.Equal(1, response.ExitCode);
        var diagnostic = Assert.Single(response.Diagnostics);
        Assert.Equal("plugin.notion.unsupportedCommand", diagnostic.Code);
        Assert.Equal("error", diagnostic.Severity);
        Assert.Equal("Unsupported notion command path. Supported commands in this phase: notion validate-seed, notion validate-database-map, notion schema validate, notion push.", diagnostic.Message);
    }
}
