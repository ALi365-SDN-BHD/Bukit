using Bukit.Engine.Abstractions.Plugins.Protocol;

namespace Bukit.Plugins.SampleAfterBuildPlugin;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var plugin = new SampleAfterBuildPlugin();
        await plugin.RunAsync();
        return 0;
    }
}

internal sealed class SampleAfterBuildPlugin : ProcessPluginHost
{
    protected override string PluginName => "sample-after-build";
    protected override string PluginVersion => "0.2.0";
    protected override IReadOnlyList<string> SupportedHooks => new[] { "after-build" };
}
