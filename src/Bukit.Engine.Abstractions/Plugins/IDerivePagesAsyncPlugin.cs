using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;

namespace Bukit.Engine.Abstractions.Plugins;

public interface IDerivePagesAsyncPlugin
{
    Task<IReadOnlyList<RoutedContentDocument>> DerivePagesAsync(
        BuildContext context,
        CancellationToken cancellationToken = default);
}
