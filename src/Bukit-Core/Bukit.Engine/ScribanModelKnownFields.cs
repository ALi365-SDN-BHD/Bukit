namespace Bukit.Engine;

internal static class ScribanModelKnownFields
{
    internal static class PageFields
    {
        public const string Title = "title";
        public const string Url = "url";
        public const string Content = "content";
        public const string Summary = "summary";
        public const string TableOfContents = "table_of_contents";
        public const string PublishDate = "publish_date";
        public const string UpdatedAt = "updated_at";
        public const string Fields = "fields";
        public const string Seo = "seo";
        public const string Alternates = "alternates";
        public const string Term = "term";
        public const string Terms = "terms";
    }

    internal static class SiteFields
    {
        public const string Name = "name";
        public const string Title = "title";
        public const string Url = "url";
        public const string Description = "description";
        public const string BaseUrl = "base_url";
        public const string Language = "language";
        public const string Params = "params";
        public const string Modules = "modules";
        public const string Data = "data";
        public const string DataIndex = "data_index";
        public const string Analytics = "analytics";
    }

    internal static class ListPageFields
    {
        public const string Site = "site";
        public const string Page = "page";
        public const string Pages = "pages";
    }

    internal static class SeoFields
    {
        public const string Title = "title";
        public const string DocumentTitle = "document_title";
        public const string Description = "description";
        public const string Canonical = "canonical";
        public const string Prev = "prev";
        public const string Next = "next";
        public const string Robots = "robots";
        public const string Og = "og";
        public const string Twitter = "twitter";
        public const string Article = "article";
        public const string Alternates = "alternates";
        public const string JsonLd = "json_ld";
        public const string SchemaType = "schema_type";
    }

    internal static class SeoOgFields
    {
        public const string Title = "title";
        public const string Description = "description";
        public const string Url = "url";
        public const string Image = "image";
        public const string Type = "type";
        public const string SiteName = "site_name";
        public const string Locale = "locale";
    }

    internal static class SeoTwitterFields
    {
        public const string Card = "card";
        public const string Title = "title";
        public const string Description = "description";
        public const string Image = "image";
        public const string Site = "site";
        public const string Creator = "creator";
    }

    internal static class AnalyticsFields
    {
        public const string Enabled = "enabled";
        public const string GoogleAnalyticsId = "googleAnalyticsId";
    }

    private static readonly HashSet<string> _pageFields = new(StringComparer.OrdinalIgnoreCase)
    {
        PageFields.Title, PageFields.Url, PageFields.Content,
        PageFields.Summary, PageFields.TableOfContents,
        PageFields.PublishDate, PageFields.UpdatedAt, PageFields.Fields, PageFields.Seo,
        PageFields.Alternates, PageFields.Term, PageFields.Terms
    };

    private static readonly HashSet<string> _siteFields = new(StringComparer.OrdinalIgnoreCase)
    {
        SiteFields.Name, SiteFields.Title, SiteFields.Url,
        SiteFields.Description, SiteFields.BaseUrl, SiteFields.Language,
        SiteFields.Params, SiteFields.Modules, SiteFields.Data, SiteFields.DataIndex,
        SiteFields.Analytics
    };

    private static readonly HashSet<string> _listPageFields = new(StringComparer.OrdinalIgnoreCase)
    {
        ListPageFields.Site, ListPageFields.Page, ListPageFields.Pages
    };

    private static readonly HashSet<string> _seoFields = new(StringComparer.OrdinalIgnoreCase)
    {
        SeoFields.Title, SeoFields.DocumentTitle, SeoFields.Description, SeoFields.Canonical,
        SeoFields.Prev, SeoFields.Next, SeoFields.Robots, SeoFields.Og, SeoFields.Twitter,
        SeoFields.Article, SeoFields.Alternates, SeoFields.JsonLd,
        SeoFields.SchemaType
    };

    private static readonly HashSet<string> _seoOgFields = new(StringComparer.OrdinalIgnoreCase)
    {
        SeoOgFields.Title, SeoOgFields.Description, SeoOgFields.Url,
        SeoOgFields.Image, SeoOgFields.Type, SeoOgFields.SiteName,
        SeoOgFields.Locale
    };

    private static readonly HashSet<string> _seoTwitterFields = new(StringComparer.OrdinalIgnoreCase)
    {
        SeoTwitterFields.Card, SeoTwitterFields.Title, SeoTwitterFields.Description,
        SeoTwitterFields.Image, SeoTwitterFields.Site, SeoTwitterFields.Creator
    };

    private static readonly HashSet<string> _analyticsFields = new(StringComparer.OrdinalIgnoreCase)
    {
        AnalyticsFields.Enabled, AnalyticsFields.GoogleAnalyticsId
    };

    private static readonly HashSet<string> _loopVarPageFields = new(StringComparer.OrdinalIgnoreCase)
    {
        PageFields.Title, PageFields.Url, PageFields.Content,
        PageFields.Summary, PageFields.PublishDate, PageFields.UpdatedAt, PageFields.Fields
    };

    internal static readonly HashSet<string> KnownRootContexts = new(StringComparer.OrdinalIgnoreCase)
    {
        "page", "site", "list", "pages"
    };

    internal static HashSet<string> ForPage() => _pageFields;
    internal static HashSet<string> ForSite() => _siteFields;
    internal static HashSet<string> ForListPage() => _listPageFields;

    internal static bool IsKnownField(string root, string fieldPath)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(fieldPath))
        {
            return false;
        }

        var parts = fieldPath.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        return root.ToLowerInvariant() switch
        {
            "page" => IsKnownPageField(parts, 0),
            "site" => IsKnownSiteField(parts, 0),
            "list" => true,
            "p" or "item" => IsKnownLoopVarField(parts, 0),
            "section" => true,
            "items" => true,
            _ => IsKnownLoopVarField(parts, 0)
        };
    }

    private static bool IsKnownPageField(string[] parts, int offset)
    {
        if (offset >= parts.Length) return true;
        if (!_pageFields.Contains(parts[offset])) return false;
        if (offset + 1 >= parts.Length) return true;

        return parts[offset].ToLowerInvariant() switch
        {
            "seo" => IsKnownSeoField(parts, offset + 1),
            "fields" or "alternates" or "term" or "terms" => true,
            _ => false
        };
    }

    private static bool IsKnownSeoField(string[] parts, int offset)
    {
        if (offset >= parts.Length) return true;
        if (!_seoFields.Contains(parts[offset])) return false;
        if (offset + 1 >= parts.Length) return true;

        return parts[offset].ToLowerInvariant() switch
        {
            "og" when offset + 1 < parts.Length => _seoOgFields.Contains(parts[offset + 1]),
            "twitter" when offset + 1 < parts.Length => _seoTwitterFields.Contains(parts[offset + 1]),
            _ => true
        };
    }

    private static bool IsKnownSiteField(string[] parts, int offset)
    {
        if (offset >= parts.Length) return true;
        if (!_siteFields.Contains(parts[offset])) return false;
        if (offset + 1 >= parts.Length) return true;

        return parts[offset].ToLowerInvariant() switch
        {
            "analytics" when offset + 1 < parts.Length => _analyticsFields.Contains(parts[offset + 1]),
            "params" or "data" or "data_index" or "modules" => true,
            _ => false
        };
    }

    private static bool IsKnownLoopVarField(string[] parts, int offset)
    {
        if (offset >= parts.Length) return true;
        if (!_loopVarPageFields.Contains(parts[offset])) return false;
        return true;
    }
}
