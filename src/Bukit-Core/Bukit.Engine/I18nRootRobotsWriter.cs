namespace Bukit.Engine;

internal sealed class I18nRootRobotsWriter : II18nRootProjectionWriter
{
    public IReadOnlyList<string> RepresentationKinds => ["robots"];

    internal static bool HasStaticRobots(Config.AppConfig config, string rootDir)
        => I18nOutputLayout.IsRootLanguageOutput(config) &&
           File.Exists(Path.Combine(rootDir, config.Theme.Static, "robots.txt"));

    public void Write(I18nRootProjectionWriterContext context, PublishRepresentation representation)
    {
        _ = representation;
        var layout = I18nOutputLayout.Create(
            context.Config.Site,
            I18nOutputMerger.GetLanguages(context.Config.Site),
            context.RootBaseUrl);
        var seoIndex = I18nMergedVariantState.BuildSeoIndex(context.Results, layout);
        RobotsTxtWriter.WriteIfRequested(
            context.Config,
            context.OutputDir,
            context.RootBaseUrl,
            seoIndex,
            string.Equals(context.Config.Site.SitemapMode?.Trim() ?? "split", "split", StringComparison.OrdinalIgnoreCase)
                ? context.Results.Select(result => SitemapGenerator.BuildAbsoluteUrl(
                    context.Config.Site.Url ?? string.Empty, result.BaseUrl, "/sitemap.xml")).ToArray()
                : null,
            context.Logger);
    }
}
