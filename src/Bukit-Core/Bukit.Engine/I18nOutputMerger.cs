using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class I18nOutputMerger
{
    internal static IReadOnlyList<string> GetLanguages(SiteConfig site)
        => I18nLanguagePolicy.GetLanguages(site);

    internal static string GetDefaultLanguage(SiteConfig site, IReadOnlyList<string> languages)
        => I18nLanguagePolicy.GetDefaultLanguage(site, languages);

    internal static string CombineBaseUrlWithLanguage(string baseUrl, string language)
        => I18nLanguagePolicy.CombineBaseUrlWithLanguage(baseUrl, language);

    internal static IReadOnlyList<ContentDocument> FilterDocumentsByLanguage(
        IReadOnlyList<ContentDocument> documents,
        string language,
        string defaultLanguage)
        => I18nLanguagePolicy.FilterDocumentsByLanguage(documents, language, defaultLanguage);

    internal static IReadOnlyList<PublishProjectionResult> GenerateRootOutputs(
        AppConfig config,
        string outputDir,
        string rootBaseUrl,
        IReadOnlyList<BuildVariantResult> results,
        ILogger logger,
        ISearchIndexBuilder searchIndexBuilder)
    {
        return I18nRootProjectionCoordinator.GenerateRootOutputs(
            config,
            outputDir,
            rootBaseUrl,
            results,
            logger,
            searchIndexBuilder);
    }

    internal static PublishProjectionResult ProjectRootAggregate(
        PublishProjectionContext context,
        PublishRepresentation representation)
        => I18nRootProjectionCoordinator.Project(context, representation);

}
