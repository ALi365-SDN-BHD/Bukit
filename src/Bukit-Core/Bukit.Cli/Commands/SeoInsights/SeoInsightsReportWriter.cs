using System.Text.Json;
using Bukit.Engine;

namespace Bukit.Cli.Commands.SeoInsights;

internal static class SeoInsightsReportWriter
{
    internal const string Schema = "https://bukit.dev/schemas/seo-insights-report.v1.json";
    internal const string SchemaVersion = "1.0";
    internal const string FileName = "seo-insights-report.json";

    internal static SeoInsightsReport Assemble(
        SeoObservationRouteMatcher matcher,
        IReadOnlyList<SeoObservationDataset> datasets)
    {
        if (datasets.Count == 0)
        {
            throw Invalid("report.dataset_required", "At least one observation dataset is required.");
        }

        var window = datasets[0].Window;
        if (datasets.Any(dataset => dataset.Window != window))
        {
            throw Invalid("report.window_mismatch", "All v1 observation datasets must use the exact same window.");
        }

        var accumulators = new Dictionary<string, RouteAccumulator>(StringComparer.Ordinal);
        var providerCounts = new Dictionary<string, MutableCounts>(StringComparer.Ordinal);
        var overall = new MutableCounts();
        var unmatched = new List<SeoUnmatchedObservation>();
        var ambiguous = new List<SeoAmbiguousObservation>();

        foreach (var dataset in datasets)
        {
            if (!providerCounts.TryGetValue(dataset.Provider, out var counts))
            {
                counts = new MutableCounts();
                providerCounts.Add(dataset.Provider, counts);
            }

            foreach (var row in dataset.Rows)
            {
                AddCount(overall, counts, MatchCategory.Total);
                var match = matcher.Match(row.Url);
                switch (match.Kind)
                {
                    case SeoObservationMatchKind.Matched:
                        AddCount(overall, counts, MatchCategory.Matched);
                        var candidate = AssertSingleCandidate(match);
                        if (!accumulators.TryGetValue(candidate.RouteKey, out var accumulator))
                        {
                            accumulator = new RouteAccumulator(candidate);
                            accumulators.Add(candidate.RouteKey, accumulator);
                        }

                        accumulator.Add(row);
                        break;
                    case SeoObservationMatchKind.Unmatched:
                        AddCount(overall, counts, MatchCategory.Unmatched);
                        unmatched.Add(new SeoUnmatchedObservation(
                            dataset.Provider,
                            dataset.Scope,
                            row.Url,
                            match.NormalizedUrl,
                            match.ErrorCode,
                            EvidenceMetrics(row)));
                        break;
                    case SeoObservationMatchKind.Ambiguous:
                        AddCount(overall, counts, MatchCategory.Ambiguous);
                        ambiguous.Add(new SeoAmbiguousObservation(
                            dataset.Provider,
                            dataset.Scope,
                            row.Url,
                            match.NormalizedUrl!,
                            EvidenceMetrics(row),
                            match.Candidates));
                        break;
                    default:
                        throw Invalid("report.match_invalid", "Observation matcher returned an unsupported result.");
                }
            }
        }

        var sources = datasets
            .Select(dataset => new SeoInsightsSource(
                dataset.Provider,
                dataset.Scope,
                dataset.CollectedAt,
                dataset.Rows.Count))
            .OrderBy(source => source.Provider, StringComparer.Ordinal)
            .ThenBy(source => source.CollectedAt)
            .ThenBy(source => source.Scope, StringComparer.Ordinal)
            .ToArray();
        var routes = accumulators.Values
            .Select(accumulator => accumulator.Build())
            .OrderBy(route => route.Canonical, StringComparer.Ordinal)
            .ThenBy(route => route.RouteKey, StringComparer.Ordinal)
            .ToArray();
        var unmatchedEvidence = unmatched
            .OrderBy(value => value.Provider, StringComparer.Ordinal)
            .ThenBy(value => value.NormalizedUrl, StringComparer.Ordinal)
            .ThenBy(value => value.OriginalUrl, StringComparer.Ordinal)
            .ToArray();
        var ambiguousEvidence = ambiguous
            .OrderBy(value => value.Provider, StringComparer.Ordinal)
            .ThenBy(value => value.NormalizedUrl, StringComparer.Ordinal)
            .ThenBy(value => value.OriginalUrl, StringComparer.Ordinal)
            .ToArray();

        return new SeoInsightsReport(
            Schema,
            SchemaVersion,
            datasets.Max(dataset => dataset.CollectedAt),
            window,
            sources,
            new SeoJoinQuality(
                overall.Snapshot(),
                providerCounts
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new SeoProviderJoinQuality(pair.Key, pair.Value.Snapshot()))
                    .ToArray()),
            routes,
            unmatchedEvidence,
            ambiguousEvidence);
    }

    internal static void Write(string outputDir, SeoInsightsReport report)
    {
        var json = JsonSerializer.Serialize(report, SeoInsightsJsonContext.Default.SeoInsightsReport);
        FileWriter.WriteUtf8(
            outputDir,
            Path.Combine(BuildReporter.ReportDirectoryName, FileName),
            json + Environment.NewLine);
    }

    private static SeoObservationRouteCandidate AssertSingleCandidate(SeoObservationRouteMatch match)
    {
        if (match.RouteKey is null || match.Candidates.Count != 1)
        {
            throw Invalid("report.match_invalid", "Matched observation did not contain one route candidate.");
        }

        return match.Candidates[0];
    }

    private static SeoObservationMetrics EvidenceMetrics(SeoObservationRow row)
        => new(
            row.Impressions,
            row.Clicks,
            row.AveragePosition,
            Divide(row.Clicks, row.Impressions),
            row.Sessions,
            row.EngagedSessions,
            row.KeyEvents,
            Divide(row.EngagedSessions, row.Sessions),
            Divide(row.KeyEvents, row.Sessions));

    private static double? Divide(long? numerator, long? denominator)
        => numerator is null || denominator is null || denominator == 0
            ? null
            : (double)numerator.Value / denominator.Value;

    private static void AddCount(MutableCounts overall, MutableCounts provider, MatchCategory category)
    {
        try
        {
            overall.Add(category);
            provider.Add(category);
        }
        catch (OverflowException exception)
        {
            throw Invalid("report.numeric_overflow", "Join-quality count overflowed.", exception);
        }
    }

    private static InvalidDataException Invalid(string code, string detail, Exception? inner = null)
        => new($"{code}: {detail}", inner);

    private enum MatchCategory
    {
        Total,
        Matched,
        Unmatched,
        Ambiguous
    }

    private sealed class MutableCounts
    {
        private long _total;
        private long _matched;
        private long _unmatched;
        private long _ambiguous;

        internal void Add(MatchCategory category)
        {
            checked
            {
                switch (category)
                {
                    case MatchCategory.Total: _total++; break;
                    case MatchCategory.Matched: _matched++; break;
                    case MatchCategory.Unmatched: _unmatched++; break;
                    case MatchCategory.Ambiguous: _ambiguous++; break;
                }
            }
        }

        internal SeoJoinCounts Snapshot() => new(_total, _matched, _unmatched, _ambiguous);
    }

    private sealed class RouteAccumulator
    {
        private readonly SeoObservationRouteCandidate _candidate;
        private long? _impressions;
        private long? _clicks;
        private long? _sessions;
        private long? _engagedSessions;
        private long? _keyEvents;
        private long _positionImpressions;
        private double? _weightedPosition;

        internal RouteAccumulator(SeoObservationRouteCandidate candidate) => _candidate = candidate;

        internal void Add(SeoObservationRow row)
        {
            try
            {
                _impressions = AddNullable(_impressions, row.Impressions);
                _clicks = AddNullable(_clicks, row.Clicks);
                _sessions = AddNullable(_sessions, row.Sessions);
                _engagedSessions = AddNullable(_engagedSessions, row.EngagedSessions);
                _keyEvents = AddNullable(_keyEvents, row.KeyEvents);
                AddPosition(row.AveragePosition, row.Impressions);
            }
            catch (OverflowException exception)
            {
                throw Invalid("report.numeric_overflow", "Route metric aggregation overflowed.", exception);
            }
        }

        internal SeoInsightsRoute Build()
            => new(
                _candidate.RouteKey,
                _candidate.ContentKey,
                _candidate.Route,
                _candidate.Canonical,
                new SeoObservationMetrics(
                    _impressions,
                    _clicks,
                    _weightedPosition,
                    Divide(_clicks, _impressions),
                    _sessions,
                    _engagedSessions,
                    _keyEvents,
                    Divide(_engagedSessions, _sessions),
                    Divide(_keyEvents, _sessions)));

        private void AddPosition(double? position, long? impressions)
        {
            if (position is null || impressions is null || impressions == 0)
            {
                return;
            }

            var previousImpressions = _positionImpressions;
            var totalImpressions = checked(previousImpressions + impressions.Value);
            _weightedPosition = _weightedPosition is null
                ? position.Value
                : (_weightedPosition.Value * ((double)previousImpressions / totalImpressions)) +
                  (position.Value * ((double)impressions.Value / totalImpressions));
            if (!double.IsFinite(_weightedPosition.Value))
            {
                throw new OverflowException("Weighted position is not finite.");
            }

            _positionImpressions = totalImpressions;
        }

        private static long? AddNullable(long? total, long? value)
            => value is null ? total : checked((total ?? 0) + value.Value);
    }
}
