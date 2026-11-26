using Bukit.Shared;
using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

/// <summary>Reads, validates and applies <c>site.i18n.&lt;lang&gt;</c>, the per-language site identity.</summary>
internal static class SiteLanguageIdentity
{
    internal static IReadOnlyDictionary<string, SiteLanguageIdentityConfig>? Read(YamlMappingNode siteNode)
    {
        if (ConfigYamlHelpers.GetOptionalMapping(siteNode, "i18n") is not { } i18nNode)
        {
            return null;
        }

        var identities = new Dictionary<string, SiteLanguageIdentityConfig>(StringComparer.Ordinal);
        foreach (var (key, value) in i18nNode.Children)
        {
            if (key is not YamlScalarNode { Value: { Length: > 0 } language } || value is not YamlMappingNode node)
            {
                throw new ConfigException("site.i18n must map each site language to a mapping.", DiagnosticCode.ConfigInvalidValue);
            }

            var path = $"site.i18n.{language}";

            var organization = ConfigYamlHelpers.GetOptionalMapping(node, "organization", path);
            identities[language] = new SiteLanguageIdentityConfig
            {
                Title = ConfigYamlHelpers.GetOptionalString(node, "title"),
                Description = ConfigYamlHelpers.GetOptionalString(node, "description"),
                TitleSeparator = ConfigYamlHelpers.GetOptionalString(node, "titleSeparator"),
                Organization = organization is null
                    ? null
                    : new SiteLanguageOrganizationConfig
                    {
                        Name = ConfigYamlHelpers.GetOptionalString(organization, "name"),
                        Url = ConfigYamlHelpers.GetOptionalString(organization, "url"),
                        Logo = ConfigYamlHelpers.GetOptionalString(organization, "logo"),
                        SameAs = ConfigYamlHelpers.ReadStringList(organization, "sameAs")
                    }
            };
        }

        return identities;
    }

    internal static void Validate(SiteConfig site)
    {
        if (site.I18n is not { Count: > 0 } identities)
        {
            return;
        }

        var languages = SiteLanguageResolver.Clean(site.Languages);
        if (languages.Count < 2)
        {
            throw new ConfigException(
                "site.i18n requires two or more site.languages; a single-language site sets its identity with the top-level site.title, site.description and site.seo.organization.",
                DiagnosticCode.ConfigInvalidValue);
        }

        foreach (var (language, identity) in identities)
        {
            if (!languages.Contains(language, StringComparer.Ordinal))
            {
                throw new ConfigException(
                    $"site.i18n.{language} is not a site language; use one of: {string.Join(", ", languages)}.",
                    DiagnosticCode.ConfigInvalidValue);
            }

            if (identity.Title is { } title && string.IsNullOrWhiteSpace(title))
            {
                throw new ConfigException($"site.i18n.{language}.title must not be blank.", DiagnosticCode.ConfigInvalidValue);
            }
        }
    }

    /// <summary>The site identity of <paramref name="language"/>'s build: its <c>site.i18n</c> entry over the top-level values (<c>titleSeparator</c> over <c>site.seo.titleSeparator</c>).</summary>
    internal static SiteConfig Apply(SiteConfig site, string language)
    {
        ArgumentNullException.ThrowIfNull(site);
        if (site.I18n is null || !site.I18n.TryGetValue(language, out var identity))
        {
            return site;
        }

        return site with
        {
            Title = identity.Title ?? site.Title,
            Description = identity.Description ?? site.Description,
            Seo = site.Seo with
            {
                TitleSeparator = identity.TitleSeparator ?? site.Seo.TitleSeparator,
                Organization = identity.Organization is { } organization
                    ? Merge(site.Seo.Organization ?? new SeoOrganizationConfig(), organization)
                    : site.Seo.Organization
            }
        };
    }

    private static SeoOrganizationConfig Merge(SeoOrganizationConfig organization, SiteLanguageOrganizationConfig overrides)
        => organization with
        {
            Name = overrides.Name ?? organization.Name,
            Url = overrides.Url ?? organization.Url,
            Logo = overrides.Logo ?? organization.Logo,
            SameAs = overrides.SameAs ?? organization.SameAs
        };
}
