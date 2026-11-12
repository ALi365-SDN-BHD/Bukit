using Bukit.Config;

namespace Bukit.Engine;

/// <summary>
/// Root redirect for the prefixed layout. It is deliberately outside the root projection
/// registry: it is not an aggregate representation, so it cannot change build-report,
/// publish-audit or machine-readability contracts.
/// </summary>
internal static class I18nRootRedirectWriter
{
    internal static bool ShouldWrite(AppConfig config)
        => config.Site.DefaultLanguageRedirect &&
           config.Site.DefaultLanguageInSubdir &&
           I18nOutputLayout.For(config.Site).IsMultilingual;

    internal static void WriteIfRequested(AppConfig config, string outputDir)
    {
        if (!ShouldWrite(config))
        {
            return;
        }

        var languages = I18nOutputMerger.GetLanguages(config.Site);
        var layout = I18nOutputLayout.Create(config.Site, languages, config.Site.BaseUrl);
        var defaultLanguage = I18nLanguagePolicy.GetDefaultLanguage(config.Site, languages);
        var target = SeoModelBuilder.BuildAbsoluteUrl(config.Site.Url, layout.BaseUrl(defaultLanguage), "/");
        FileWriter.WriteUtf8(outputDir, "index.html", RedirectPageHtml.Build(target));
    }
}
