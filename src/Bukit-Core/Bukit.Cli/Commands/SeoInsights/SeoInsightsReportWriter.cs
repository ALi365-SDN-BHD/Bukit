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
        => AssembleCore(matcher, datasets, ruleProfile: null);

    internal static SeoInsightsReport Assemble(
        SeoObservationRouteMatcher matcher,
        IReadOnlyList<SeoObservationDataset> datasets,
        SeoInsightsRuleProfile ruleProfile)
    {
        ArgumentNullException.ThrowIfNull(ruleProfile);
        return AssembleCore(matcher, datasets, ruleProfile);
    }

    private static SeoInsightsReport AssembleCore(
        SeoObservationRouteMatcher matcher,
        IReadOnlyList<SeoObservationDataset> datasets,
        SeoInsightsRuleProfile? ruleProfile)
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
                            SafeEvidenceOriginalUrl(row.Url),
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
                dataset.CollectedAt.ToUniversalTime(),
                dataset.Rows.Count))
            .OrderBy(source => source.Provider, StringComparer.Ordinal)
            .ThenBy(source => source.CollectedAt)
            .ThenBy(source => source.Scope, StringComparer.Ordinal)
            .ThenBy(source => source.RowCount)
            .ToArray();
        var routes = accumulators.Values
            .Select(accumulator => accumulator.Build(ruleProfile))
            .OrderBy(route => route.Canonical, StringComparer.Ordinal)
            .ThenBy(route => route.RouteKey, StringComparer.Ordinal)
            .ToArray();
        // These comparers cover every serialized discriminator, including
        // metrics and candidates, so evidence has a total input-independent order.
        var unmatchedEvidence = unmatched
            .OrderBy(value => value, UnmatchedEvidenceComparer.Instance)
            .ToArray();
        var ambiguousEvidence = ambiguous
            .OrderBy(value => value, AmbiguousEvidenceComparer.Instance)
            .ToArray();

        return new SeoInsightsReport(
            Schema,
            SchemaVersion,
            datasets.Max(dataset => dataset.CollectedAt).ToUniversalTime(),
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

    private static string SafeEvidenceOriginalUrl(string value)
    {
        if (!HasCredentialAuthority(value))
        {
            return value;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.UserInfo))
        {
            return "[redacted:unsafe_url]";
        }

        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty
        };
        return builder.Uri.AbsoluteUri;
    }

    private static bool HasCredentialAuthority(string value)
    {
        var schemeSeparator = value.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparator < 0)
        {
            return false;
        }

        var authority = value.AsSpan(schemeSeparator + 3);
        var authorityEnd = authority.IndexOfAny('/', '?', '#');
        if (authorityEnd >= 0)
        {
            authority = authority[..authorityEnd];
        }

        return authority.Contains('@');
    }

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
        private readonly List<PositionSample> _positionSamples = [];

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

        internal SeoInsightsRoute Build(SeoInsightsRuleProfile? ruleProfile)
        {
            double? averagePosition;
            try
            {
                averagePosition = AveragePosition();
            }
            catch (OverflowException exception)
            {
                throw Invalid("report.numeric_overflow", "Route metric aggregation overflowed.", exception);
            }

            var route = new SeoInsightsRoute(
                _candidate.RouteKey,
                _candidate.ContentKey,
                _candidate.Route,
                _candidate.Canonical,
                new SeoObservationMetrics(
                    _impressions,
                    _clicks,
                    averagePosition,
                    Divide(_clicks, _impressions),
                    _sessions,
                    _engagedSessions,
                    _keyEvents,
                    Divide(_engagedSessions, _sessions),
                    Divide(_keyEvents, _sessions)),
                Array.Empty<SeoInsightsFinding>());
            return ruleProfile is null
                ? route
                : route with { Findings = SeoInsightsRuleEvaluator.Evaluate(route, ruleProfile) };
        }

        private void AddPosition(double? position, long? impressions)
        {
            if (position is null || impressions is null || impressions == 0)
            {
                return;
            }

            if (!double.IsFinite(position.Value))
            {
                throw new OverflowException("Position sample is not finite.");
            }

            _positionSamples.Add(new PositionSample(position.Value, impressions.Value));
        }

        private double? AveragePosition()
        {
            if (_positionSamples.Count == 0)
            {
                return null;
            }

            // This is a total order over every serialized position discriminator,
            // so aggregation and threshold evidence do not depend on input order.
            var samples = _positionSamples
                .OrderBy(sample => BitConverter.DoubleToInt64Bits(sample.Position))
                .ThenBy(sample => sample.Impressions)
                .ToArray();
            var totalImpressions = 0L;
            foreach (var sample in samples)
            {
                totalImpressions = checked(totalImpressions + sample.Impressions);
            }

            if (!CanUseDecimalAverage(samples))
            {
                return ScaledDoubleAverage(samples, totalImpressions);
            }

            try
            {
                return DecimalAverage(samples, totalImpressions);
            }
            catch (OverflowException)
            {
                return ScaledDoubleAverage(samples, totalImpressions);
            }
        }

        private static bool CanUseDecimalAverage(IReadOnlyList<PositionSample> samples)
        {
            foreach (var sample in samples)
            {
                try
                {
                    var decimalPosition = (decimal)sample.Position;
                    if ((sample.Position != 0 && decimalPosition == 0)
                        || (double)decimalPosition != sample.Position)
                    {
                        return false;
                    }
                }
                catch (OverflowException)
                {
                    return false;
                }
            }

            return true;
        }

        private static double DecimalAverage(IReadOnlyList<PositionSample> samples, long totalImpressions)
        {
            decimal weightedPositions = 0;
            foreach (var sample in samples)
            {
                weightedPositions = checked(weightedPositions + checked((decimal)sample.Position * sample.Impressions));
            }

            var average = (double)(weightedPositions / totalImpressions);
            if (!double.IsFinite(average))
            {
                throw new OverflowException("Weighted position average is not finite.");
            }

            return average;
        }

        private static double ScaledDoubleAverage(IReadOnlyList<PositionSample> samples, long totalImpressions)
        {
            var maximumPosition = samples.Max(sample => sample.Position);
            if (maximumPosition == 0)
            {
                return 0;
            }

            var normalizedWeightedPositions = 0d;
            foreach (var sample in samples)
            {
                var normalizedContribution =
                    (sample.Position / maximumPosition) * ((double)sample.Impressions / totalImpressions);
                if (!double.IsFinite(normalizedContribution))
                {
                    throw new OverflowException("Scaled weighted position contribution is not finite.");
                }

                normalizedWeightedPositions += normalizedContribution;
                if (!double.IsFinite(normalizedWeightedPositions))
                {
                    throw new OverflowException("Scaled weighted position is not finite.");
                }
            }

            // Round-off can put a mathematical unit weight infinitesimally above one.
            var average = maximumPosition * Math.Min(1d, normalizedWeightedPositions);
            if (!double.IsFinite(average))
            {
                throw new OverflowException("Scaled weighted position average is not finite.");
            }

            return average;
        }

        private static long? AddNullable(long? total, long? value)
            => value is null ? total : checked((total ?? 0) + value.Value);

        private readonly record struct PositionSample(double Position, long Impressions);
    }

    private sealed class UnmatchedEvidenceComparer : IComparer<SeoUnmatchedObservation>
    {
        internal static readonly UnmatchedEvidenceComparer Instance = new();

        public int Compare(SeoUnmatchedObservation? left, SeoUnmatchedObservation? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return -1;
            if (right is null) return 1;
            return CompareUnmatched(left, right);
        }
    }

    private sealed class AmbiguousEvidenceComparer : IComparer<SeoAmbiguousObservation>
    {
        internal static readonly AmbiguousEvidenceComparer Instance = new();

        public int Compare(SeoAmbiguousObservation? left, SeoAmbiguousObservation? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return -1;
            if (right is null) return 1;
            return CompareAmbiguous(left, right);
        }
    }

    private static int CompareUnmatched(SeoUnmatchedObservation left, SeoUnmatchedObservation right)
    {
        var comparison = CompareEvidencePrefix(left.Provider, left.Scope, left.NormalizedUrl, left.OriginalUrl, left.ErrorCode,
            right.Provider, right.Scope, right.NormalizedUrl, right.OriginalUrl, right.ErrorCode);
        return comparison != 0 ? comparison : CompareMetrics(left.Metrics, right.Metrics);
    }

    private static int CompareAmbiguous(SeoAmbiguousObservation left, SeoAmbiguousObservation right)
    {
        var comparison = CompareEvidencePrefix(left.Provider, left.Scope, left.NormalizedUrl, left.OriginalUrl, null,
            right.Provider, right.Scope, right.NormalizedUrl, right.OriginalUrl, null);
        if (comparison != 0) return comparison;
        comparison = CompareMetrics(left.Metrics, right.Metrics);
        return comparison != 0 ? comparison : CompareCandidates(left.Candidates, right.Candidates);
    }

    private static int CompareEvidencePrefix(string provider, string scope, string? normalizedUrl, string originalUrl, string? errorCode,
        string otherProvider, string otherScope, string? otherNormalizedUrl, string otherOriginalUrl, string? otherErrorCode)
    {
        var comparison = StringComparer.Ordinal.Compare(provider, otherProvider);
        if (comparison != 0) return comparison;
        comparison = StringComparer.Ordinal.Compare(scope, otherScope);
        if (comparison != 0) return comparison;
        comparison = CompareNullableString(normalizedUrl, otherNormalizedUrl);
        if (comparison != 0) return comparison;
        comparison = StringComparer.Ordinal.Compare(originalUrl, otherOriginalUrl);
        if (comparison != 0) return comparison;
        return CompareNullableString(errorCode, otherErrorCode);
    }

    private static int CompareMetrics(SeoObservationMetrics left, SeoObservationMetrics right)
    {
        var comparison = CompareNullableLong(left.Impressions, right.Impressions);
        if (comparison != 0) return comparison;
        comparison = CompareNullableLong(left.Clicks, right.Clicks);
        if (comparison != 0) return comparison;
        comparison = CompareNullableDouble(left.AveragePosition, right.AveragePosition);
        if (comparison != 0) return comparison;
        comparison = CompareNullableDouble(left.Ctr, right.Ctr);
        if (comparison != 0) return comparison;
        comparison = CompareNullableLong(left.Sessions, right.Sessions);
        if (comparison != 0) return comparison;
        comparison = CompareNullableLong(left.EngagedSessions, right.EngagedSessions);
        if (comparison != 0) return comparison;
        comparison = CompareNullableLong(left.KeyEvents, right.KeyEvents);
        if (comparison != 0) return comparison;
        comparison = CompareNullableDouble(left.EngagementRate, right.EngagementRate);
        return comparison != 0 ? comparison : CompareNullableDouble(left.KeyEventRate, right.KeyEventRate);
    }

    private static int CompareCandidates(IReadOnlyList<SeoObservationRouteCandidate> left, IReadOnlyList<SeoObservationRouteCandidate> right)
    {
        for (var index = 0; index < Math.Min(left.Count, right.Count); index++)
        {
            var comparison = StringComparer.Ordinal.Compare(left[index].RouteKey, right[index].RouteKey);
            if (comparison != 0) return comparison;
            comparison = CompareNullableString(left[index].ContentKey, right[index].ContentKey);
            if (comparison != 0) return comparison;
            comparison = StringComparer.Ordinal.Compare(left[index].Route, right[index].Route);
            if (comparison != 0) return comparison;
            comparison = StringComparer.Ordinal.Compare(left[index].Canonical, right[index].Canonical);
            if (comparison != 0) return comparison;
        }

        return left.Count.CompareTo(right.Count);
    }

    private static int CompareNullableString(string? left, string? right)
        => left is null ? right is null ? 0 : -1 : right is null ? 1 : StringComparer.Ordinal.Compare(left, right);

    private static int CompareNullableLong(long? left, long? right)
        => left is null ? right is null ? 0 : -1 : right is null ? 1 : left.Value.CompareTo(right.Value);

    private static int CompareNullableDouble(double? left, double? right)
        => left is null ? right is null ? 0 : -1 : right is null ? 1 :
            BitConverter.DoubleToInt64Bits(left.Value).CompareTo(BitConverter.DoubleToInt64Bits(right.Value));
}
