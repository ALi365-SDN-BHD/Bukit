namespace Bukit.Engine.Plugins;

public interface IAfterBuildAsyncPlugin
{
    Task AfterBuildAsync(BuildContext context, CancellationToken cancellationToken = default);
}
