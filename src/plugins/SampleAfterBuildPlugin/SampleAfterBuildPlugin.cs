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

// DESKTOP-REMOVED: Original inline [BukitPlugin] implementation.
#if false
using Bukit.Engine.Abstractions.Plugins;

namespace Bukit.Plugins.SampleAfterBuildPlugin;

[BukitPlugin]
public sealed class SampleAfterBuildPlugin : IBukitPlugin, IAfterBuildPlugin
{
    public string Name => "sample-after-build";

    public string Version => "0.1.0";

    public void AfterBuild(BuildContext context)
    {
    }
}
#endif
