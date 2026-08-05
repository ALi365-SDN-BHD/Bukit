using Bukit.Cli.Commands.SeoInsights;

namespace Bukit.Cli.Commands.SeoGenerativeInsights;

internal static class GenerativeAnswerObservationValidator
{
    internal const string AllowedKind = "allowed";
    internal const string ExternalKind = "external";
    internal const string InvalidKind = "invalid";

    internal static GenerativeAnswerObservationValidation Validate(
        GenerativeAnswerObservationDataset dataset,
        SeoObservationUrlOptions options)
    {
        var rows = new List<GenerativeRowValidation>(dataset.Rows.Count);
        foreach (var row in dataset.Rows)
        {
            var classifications = new List<GenerativeCitedUrlClassification>(row.CitedUrls.Count);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var hasAllowedHostUrl = false;
            foreach (var url in row.CitedUrls)
            {
                if (!seen.Add(url))
                {
                    throw Invalid("generative_observation.cited_url_duplicate", "Cited URLs must be unique within a row.");
                }

                var classification = Classify(url, options);
                if (classification.Kind == AllowedKind)
                {
                    hasAllowedHostUrl = true;
                }

                classifications.Add(classification);
            }

            if (row.SiteCited != hasAllowedHostUrl)
            {
                throw Invalid(
                    "generative_observation.site_cited_contradiction",
                    "Site citation flag must agree with allowed-host cited URLs.");
            }

            rows.Add(new GenerativeRowValidation(classifications));
        }

        return new GenerativeAnswerObservationValidation(rows);
    }

    private static GenerativeCitedUrlClassification Classify(string url, SeoObservationUrlOptions options)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            return new GenerativeCitedUrlClassification(url, InvalidKind, "invalid_url");
        }

        if (uri.Scheme is not "http" and not "https")
        {
            return new GenerativeCitedUrlClassification(url, InvalidKind, "unsupported_scheme");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return new GenerativeCitedUrlClassification(url, InvalidKind, "credentials_not_allowed");
        }

        var host = uri.IdnHost.ToLowerInvariant();
        if (host == options.SiteHost.ToLowerInvariant() ||
            options.HostAliases.Any(alias => string.Equals(alias, host, StringComparison.OrdinalIgnoreCase)))
        {
            return new GenerativeCitedUrlClassification(url, AllowedKind, null);
        }

        return new GenerativeCitedUrlClassification(url, ExternalKind, null);
    }

    private static InvalidDataException Invalid(string code, string detail)
        => new($"{code}: {detail}");
}
