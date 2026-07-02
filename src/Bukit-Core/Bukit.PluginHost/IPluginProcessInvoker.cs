namespace Bukit.PluginHost;

public interface IPluginProcessInvoker
{
    Task<PluginProcessResult> InvokeAsync(
        PluginProcessRequest request,
        CancellationToken cancellationToken);
}
