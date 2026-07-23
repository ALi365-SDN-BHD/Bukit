using Bukit.Config;

namespace Bukit.Engine.Plugins;

internal sealed class PluginExecutionPolicy
{
    private readonly IReadOnlyDictionary<string, bool>? _pluginEnablement;

    private PluginExecutionPolicy(
        bool warnOnPluginFailure,
        string deriveConflictPolicy,
        IReadOnlyDictionary<string, bool>? pluginEnablement)
    {
        WarnOnPluginFailure = warnOnPluginFailure;
        DeriveConflictPolicy = deriveConflictPolicy;
        _pluginEnablement = pluginEnablement;
    }

    internal bool WarnOnPluginFailure { get; }

    internal string DeriveConflictPolicy { get; }

    internal static PluginExecutionPolicy From(SiteConfig site)
    {
        Dictionary<string, bool>? pluginEnablement = null;
        if (site.Plugins is not null)
        {
            pluginEnablement = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, config) in site.Plugins)
            {
                pluginEnablement[name] = config.Enabled;
            }
        }

        return new PluginExecutionPolicy(
            string.Equals(
                site.PluginFailMode,
                "warn",
                StringComparison.OrdinalIgnoreCase),
            (site.DeriveConflictPolicy ?? "fail").Trim().ToLowerInvariant(),
            pluginEnablement);
    }

    internal bool IsPluginEnabled(string? name)
    {
        if (_pluginEnablement is null || string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        return !_pluginEnablement.TryGetValue(name, out var enabled) || enabled;
    }
}
