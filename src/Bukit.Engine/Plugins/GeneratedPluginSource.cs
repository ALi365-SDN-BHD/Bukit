#if !AOT
using System.Collections.Generic;

namespace Bukit.Engine.Plugins.Generated;

internal sealed class GeneratedPluginSource : IPluginSource
{
    public IEnumerable<IBukitPlugin> GetPlugins()
    {
        yield break;
    }
}
#endif
