using System.Globalization;

namespace Bukit.Cli.Commands.SeoInsights;

internal sealed record SeoObservationUrlOptions(
    string SiteHost,
    IReadOnlySet<string> HostAliases,
    IReadOnlySet<string> IgnoredQueryParameters);

internal sealed record SeoObservationUrlNormalizationResult(
    bool Success,
    string? NormalizedUrl,
    string? MatchKey,
    string? ErrorCode);

internal static class SeoObservationUrlNormalizer
{
    internal const string InvalidUrl = "invalid_url";
    internal const string UnsupportedScheme = "unsupported_scheme";
    internal const string CredentialsNotAllowed = "credentials_not_allowed";
    internal const string HostNotAllowed = "host_not_allowed";

    internal static SeoObservationUrlNormalizationResult Normalize(
        string value,
        SeoObservationUrlOptions options)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            return Failure(InvalidUrl);
        }

        if (uri.Scheme is not "http" and not "https")
        {
            return Failure(UnsupportedScheme);
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return Failure(CredentialsNotAllowed);
        }

        var normalizedHost = NormalizeHost(uri.IdnHost);
        if (normalizedHost is null || !AllowedHosts(options).Contains(normalizedHost))
        {
            return Failure(HostNotAllowed);
        }

        var path = NormalizePath(uri.AbsolutePath);
        if (!TryNormalizeQuery(uri.Query, options.IgnoredQueryParameters, out var query))
        {
            return Failure(InvalidUrl);
        }

        var port = uri.IsDefaultPort ? -1 : uri.Port;
        var builder = new UriBuilder(uri.Scheme.ToLowerInvariant(), normalizedHost, port, path)
        {
            Fragment = string.Empty,
            Query = query
        };
        var normalizedUrl = builder.Uri.AbsoluteUri;
        var matchKey = path + (query.Length == 0 ? string.Empty : "?" + query);
        return new SeoObservationUrlNormalizationResult(true, normalizedUrl, matchKey, null);
    }

    private static HashSet<string> AllowedHosts(SeoObservationUrlOptions options)
    {
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddHost(hosts, options.SiteHost);
        foreach (var alias in options.HostAliases)
        {
            AddHost(hosts, alias);
        }

        return hosts;
    }

    private static void AddHost(HashSet<string> hosts, string value)
    {
        var normalized = NormalizeHost(value);
        if (normalized is not null)
        {
            hosts.Add(normalized);
        }
    }

    private static string? NormalizeHost(string value)
    {
        var trimmed = value.Trim().TrimEnd('.');
        if (trimmed.Length == 0)
        {
            return null;
        }

        try
        {
            return new IdnMapping().GetAscii(trimmed).ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string NormalizePath(string value)
    {
        var path = string.IsNullOrEmpty(value) ? "/" : value;
        if (!path.EndsWith('/') && !Path.HasExtension(path))
        {
            path += "/";
        }

        return path;
    }

    private static bool TryNormalizeQuery(
        string value,
        IReadOnlySet<string> ignoredNames,
        out string normalized)
    {
        var pairs = new List<QueryPair>();
        var query = value.StartsWith('?') ? value[1..] : value;
        if (query.Length != 0)
        {
            foreach (var part in query.Split('&'))
            {
                var separator = part.IndexOf('=');
                var encodedName = separator < 0 ? part : part[..separator];
                var encodedValue = separator < 0 ? string.Empty : part[(separator + 1)..];
                string name;
                string pairValue;
                try
                {
                    name = Uri.UnescapeDataString(encodedName);
                    pairValue = Uri.UnescapeDataString(encodedValue);
                }
                catch (UriFormatException)
                {
                    normalized = string.Empty;
                    return false;
                }

                if (ignoredNames.Any(ignored => string.Equals(ignored, name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                pairs.Add(new QueryPair(name, pairValue, separator >= 0));
            }
        }

        normalized = string.Join(
            "&",
            pairs
                .OrderBy(pair => pair.Name, StringComparer.Ordinal)
                .ThenBy(pair => pair.Value, StringComparer.Ordinal)
                .ThenBy(pair => pair.HasValue)
                .Select(pair => pair.HasValue
                    ? $"{Uri.EscapeDataString(pair.Name)}={Uri.EscapeDataString(pair.Value)}"
                    : Uri.EscapeDataString(pair.Name)));
        return true;
    }

    private static SeoObservationUrlNormalizationResult Failure(string errorCode)
        => new(false, null, null, errorCode);

    private sealed record QueryPair(string Name, string Value, bool HasValue);
}
