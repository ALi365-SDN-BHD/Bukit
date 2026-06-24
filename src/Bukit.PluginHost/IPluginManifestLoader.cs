using Bukit.Plugin.Abstractions.Manifest;

namespace Bukit.PluginHost;

public interface IPluginManifestLoader
{
    Task<PluginManifest> LoadAsync(string pluginRoot, CancellationToken cancellationToken);
}
