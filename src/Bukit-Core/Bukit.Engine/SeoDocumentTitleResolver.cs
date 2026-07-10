using Bukit.Config;
using Bukit.Rendering;
using System.Text.RegularExpressions;

namespace Bukit.Engine;

internal static class SeoDocumentTitleResolver
{
    private static readonly Regex PlaceholderRegex = new(
        @"\{(pageTitle|siteTitle|separator)\}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static string Resolve(
        SeoConfig seo,
        string siteTitle,
        string pageTitle,
        string routeUrl)
    {
        var template = string.Equals(routeUrl, "/", StringComparison.Ordinal)
            ? seo.HomeTitleTemplate
            : seo.PageTitleTemplate;

        var resolved = PlaceholderRegex.Replace(template, match =>
        {
            var placeholder = match.Groups[1].Value;
            if (string.Equals(placeholder, "pageTitle", StringComparison.OrdinalIgnoreCase))
            {
                return pageTitle;
            }

            if (string.Equals(placeholder, "siteTitle", StringComparison.OrdinalIgnoreCase))
            {
                return siteTitle;
            }

            return seo.TitleSeparator;
        });

        return Normalize(resolved);
    }

    internal static string ResolveEffective(SeoModel seo)
        => Normalize(string.IsNullOrWhiteSpace(seo.DocumentTitle) ? seo.Title : seo.DocumentTitle);

    internal static string Normalize(string value)
        => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
