using Bukit.Content;
using Bukit.Routing;

namespace Bukit.Engine.Plugins;

public interface IDerivePagesPlugin
{
    IReadOnlyList<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)> DerivePages(BuildContext context);
}

