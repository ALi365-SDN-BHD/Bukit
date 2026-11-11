namespace Bukit.Engine.Incremental;

internal sealed class NonAnalyticsPluginEnablementContributor : IRenderDependencyContributor
{
    public string Name => "non-analytics-plugin-enablement";

    public void Contribute(RenderDependencyContext context, RenderDependencyHashWriter writer)
    {
        if (context.Config.Site.Plugins is not { Count: > 0 } plugins)
        {
            return;
        }

        // Plugin names and option names are matched ignoring case, so they are hashed in lower case.
        foreach (var plugin in plugins
            .Select(x => (Name: x.Key.ToLowerInvariant(), x.Value))
            .OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            if (string.Equals(plugin.Name, "analytics", StringComparison.Ordinal))
            {
                continue;
            }

            writer.AppendLabeledCanonicalValue("plugin.name", plugin.Name);
            writer.AppendLabeledCanonicalValue("plugin.enabled", plugin.Value.Enabled);
            // Options can change the rendered HTML; dictionaries are hashed in ordinal key order.
            if (plugin.Value.Enabled && plugin.Value.Options is { Count: > 0 } options)
            {
                writer.AppendLabeledCanonicalValue("plugin.options", LowerCaseKeys(options));
            }
        }
    }

    private static IReadOnlyDictionary<string, object> LowerCaseKeys(IReadOnlyDictionary<string, object> options)
    {
        var lowered = new Dictionary<string, object>(options.Count, StringComparer.Ordinal);
        foreach (var (key, value) in options)
        {
            if (!lowered.TryAdd(key.ToLowerInvariant(), value))
            {
                // Two names differ only in case in a case-sensitive map: keep the names as given.
                return options;
            }
        }

        return lowered;
    }
}
