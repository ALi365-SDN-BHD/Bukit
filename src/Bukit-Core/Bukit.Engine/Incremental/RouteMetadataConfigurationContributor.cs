namespace Bukit.Engine.Incremental;

internal sealed class RouteMetadataConfigurationContributor : IRenderDependencyContributor
{
    public string Name => "route-metadata-configuration";

    public void Contribute(RenderDependencyContext context, RenderDependencyHashWriter writer)
    {
        var config = context.Config.Content.RouteMetadata;
        if (config is null)
        {
            return;
        }

        writer.AppendFramedValue("source", config.Source);
        writer.AppendFramedValue("routeField", config.RouteField);
        writer.AppendFramedValue("titleField", config.TitleField);
        writer.AppendFramedValue("summaryField", config.SummaryField);
        writer.AppendFramedValue("seoTitleField", config.SeoTitleField);
        writer.AppendFramedValue("seoDescriptionField", config.SeoDescriptionField);
        foreach (var route in config.RequiredRoutes.OrderBy(x => x, StringComparer.Ordinal))
        {
            writer.AppendFramedValue("requiredRoute", route);
        }
    }
}
