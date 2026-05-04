using Bukit.Content;
using Bukit.Engine.Plugins;
using Bukit.Routing;

namespace Bukit.Plugins.ThrowingPlugin;

/// <summary>
/// Test-only plugin that throws to verify PluginRunner fail-mode behavior.
/// </summary>
public sealed class ThrowingPlugin : IBukitPlugin, IAfterBuildPlugin, IDerivePagesPlugin
{
    public string Name => "throwing-test";
    public string Version => "0.1.0";

    public void AfterBuild(BuildContext context)
    {
        throw new InvalidOperationException("throwing-test plugin intentionally fails");
    }

    public IReadOnlyList<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)> DerivePages(BuildContext context)
    {
        throw new InvalidOperationException("throwing-test DerivePages intentionally fails");
    }
}
