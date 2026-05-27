using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
namespace Bukit.Plugins.ThrowingPlugin;

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
