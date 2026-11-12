using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

/// <summary>
/// With the default language at the root, its old prefixed home URL gets a redirect page
/// back to the site root unless user content already owns that path.
/// </summary>
internal static class I18nLegacyRedirectDeriver
{
    internal static void Derive(BuildVariantContext context, BuildContext pluginContext)
    {
        var site = context.Config.Site;
        if (!site.DefaultLanguageRedirect || !I18nOutputLayout.IsRootLanguageVariant(context.Config))
        {
            return;
        }

        var language = site.Language.Trim();
        var outputPath = BuildPathUtils.NormalizeRelPath(Path.Combine(language, "index.html"));
        var occupied = pluginContext.RoutedDocuments.Any(document => SamePath(document.Route.OutputPath, outputPath)) ||
                       pluginContext.DerivedDocuments.Any(document => SamePath(document.Route.OutputPath, outputPath)) ||
                       pluginContext.StaticHtmlRoutes.Any(route => SamePath(route.OutputPath, outputPath)) ||
                       File.Exists(Path.Combine(context.StaticDir, outputPath)) ||
                       (context.ParentStaticDir is not null && File.Exists(Path.Combine(context.ParentStaticDir, outputPath)));
        if (occupied)
        {
            pluginContext.Logger.Info(
                $"event=i18n.legacy_root_redirect.skipped path={outputPath} reason=user-output");
            return;
        }

        var target = SeoModelBuilder.BuildAbsoluteUrl(site.Url, context.BaseUrl, "/");
        var document = DerivedContentDocumentFactory.Create(
            id: $"i18n-legacy-home-{language}",
            title: "Redirect",
            slug: $"i18n-legacy-home-{language}",
            publishAt: pluginContext.RoutedDocuments.Count > 0 ? pluginContext.RoutedDocuments[0].Document.PublishAt : DateTimeOffset.UnixEpoch,
            body: new ContentBodyRef(Html: RedirectPageHtml.Build(target)),
            customFields: ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "redirect",
                ["collection"] = "redirect",
                ["excludeFromSitemap"] = true
            }));
        var route = new RouteInfo("/" + language + "/", outputPath, RedirectPageHtml.RouteTemplate);
        var derived = new RoutedContentDocument(document, route, document.PublishAt);
        pluginContext.DerivedDocuments.Add(derived);
        pluginContext.DerivedRoutes.Add((route, document.PublishAt));
    }

    private static bool SamePath(string left, string right)
        => string.Equals(
            BuildPathUtils.NormalizeRelPath(left),
            BuildPathUtils.NormalizeRelPath(right),
            StringComparison.OrdinalIgnoreCase);
}
