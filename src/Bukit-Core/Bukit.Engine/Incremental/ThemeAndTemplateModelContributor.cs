namespace Bukit.Engine.Incremental;

internal sealed class ThemeAndTemplateModelContributor : IRenderDependencyContributor
{
    public string Name => "theme-and-template-model";

    public void Contribute(RenderDependencyContext context, RenderDependencyHashWriter writer)
    {
        var theme = context.Config.Theme;
        writer.AppendDictionary(theme.Params);

        if (theme.Shortcodes is { Count: > 0 })
        {
            foreach (var shortcode in theme.Shortcodes.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                writer.AppendNewline();
                writer.AppendUtf8(shortcode.Key);
                writer.AppendNewline();
                writer.AppendUtf8(shortcode.Value);
            }
        }

        if (theme.Components is { Count: > 0 })
        {
            foreach (var component in theme.Components.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                writer.AppendNewline();
                writer.AppendUtf8(component.Key);
                writer.AppendNewline();
                writer.AppendUtf8(component.Value.Template);
                if (component.Value.Props is { Count: > 0 })
                {
                    foreach (var property in component.Value.Props.OrderBy(x => x.Key, StringComparer.Ordinal))
                    {
                        writer.AppendNewline();
                        writer.AppendUtf8(property.Key);
                        writer.AppendNewline();
                        writer.AppendUtf8(property.Value);
                    }
                }
            }
        }

        writer.AppendUtf8(theme.ComponentValidation);
        writer.AppendNewline();
        writer.AppendUtf8(context.Config.Build.ListPageContentMode);
        writer.AppendNewline();
    }
}
