using Bukit.Plugin.Abstractions.Config;

namespace Bukit.PluginHost;

public interface IPluginConfigLoader
{
    Task<PluginHostConfig> LoadAsync(string projectRoot, CancellationToken cancellationToken);
}
