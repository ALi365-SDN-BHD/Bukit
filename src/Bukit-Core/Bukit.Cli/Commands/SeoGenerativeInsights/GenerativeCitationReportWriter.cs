using System.Text.Json;
using Bukit.Cli.Commands.SeoInsights;
using Bukit.Engine;

namespace Bukit.Cli.Commands.SeoGenerativeInsights;

internal static class GenerativeCitationReportWriter
{
    internal const string FileName = "generative-citation-report.json";
    internal const string Schema = "https://bukit.dev/schemas/generative-citation-report.v1.json";
    internal const string SchemaVersion = "1.0";

    internal static GenerativeCitationReport Assemble(
        string routeMapPath,
        IReadOnlyList<(string Path, GenerativeAnswerObservationDataset Dataset)> observations,
        SeoObservationUrlOptions options,
        DateTimeOffset generatedAt)
    {
        var routeMap = SeoRouteMapReader.Read(routeMapPath);
        var matcher = new SeoObservationRouteMatcher(routeMap, options);
        return Assemble(routeMap, observations, matcher, options, generatedAt);
    }

    internal static GenerativeCitationReport Assemble(
        SeoRouteMap routeMap,
        IReadOnlyList<(string Path, GenerativeAnswerObservationDataset Dataset)> observations,
        SeoObservationRouteMatcher matcher,
        SeoObservationUrlOptions options,
        DateTimeOffset generatedAt)
    {
        if (observations.Count == 0)
        {
            throw Invalid("generative_insights.observations_required", "At least one generative observation dataset is required.");
        }

        var routesByKey = routeMap.Routes.ToDictionary(route => route.RouteKey, StringComparer.Ordinal);

        var sources = observations
            .Select(entry => new GenerativeCitationSource(
                entry.Dataset.Engine,
                entry.Dataset.PromptSetVersion,
                entry.Dataset.Locale,
                entry.Dataset.CollectedAt,
                entry.Dataset.CollectionMethod,
                entry.Path,
                entry.Dataset.Rows.Count))
            .ToArray();

        var overall = new StatsAccumulator();
        var engines = new Dictionary<string, StatsAccumulator>(StringComparer.Ordinal);
        var questions = new Dictionary<string, StatsAccumulator>(StringComparer.Ordinal);
        var routeCitations = new Dictionary<(string QuestionKey, string RouteKey), long>();
        var externalRuns = new Dictionary<string, long>(StringComparer.Ordinal);
        var urlJoins = new Dictionary<string, UrlJoin>(StringComparer.Ordinal);
        var invalidUrls = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (_, dataset) in observations)
        {
            var validation = GenerativeAnswerObservationValidator.Validate(dataset, options);
            for (var index = 0; index < dataset.Rows.Count; index++)
            {
                var row = dataset.Rows[index];
                overall.Add(row);
                Add(engines, dataset.Engine).Add(row);
                Add(questions, row.QuestionKey).Add(row);

                var runRouteKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var classification in validation.Rows[index].CitedUrls)
                {
                    switch (classification.Kind)
                    {
                        case GenerativeAnswerObservationValidator.AllowedKind:
                            var match = matcher.Match(classification.Url);
                            var joinKey = match.NormalizedUrl ?? classification.Url;
                            if (!urlJoins.TryGetValue(joinKey, out var join))
                            {
                                join = JoinUrl(match);
                                urlJoins[joinKey] = join;
                            }

                            if (join.Kind == SeoObservationMatchKind.Matched && join.RouteKey is not null)
                            {
                                runRouteKeys.Add(join.RouteKey);
                            }

                            break;
                        case GenerativeAnswerObservationValidator.ExternalKind:
                            externalRuns[classification.Url] = externalRuns.GetValueOrDefault(classification.Url) + 1;
                            break;
                        default:
                            invalidUrls.TryAdd(
                                SeoObservationUrlNormalizer.SanitizeEvidenceUrl(classification.Url),
                                classification.ErrorCode ?? "invalid_url");
                            break;
                    }
                }

                foreach (var routeKey in runRouteKeys)
                {
                    var key = (row.QuestionKey, routeKey);
                    routeCitations[key] = routeCitations.GetValueOrDefault(key) + 1;
                }
            }
        }

        var questionStats = new List<GenerativeQuestionStats>();
        foreach (var (questionKey, accumulator) in questions.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var routes = routeCitations
                .Where(entry => entry.Key.QuestionKey == questionKey)
                .OrderBy(entry => entry.Key.RouteKey, StringComparer.Ordinal)
                .Select(entry => new GenerativeRouteCitation(
                    entry.Key.RouteKey,
                    routesByKey.TryGetValue(entry.Key.RouteKey, out var route) ? route.Canonical : entry.Key.RouteKey,
                    entry.Value))
                .ToArray();
            questionStats.Add(ToStats(accumulator, questionKey, routes));
        }

        var engineStats = engines
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new GenerativeEngineStats(
                entry.Key,
                entry.Value.Runs,
                entry.Value.BrandMentions,
                Rate(entry.Value.BrandMentions, entry.Value.Runs),
                entry.Value.SiteCitations,
                Rate(entry.Value.SiteCitations, entry.Value.Runs)))
            .ToArray();

        var unmatched = urlJoins
            .Where(entry => entry.Value.Kind == SeoObservationMatchKind.Unmatched)
            .Select(entry => new GenerativeUnmatchedCitedUrl(entry.Key, entry.Value.ErrorCode))
            .Concat(invalidUrls.Select(entry => new GenerativeUnmatchedCitedUrl(entry.Key, entry.Value)))
            .OrderBy(entry => entry.Url, StringComparer.Ordinal)
            .ToArray();

        var ambiguous = urlJoins
            .Where(entry => entry.Value.Kind == SeoObservationMatchKind.Ambiguous)
            .Select(entry => new GenerativeAmbiguousCitedUrl(entry.Key, entry.Value.Candidates))
            .OrderBy(entry => entry.Url, StringComparer.Ordinal)
            .ToArray();

        var external = externalRuns
            .Select(entry => new GenerativeExternalCitedUrl(entry.Key, entry.Value))
            .OrderBy(entry => entry.Url, StringComparer.Ordinal)
            .ToArray();

        var matchedUrls = urlJoins.Count(entry => entry.Value.Kind == SeoObservationMatchKind.Matched);
        var unmatchedUrls = urlJoins.Count(entry => entry.Value.Kind == SeoObservationMatchKind.Unmatched);
        var ambiguousUrls = urlJoins.Count(entry => entry.Value.Kind == SeoObservationMatchKind.Ambiguous);

        return new GenerativeCitationReport(
            Schema,
            SchemaVersion,
            generatedAt,
            sources,
            new GenerativeStats(
                overall.Runs,
                overall.BrandMentions,
                Rate(overall.BrandMentions, overall.Runs),
                overall.SiteCitations,
                Rate(overall.SiteCitations, overall.Runs)),
            engineStats,
            questionStats,
            unmatched,
            ambiguous,
            external,
            new GenerativeJoinQuality(urlJoins.Count, matchedUrls, unmatchedUrls, ambiguousUrls));
    }

    internal static void Write(string outputDir, GenerativeCitationReport report)
    {
        var json = JsonSerializer.Serialize(report, SeoGenerativeInsightsJsonContext.Default.GenerativeCitationReport);
        FileWriter.WriteUtf8(
            outputDir,
            Path.Combine(BuildReporter.ReportDirectoryName, FileName),
            json + Environment.NewLine);
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

    private static StatsAccumulator Add(Dictionary<string, StatsAccumulator> groups, string key)
    {
        if (!groups.TryGetValue(key, out var accumulator))
        {
            accumulator = new StatsAccumulator();
            groups[key] = accumulator;
        }

        return accumulator;
    }

    private static GenerativeQuestionStats ToStats(
        StatsAccumulator accumulator,
        string questionKey,
        IReadOnlyList<GenerativeRouteCitation> routes)
        => new(
            questionKey,
            accumulator.Runs,
            accumulator.BrandMentions,
            Rate(accumulator.BrandMentions, accumulator.Runs),
            accumulator.SiteCitations,
            Rate(accumulator.SiteCitations, accumulator.Runs),
            routes);

    private static double? Rate(long numerator, long denominator)
        => denominator == 0 ? null : (double)numerator / denominator;

    private static InvalidDataException Invalid(string code, string detail)
        => new($"{code}: {detail}");

    private sealed record UrlJoin(
        SeoObservationMatchKind Kind,
        string? RouteKey,
        IReadOnlyList<string> Candidates,
        string? ErrorCode);

    private sealed class StatsAccumulator
    {
        internal long Runs { get; private set; }
        internal long BrandMentions { get; private set; }
        internal long SiteCitations { get; private set; }

        internal void Add(GenerativeAnswerObservationRow row)
        {
            Runs++;
            if (row.BrandMentioned)
            {
                BrandMentions++;
            }

            if (row.SiteCited)
            {
                SiteCitations++;
            }
        }
    }
}
