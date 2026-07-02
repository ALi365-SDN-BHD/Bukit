using Bukit.Plugin.Abstractions.Protocol;

namespace Bukit.PluginHost;

public interface IPluginProtocolClient
{
    Task<PluginHandshakeResponse> HandshakeAsync(
        ResolvedPlugin plugin,
        CancellationToken cancellationToken);

    Task<PluginManifestResponse> GetManifestAsync(
        ResolvedPlugin plugin,
        CancellationToken cancellationToken);

    Task<PluginInvokeResponse> InvokeAsync(
        ResolvedPlugin plugin,
        PluginInvokeRequest request,
        CancellationToken cancellationToken);
}
