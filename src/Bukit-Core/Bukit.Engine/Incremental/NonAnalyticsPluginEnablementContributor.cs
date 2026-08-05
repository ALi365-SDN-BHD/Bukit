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

        foreach (var plugin in plugins.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (string.Equals(plugin.Key, "analytics", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            writer.AppendLabeledCanonicalValue("plugin.name", plugin.Key);
            writer.AppendLabeledCanonicalValue("plugin.enabled", plugin.Value.Enabled);
        }
    }
}
