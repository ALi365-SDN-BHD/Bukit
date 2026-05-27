using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;

namespace Bukit.Engine.Abstractions.Plugins;

public interface IDerivePagesAsyncPlugin
{
    Task<IReadOnlyList<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)>> DerivePagesAsync(
        BuildContext context,
        CancellationToken cancellationToken = default);
}
