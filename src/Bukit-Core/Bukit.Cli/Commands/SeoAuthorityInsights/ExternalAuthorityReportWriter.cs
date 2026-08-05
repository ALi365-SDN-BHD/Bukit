using System.Text.Json;
using Bukit.Cli.Commands.SeoInsights;
using Bukit.Engine;

namespace Bukit.Cli.Commands.SeoAuthorityInsights;

internal static class ExternalAuthorityReportWriter
{
    internal const string FileName = "external-authority-report.json";
    internal const string Schema = "https://bukit.dev/schemas/external-authority-report.v1.json";
    internal const string SchemaVersion = "1.0";

    internal static ExternalAuthorityReport Assemble(
        string routeMapPath,
        IReadOnlyList<(string Path, ExternalAuthorityObservationDataset Dataset)> datasets,
        SeoObservationUrlOptions options,
        DateTimeOffset generatedAt)
    {
        var routeMap = SeoRouteMapReader.Read(routeMapPath);
        var matcher = new SeoObservationRouteMatcher(routeMap, options);
        return Assemble(routeMap, datasets, matcher, generatedAt);
    }

    internal static ExternalAuthorityReport Assemble(
        SeoRouteMap routeMap,
        IReadOnlyList<(string Path, ExternalAuthorityObservationDataset Dataset)> datasets,
        SeoObservationRouteMatcher matcher,
        DateTimeOffset generatedAt)
    {
        if (datasets.Count == 0)
        {
            throw Invalid("external_authority_insights.observations_required", "At least one external authority observation dataset is required.");
        }

        var routesByKey = routeMap.Routes.ToDictionary(route => route.RouteKey, StringComparer.Ordinal);

        var sources = new List<ExternalAuthoritySourceRecord>();
        var providers = new Dictionary<string, (long Total, long Active)>(StringComparer.Ordinal);
        var sourceTypes = new Dictionary<string, (long Total, long Active)>(StringComparer.Ordinal);
        var statuses = new Dictionary<string, long>(StringComparer.Ordinal);
        var routeCitations = new Dictionary<string, long>(StringComparer.Ordinal);
        var urlJoins = new Dictionary<string, UrlJoin>(StringComparer.Ordinal);

        foreach (var (_, dataset) in datasets)
        {
            foreach (var row in dataset.Rows)
            {
                var isActive = string.Equals(row.Status, ExternalAuthorityObservationReader.ActiveStatus, StringComparison.Ordinal);

                var rowRouteKeys = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var citedUrl in row.CitedUrls)
                {
                    var match = matcher.Match(citedUrl);
                    if (string.Equals(match.ErrorCode, SeoObservationUrlNormalizer.HostNotAllowed, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var joinKey = match.NormalizedUrl ?? citedUrl;
                    if (!urlJoins.TryGetValue(joinKey, out var join))
                    {
                        join = JoinUrl(match);
                        urlJoins[joinKey] = join;
                    }

                    if (join.Kind == SeoObservationMatchKind.Matched && join.RouteKey is not null)
                    {
                        rowRouteKeys.Add(join.RouteKey);
                    }
                }

                sources.Add(new ExternalAuthoritySourceRecord(
                    dataset.Provider,
                    row.SourceType,
                    row.Status,
                    row.ObservedAt,
                    row.SourceUrl,
                    row.ContextHash,
                    rowRouteKeys.ToArray()));

                Bump(providers, dataset.Provider, isActive);
                Bump(sourceTypes, row.SourceType, isActive);
                statuses[row.Status] = statuses.GetValueOrDefault(row.Status) + 1;

                if (isActive)
                {
                    foreach (var routeKey in rowRouteKeys)
                    {
                        routeCitations[routeKey] = routeCitations.GetValueOrDefault(routeKey) + 1;
                    }
                }
            }
        }

        var activeSources = sources.Count(source =>
            string.Equals(source.Status, ExternalAuthorityObservationReader.ActiveStatus, StringComparison.Ordinal));

        var providerCounts = providers
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new ExternalAuthorityProviderCounts(entry.Key, entry.Value.Total, entry.Value.Active))
            .ToArray();

        var sourceTypeCounts = sourceTypes
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new ExternalAuthoritySourceTypeCounts(entry.Key, entry.Value.Total, entry.Value.Active))
            .ToArray();

