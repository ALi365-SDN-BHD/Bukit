namespace Bukit.Engine.Incremental;

internal interface IRenderDependencyContributor
{
    string Name { get; }

    void Contribute(RenderDependencyContext context, RenderDependencyHashWriter writer);
}

/// <summary>
/// Contributors to the base render hash. Shared data collections are hashed separately
/// (<see cref="SiteModelDataContributor"/>).
/// </summary>
internal static class RenderDependencyContributorPlan
{
    internal static IReadOnlyList<IRenderDependencyContributor> Contributors { get; } =
    [
        new SiteIdentityAndModesContributor(),
        new AnalyticsContributor(),
        new SeoContributor(),
        new ThemeAndTemplateModelContributor(),
        new ImageProcessingContributor(),
        new CollectionsAndFieldScopesContributor(),
        new TaxonomyContributor(),
        new RouteMetadataConfigurationContributor(),
        new NonAnalyticsPluginEnablementContributor()
    ];
}
