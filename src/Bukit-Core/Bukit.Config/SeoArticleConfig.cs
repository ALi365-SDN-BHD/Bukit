using Bukit.Shared;
using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

/// <summary>Reads and validates the article schema default and <c>site.seo.authors</c>.</summary>
internal static class SeoArticleConfig
{
    internal const string DefaultArticleType = "BlogPosting";

    /// <summary>True for BlogPosting and every schema.org type whose name ends in Article.</summary>
    internal static bool IsArticleType(string? schemaType)
        => schemaType is not null &&
           (schemaType.EndsWith("Article", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(schemaType, DefaultArticleType, StringComparison.OrdinalIgnoreCase));

    /// <summary>Schema type of an article page that sets none: its collection's, then the site's, then BlogPosting.</summary>
    internal static string ResolveArticleType(SiteConfig site, string? collection)
    {
        ArgumentNullException.ThrowIfNull(site);
        var collectionType = collection is not null && site.Collections?.TryGetValue(collection, out var config) is true
            ? config.ArticleType
            : null;
        return (collectionType ?? site.Seo.Schema.ArticleType)?.Trim() ?? DefaultArticleType;
    }

    internal static IReadOnlyDictionary<string, SeoAuthorProfileConfig>? ReadAuthors(YamlMappingNode seoNode)
    {
        if (ConfigYamlHelpers.GetOptionalMapping(seoNode, "authors", "site.seo") is not { } authorsNode)
        {
            return null;
        }

        var authors = new Dictionary<string, SeoAuthorProfileConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in authorsNode.Children)
        {
            if (key is not YamlScalarNode { Value: { } name } || string.IsNullOrWhiteSpace(name) || value is not YamlMappingNode node)
            {
                throw new ConfigException("site.seo.authors must map each author name or id to a mapping.", DiagnosticCode.ConfigInvalidValue);
            }

            var trimmed = name.Trim();
            var path = $"site.seo.authors.{trimmed}";
            var profile = new SeoAuthorProfileConfig
            {
                Name = ConfigYamlHelpers.GetOptionalString(node, "name", path),
                Type = ConfigYamlHelpers.GetOptionalString(node, "type", path),
                Url = ConfigYamlHelpers.GetOptionalString(node, "url", path),
                Image = ConfigYamlHelpers.GetOptionalString(node, "image", path),
                SameAs = ConfigYamlHelpers.ReadStringList(node, "sameAs") ?? []
            };
            if (!authors.TryAdd(trimmed, profile))
            {
                throw new ConfigException($"site.seo.authors lists '{trimmed}' twice (keys match case-insensitively).", DiagnosticCode.ConfigInvalidValue);
            }
        }

        return authors;
    }

    internal static void Validate(SiteConfig site)
    {
        ArgumentNullException.ThrowIfNull(site);
        ValidateArticleType(site.Seo.Schema.ArticleType, "site.seo.schema.articleType");
        if (site.Collections is not null)
        {
            foreach (var (name, collection) in site.Collections)
            {
                ValidateArticleType(collection.ArticleType, $"site.collections.{name}.seo.articleType");
            }
        }

        if (site.Seo.Authors is null)
        {
            return;
        }

        foreach (var (key, author) in site.Seo.Authors)
        {
            var path = $"site.seo.authors.{key}";
            RejectBlank(author.Name, $"{path}.name");
            RejectBlank(author.Url, $"{path}.url");
            RejectBlank(author.Image, $"{path}.image");
            if (author.Type is { } type &&
                !type.Trim().Equals("Person", StringComparison.OrdinalIgnoreCase) &&
                !type.Trim().Equals("Organization", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConfigException($"{path}.type must be Person|Organization.", DiagnosticCode.ConfigInvalidValue);
            }
        }
    }

    private static void ValidateArticleType(string? value, string path)
    {
        if (value is null)
        {
            return;
        }

        if (!IsArticleType(value.Trim()) || !value.Trim().All(char.IsAsciiLetter))
        {
            throw new ConfigException(
                $"{path} must be BlogPosting or a schema.org Article type such as Article, NewsArticle or TechArticle.",
                DiagnosticCode.ConfigInvalidValue);
        }
    }

    private static void RejectBlank(string? value, string path)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            throw new ConfigException($"{path} must not be blank.", DiagnosticCode.ConfigInvalidValue);
        }
    }
}
