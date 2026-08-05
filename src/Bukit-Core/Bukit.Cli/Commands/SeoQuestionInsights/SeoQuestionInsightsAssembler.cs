using Bukit.Cli.Commands.SeoInsights;
using Bukit.Engine;

namespace Bukit.Cli.Commands.SeoQuestionInsights;

internal static class SeoQuestionInsightsAssembler
{
    internal const string Schema = "https://bukit.dev/schemas/seo-question-insights-report.v1.json";
    internal const string SchemaVersion = "1.0";

    internal static SeoQuestionInsightsReport Assemble(
        string routeMapPath,
        SeoQuestionTargetMap targets,
        IReadOnlyList<(string Path, SearchQuestionObservationDataset Dataset)> observations,
        SeoObservationUrlOptions options,
        DateTimeOffset generatedAt)
    {
        var routeMap = SeoRouteMapReader.Read(routeMapPath);
        var matcher = new SeoObservationRouteMatcher(routeMap, options);
        return Assemble(routeMap, targets, observations, matcher, generatedAt);
    }

    internal static SeoQuestionInsightsReport Assemble(
        SeoRouteMap routeMap,
        SeoQuestionTargetMap targets,
        IReadOnlyList<(string Path, SearchQuestionObservationDataset Dataset)> observations,
        SeoObservationRouteMatcher matcher,
        DateTimeOffset generatedAt)
    {
        if (observations.Count == 0)
        {
            throw Invalid("question_insights.observations_required", "At least one question observation dataset is required.");
        }

        var window = observations[0].Dataset.Window;
        foreach (var (_, dataset) in observations)
        {
            if (!string.Equals(dataset.Provider, SearchQuestionObservationReader.Provider, StringComparison.Ordinal))
            {
                throw Invalid("question_insights.provider_mismatch", "Question observation provider is not supported by v1.");
            }

            if (dataset.Window.StartDate != window.StartDate ||
                dataset.Window.EndDate != window.EndDate ||
                !string.Equals(dataset.Window.TimeZone, window.TimeZone, StringComparison.Ordinal))
            {
                throw Invalid("question_insights.window_mismatch", "All question observation datasets must share one window.");
            }
        }

        var routesByKey = routeMap.Routes.ToDictionary(route => route.RouteKey, StringComparer.Ordinal);

        var unmatchedTargets = new List<SeoQuestionUnmatchedTarget>();
        long targetSourceRows = 0;
        long targetMatchedRows = 0;
        foreach (var target in targets.Questions)
        {
            foreach (var routeKey in target.CoveredRouteKeys)
            {
                targetSourceRows++;
                if (routesByKey.ContainsKey(routeKey))
                {
                    targetMatchedRows++;
                }
                else
                {
                    unmatchedTargets.Add(new SeoQuestionUnmatchedTarget(target.QuestionKey, routeKey, "route_key_not_found"));
                }
            }
        }

        var aggregations = new Dictionary<(string QuestionKey, string RouteKey), RouteAggregation>();
        var unmatchedObservations = new List<SeoQuestionUnmatchedObservation>();
        var ambiguousObservations = new List<SeoQuestionAmbiguousObservation>();
        long observationSourceRows = 0;
        long observationMatchedRows = 0;
        long observationUnmatchedRows = 0;
        long observationAmbiguousRows = 0;

        foreach (var (_, dataset) in observations)
        {
            foreach (var row in dataset.Rows)
            {
                observationSourceRows++;
                var match = matcher.Match(row.Url);
                switch (match.Kind)
                {
                    case SeoObservationMatchKind.Matched:
                        observationMatchedRows++;
                        var key = (row.QuestionKey, match.RouteKey!);
                        if (!aggregations.TryGetValue(key, out var aggregation))
                        {
                            aggregation = new RouteAggregation();
                            aggregations[key] = aggregation;
                        }

                        aggregation.Impressions += row.Impressions;
                        aggregation.Clicks += row.Clicks;
                        aggregation.PositionWeightedSum += row.AveragePosition * row.Impressions;
                        aggregation.PositionSampleSum += row.AveragePosition;
                        aggregation.PositionSamples++;
                        break;
                    case SeoObservationMatchKind.Ambiguous:
                        observationAmbiguousRows++;
                        ambiguousObservations.Add(new SeoQuestionAmbiguousObservation(
                            row.QuestionKey,
                            SeoObservationUrlNormalizer.SanitizeEvidenceUrl(row.Url),
                            match.Candidates.Select(candidate => candidate.RouteKey).ToArray()));
                        break;
                    default:
                        observationUnmatchedRows++;
                        unmatchedObservations.Add(new SeoQuestionUnmatchedObservation(
                            row.QuestionKey,
                            SeoObservationUrlNormalizer.SanitizeEvidenceUrl(row.Url),
                            match.ErrorCode));
                        break;
                }
            }
        }

        var questionsByCoverage = new Dictionary<string, List<SeoQuestionRouteCoverage>>(StringComparer.Ordinal);
        foreach (var ((questionKey, routeKey), aggregation) in aggregations)
        {
            if (!routesByKey.TryGetValue(routeKey, out var route))
            {
                continue;
            }

            if (!questionsByCoverage.TryGetValue(questionKey, out var routes))
            {
                routes = [];
                questionsByCoverage[questionKey] = routes;
            }

            var averagePosition = aggregation.Impressions > 0
                ? aggregation.PositionWeightedSum / aggregation.Impressions
                : aggregation.PositionSamples > 0 ? aggregation.PositionSampleSum / aggregation.PositionSamples : 0;
            routes.Add(new SeoQuestionRouteCoverage(
                routeKey,
                route.Canonical,
                aggregation.Impressions,
                aggregation.Clicks,
                aggregation.Impressions > 0 ? (double?)aggregation.Clicks / aggregation.Impressions : null,
                averagePosition));
        }

        var questions = new List<SeoQuestionCoverage>();
        foreach (var target in targets.Questions.OrderBy(question => question.QuestionKey, StringComparer.Ordinal))
        {
            var routes = questionsByCoverage.TryGetValue(target.QuestionKey, out var covered)
                ? covered.OrderBy(route => route.RouteKey, StringComparer.Ordinal).ToArray()
                : Array.Empty<SeoQuestionRouteCoverage>();
            questions.Add(new SeoQuestionCoverage(
                target.QuestionKey,
                target.TopicKey,
                target.Intent,
                target.Locale,
                target.Priority,
                routes.Sum(route => route.Impressions),
                routes.Sum(route => route.Clicks),
                routes));
        }

        var targetCounts = new SeoQuestionJoinCounts(
            targetSourceRows,
            targetMatchedRows,
            targetSourceRows - targetMatchedRows,
            0);
        var observationCounts = new SeoQuestionJoinCounts(
            observationSourceRows,
            observationMatchedRows,
            observationUnmatchedRows,
            observationAmbiguousRows);
        var overallCounts = new SeoQuestionJoinCounts(
            targetSourceRows + observationSourceRows,
            targetMatchedRows + observationMatchedRows,
            targetSourceRows - targetMatchedRows + observationUnmatchedRows,
            observationAmbiguousRows);

        return new SeoQuestionInsightsReport(
            Schema,
            SchemaVersion,
            generatedAt,
            window,
            observations
                .Select(entry => new SeoQuestionInsightsSource(
                    entry.Dataset.Provider,
                    entry.Dataset.Scope,
                    entry.Dataset.CollectionMethod,
                    entry.Dataset.CollectedAt,
                    entry.Path))
                .ToArray(),
            new SeoQuestionJoinQuality(overallCounts, targetCounts, observationCounts),
            questions,
            unmatchedTargets
                .OrderBy(target => target.QuestionKey, StringComparer.Ordinal)
                .ThenBy(target => target.RouteKey, StringComparer.Ordinal)
                .ToArray(),
            unmatchedObservations
                .OrderBy(observation => observation.QuestionKey, StringComparer.Ordinal)
                .ThenBy(observation => observation.Url, StringComparer.Ordinal)
                .ThenBy(observation => observation.ErrorCode ?? string.Empty, StringComparer.Ordinal)
                .ToArray(),
            ambiguousObservations
                .OrderBy(observation => observation.QuestionKey, StringComparer.Ordinal)
                .ThenBy(observation => observation.Url, StringComparer.Ordinal)
                .ToArray());
    }

    private static InvalidDataException Invalid(string code, string detail)
        => new($"{code}: {detail}");

    private sealed class RouteAggregation
    {
        internal long Impressions { get; set; }
        internal long Clicks { get; set; }
        internal double PositionWeightedSum { get; set; }
        internal double PositionSampleSum { get; set; }
        internal long PositionSamples { get; set; }
    }
}
