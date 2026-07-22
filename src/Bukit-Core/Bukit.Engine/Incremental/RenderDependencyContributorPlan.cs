namespace Bukit.Engine.Incremental;

internal interface IRenderDependencyContributor
{
    string Name { get; }

    void Contribute(RenderDependencyContext context, RenderDependencyHashWriter writer);
}

internal static class RenderDependencyContributorPlan
{
    internal static IReadOnlyList<IRenderDependencyContributor> Contributors { get; } =
    [
        new SiteIdentityAndModesContributor(),
        new AnalyticsContributor(),
        new SeoContributor(),
        new ThemeAndTemplateModelContributor(),
        new CollectionsAndFieldScopesContributor(),
        new TaxonomyContributor(),
        new RouteMetadataConfigurationContributor(),
        new NonAnalyticsPluginEnablementContributor(),
        new SiteModelDataContributor()
    ];
}
