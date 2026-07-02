namespace Bukit.PluginHost;

public sealed class PluginRequestIdFactory : IPluginRequestIdFactory
{
    public string Create() => Guid.NewGuid().ToString("N");
}
