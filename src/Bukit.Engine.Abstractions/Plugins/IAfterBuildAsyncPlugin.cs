namespace Bukit.Engine.Abstractions.Plugins;

public interface IAfterBuildAsyncPlugin
{
    Task AfterBuildAsync(BuildContext context, CancellationToken cancellationToken = default);
}
