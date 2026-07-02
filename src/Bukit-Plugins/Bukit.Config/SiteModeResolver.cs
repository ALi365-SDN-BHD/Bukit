namespace Bukit.Config;

public static class SiteModeResolver
{
    public static string ResolveFeedMode(SiteConfig site)
        => (site.Feed.Mode ?? "split").Trim().ToLowerInvariant();

    public static string ResolveSearchMode(SiteConfig site)
        => (site.Search.Mode ?? "split").Trim().ToLowerInvariant();
}
