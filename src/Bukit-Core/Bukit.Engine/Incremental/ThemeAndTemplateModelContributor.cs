namespace Bukit.Engine.Incremental;

internal sealed class ThemeAndTemplateModelContributor : IRenderDependencyContributor
{
    public string Name => "theme-and-template-model";

    public void Contribute(RenderDependencyContext context, RenderDependencyHashWriter writer)
    {
        var theme = context.Config.Theme;
        writer.AppendLabeledCanonicalValue("theme.params", theme.Params);

        if (theme.Shortcodes is { Count: > 0 })
        {
            foreach (var shortcode in theme.Shortcodes.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                writer.AppendLabeledCanonicalValue("theme.shortcode.key", shortcode.Key);
                writer.AppendLabeledCanonicalValue("theme.shortcode.value", shortcode.Value);
            }
        }

        if (theme.Components is { Count: > 0 })
        {
            foreach (var component in theme.Components.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                writer.AppendLabeledCanonicalValue("theme.component.key", component.Key);
                writer.AppendLabeledCanonicalValue("theme.component.template", component.Value.Template);
                if (component.Value.Props is { Count: > 0 })
                {
                    foreach (var property in component.Value.Props.OrderBy(x => x.Key, StringComparer.Ordinal))
                    {
                        writer.AppendLabeledCanonicalValue("theme.component.property.key", property.Key);
                        writer.AppendLabeledCanonicalValue("theme.component.property.value", property.Value);
                    }
                }
            }
        }

        writer.AppendLabeledCanonicalValue("theme.componentValidation", theme.ComponentValidation);
        writer.AppendLabeledCanonicalValue("build.listPageContentMode", context.Config.Build.ListPageContentMode);
    }
}