        var statusCounts = statuses
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new ExternalAuthorityStatusCounts(entry.Key, entry.Value))
            .ToArray();

        var routes = routeCitations
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new ExternalAuthorityRouteCitation(
                entry.Key,
                routesByKey.TryGetValue(entry.Key, out var route) ? route.Canonical : entry.Key,
                entry.Value))
            .ToArray();

        var unmatched = urlJoins
            .Where(entry => entry.Value.Kind == SeoObservationMatchKind.Unmatched)
            .Select(entry => new ExternalAuthorityUnmatchedCitedUrl(entry.Key, entry.Value.ErrorCode))
            .OrderBy(entry => entry.Url, StringComparer.Ordinal)
            .ToArray();

        var ambiguous = urlJoins
            .Where(entry => entry.Value.Kind == SeoObservationMatchKind.Ambiguous)
            .Select(entry => new ExternalAuthorityAmbiguousCitedUrl(entry.Key, entry.Value.Candidates))
            .OrderBy(entry => entry.Url, StringComparer.Ordinal)
            .ToArray();

        var matchedUrls = urlJoins.Count(entry => entry.Value.Kind == SeoObservationMatchKind.Matched);
        var unmatchedUrls = urlJoins.Count(entry => entry.Value.Kind == SeoObservationMatchKind.Unmatched);
        var ambiguousUrls = urlJoins.Count(entry => entry.Value.Kind == SeoObservationMatchKind.Ambiguous);

        return new ExternalAuthorityReport(
            Schema,
            SchemaVersion,
            generatedAt,
            sources,
            new ExternalAuthorityOverall(sources.Count, activeSources, routeCitations.Count),
            providerCounts,
            sourceTypeCounts,
            statusCounts,
            routes,
            unmatched,
            ambiguous,
            new ExternalAuthorityJoinQuality(urlJoins.Count, matchedUrls, unmatchedUrls, ambiguousUrls));
    }

    internal static void Write(string outputDir, ExternalAuthorityReport report)
    {
        var json = JsonSerializer.Serialize(report, SeoAuthorityInsightsJsonContext.Default.ExternalAuthorityReport);
        FileWriter.WriteUtf8(
            outputDir,
            Path.Combine(BuildReporter.ReportDirectoryName, FileName),
            json + Environment.NewLine);
    }

    private static void Bump(Dictionary<string, (long Total, long Active)> groups, string key, bool isActive)
    {
        groups.TryGetValue(key, out var counts);
        groups[key] = (counts.Total + 1, counts.Active + (isActive ? 1 : 0));
    }

    private static UrlJoin JoinUrl(SeoObservationRouteMatch match)
    {
        return match.Kind switch
        {
            SeoObservationMatchKind.Matched => new UrlJoin(match.Kind, match.RouteKey, [], match.ErrorCode),
            SeoObservationMatchKind.Ambiguous => new UrlJoin(
                match.Kind,
                null,
                match.Candidates
                    .Select(candidate => candidate.RouteKey)
                    .OrderBy(routeKey => routeKey, StringComparer.Ordinal)
                    .ToArray(),
                match.ErrorCode),
            _ => new UrlJoin(match.Kind, null, [], match.ErrorCode)
        };
    }

    private static InvalidDataException Invalid(string code, string detail)
        => new($"{code}: {detail}");

    private sealed record UrlJoin(
        SeoObservationMatchKind Kind,
        string? RouteKey,
        IReadOnlyList<string> Candidates,
        string? ErrorCode);
}
