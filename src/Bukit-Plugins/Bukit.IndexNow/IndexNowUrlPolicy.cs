namespace Bukit.IndexNow;

public static class IndexNowUrlPolicy
{
    public const string AllowedSiteUrl = "https://silushangxun.com/";
    public const string AllowedHost = "silushangxun.com";

    public static Uri ParseSiteUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(value, AllowedSiteUrl, StringComparison.Ordinal) ||
            !IsExactAllowedUri(uri, requireRoot: true))
        {
            throw new InvalidOperationException("Site URL must be exactly https://silushangxun.com/.");
        }

        return uri;
    }

    public static Uri ParseContentUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !IsExactAllowedUri(uri, requireRoot: false) ||
            !string.Equals(uri.AbsoluteUri, value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("IndexNow URL must use the exact https://silushangxun.com host.");
        }

        return uri;
    }

    private static bool IsExactAllowedUri(Uri uri, bool requireRoot)
        => uri.Scheme == Uri.UriSchemeHttps &&
           string.Equals(uri.Host, AllowedHost, StringComparison.Ordinal) &&
           uri.IsDefaultPort &&
           string.IsNullOrEmpty(uri.UserInfo) &&
           string.IsNullOrEmpty(uri.Query) &&
           string.IsNullOrEmpty(uri.Fragment) &&
           (!requireRoot || uri.AbsolutePath == "/");
}
