using Bukit.Content;
using Bukit.Routing;

namespace Bukit.Engine.Plugins;

public interface IDerivePagesAsyncPlugin
{
    Task<IReadOnlyList<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)>> DerivePagesAsync(
        BuildContext context,
        CancellationToken cancellationToken = default);
}
