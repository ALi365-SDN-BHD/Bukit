using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;

namespace Bukit.Engine.Abstractions.Plugins;

public interface IDerivePagesPlugin
{
    IReadOnlyList<RoutedContentDocument> DerivePages(BuildContext context);
}
