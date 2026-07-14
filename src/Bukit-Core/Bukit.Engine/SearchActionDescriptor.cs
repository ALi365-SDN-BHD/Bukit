using Bukit.Config;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed record SearchActionDescriptor(string Target, string QueryInput);

internal static class SearchActionDescriptorResolver
{
    private const string SearchTermPlaceholder = "{search_term_string}";

    internal static SearchActionDescriptor? Resolve(
        AppConfig config,
        string baseUrl,
        IEnumerable<RouteInfo> finalHtmlRoutes)
    {
        var declaredRoute = config.Site.Search.Route;
        if (!config.Site.Seo.Enabled ||
            !config.Site.Seo.Schema.SearchAction ||
            string.IsNullOrWhiteSpace(declaredRoute))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(config.Site.Url))
        {
            throw new ConfigException(
                "site.url is required when site.search.route enables SearchAction.",
                DiagnosticCode.ConfigInvalidValue);
        }

        var normalizedRoute = RoutePathBuilder.NormalizeUrl(declaredRoute.Trim());
        var routeExists = finalHtmlRoutes.Any(route =>
            string.Equals(
                RoutePathBuilder.NormalizeUrl(route.Url),
                normalizedRoute,
                StringComparison.OrdinalIgnoreCase));
        if (!routeExists)
        {
            throw new ConfigException(
                $"site.search.route '{declaredRoute.Trim()}' does not match any final HTML route.",
                DiagnosticCode.ConfigInvalidValue);
        }

        var target = SeoModelBuilder.BuildAbsoluteUrl(
            config.Site.Url,
            baseUrl,
            $"{normalizedRoute}?q={SearchTermPlaceholder}");
        return new SearchActionDescriptor(target, $"required name={SearchTermPlaceholder.Trim('{', '}')}");
    }
}
