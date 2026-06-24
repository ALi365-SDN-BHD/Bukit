using Bukit.Plugin.Abstractions.Runtime;
using Bukit.Plugin.Abstractions.Security;

namespace Bukit.Plugin.Abstractions.Protocol;

public sealed record PluginInvokeRequest(
    string Type,
    string Protocol,
    string RequestId,
    PluginHostInfo Host,
    PluginInvokeCommand Command,
    PluginInvokeContext Context,
    PluginPermissionSet Permissions);
