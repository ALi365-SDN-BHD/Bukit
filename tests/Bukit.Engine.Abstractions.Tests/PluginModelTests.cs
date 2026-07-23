using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Abstractions.Tests;

public sealed class PluginModelTests
{
    [Fact]
    public void BuildContext_MissingTemplateResolver_PreservesConfigDiagnostic()
    {
        var context = new BuildContext
        {
            RootDir = "/test",
            OutputDir = "/test/dist",
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            RoutedDocuments = [],
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var exception = Assert.Throws<ConfigException>(
            () => context.ResolveTemplateKind("archive"));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains("archive", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginExecutionInfo_RecordsSuccess()
    {
        var info = new PluginExecutionInfo("my-plugin", "AfterBuild", 42, true, null);
        Assert.Equal("my-plugin", info.Name);
        Assert.True(info.Success);
        Assert.Equal(42, info.DurationMs);
    }

    [Fact]
    public void PluginExecutionInfo_Failure()
    {
        var info = new PluginExecutionInfo("bad", "AfterBuild", 0, false, "error msg");
        Assert.False(info.Success);
        Assert.Equal("error msg", info.Error);
    }

    [Fact]
    public void PluginOutputTrackingInfo_IdentifiesOutput()
    {
        var info = new PluginOutputTrackingInfo("p", "AfterBuild", "dist/x.css");
        Assert.Equal("p", info.Plugin);
        Assert.Equal("dist/x.css", info.Path);
    }
}
