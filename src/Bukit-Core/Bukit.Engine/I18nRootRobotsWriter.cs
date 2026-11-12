namespace Bukit.Engine;

internal sealed class I18nRootRobotsWriter : II18nRootProjectionWriter
{
    public IReadOnlyList<string> RepresentationKinds => ["robots"];

    internal static bool HasStaticRobots(Config.AppConfig config, string rootDir)
        => I18nOutputLayout.IsDefaultLanguageAtRoot(config) &&
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
            SitemapUrls(context, layout),
            context.Logger);
    }

    private static string[]? SitemapUrls(I18nRootProjectionWriterContext context, I18nOutputLayout layout)
    {
        var siteUrl = context.Config.Site.Url ?? string.Empty;
        var mode = context.Config.Site.SitemapMode?.Trim() ?? "split";
        if (string.Equals(mode, "split", StringComparison.OrdinalIgnoreCase))
        {
            return [.. context.Results.Select(result => SitemapGenerator.BuildAbsoluteUrl(
                siteUrl, result.BaseUrl, "/sitemap.xml"))];
        }

        // Index mode with a root language: its sitemap moves to <lang>/sitemap.xml and is listed next to the index.
        var rootLanguage = context.Results.Select(result => result.Language).FirstOrDefault(layout.IsRootLanguage);
        return string.Equals(mode, "index", StringComparison.OrdinalIgnoreCase) && rootLanguage is not null
            ?
            [
                SitemapGenerator.BuildAbsoluteUrl(siteUrl, context.RootBaseUrl, "/sitemap.xml"),
                SitemapGenerator.BuildAbsoluteUrl(siteUrl, context.RootBaseUrl, "/" + rootLanguage.Trim() + "/sitemap.xml")
            ]
            : null;
    }
}
