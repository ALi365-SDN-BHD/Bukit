namespace Bukit.Engine;

internal sealed class I18nRootRobotsWriter : II18nRootProjectionWriter
{
    public IReadOnlyList<string> RepresentationKinds => ["robots"];

    /// <summary>
    /// Whether the root default language copies a static <c>robots.txt</c> to the output root.
    /// It reads the language's planned static outputs, so site, named-theme and parent-theme
    /// static directories are all covered exactly as the copy step resolves them.
    /// </summary>
    internal static bool HasStaticRobots(Config.AppConfig config, IReadOnlyList<BuildVariantResult> variants)
    {
        if (!I18nOutputLayout.IsDefaultLanguageAtRoot(config))
        {
            return false;
        }

        var layout = I18nOutputLayout.For(config.Site);
        return variants.Any(variant => layout.IsRootLanguage(variant.Language) &&
            variant.PlannedOutputs.Any(item => item.Category == AssetOutputCategory.Static &&
                string.Equals(BuildPathUtils.NormalizeRelPath(item.Destination), "robots.txt", StringComparison.OrdinalIgnoreCase)));
    }

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
            return [.. context.Results.Select(result => layout.SitemapUrl(siteUrl, result.Language, result.BaseUrl))];
        }

        // Index mode with a root language: its sitemap moves to <lang>/sitemap.xml and is listed next to the index.
        var rootLanguage = context.Results.Select(result => result.Language).FirstOrDefault(layout.IsRootLanguage);
        return string.Equals(mode, "index", StringComparison.OrdinalIgnoreCase) && rootLanguage is not null
            ?
            [
                SitemapGenerator.BuildAbsoluteUrl(siteUrl, context.RootBaseUrl, "/sitemap.xml"),
                layout.SitemapUrl(siteUrl, rootLanguage, context.RootBaseUrl)
            ]
            : null;
    }
}
