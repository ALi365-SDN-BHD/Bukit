using Bukit.Engine.Plugins;

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
