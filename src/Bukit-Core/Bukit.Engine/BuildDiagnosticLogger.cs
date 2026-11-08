using Bukit.Shared;

namespace Bukit.Engine;

internal sealed class BuildDiagnosticLogger : ILogger
{
    private sealed class Counters
    {
        internal int WarningCount;
        internal int ErrorCount;
        internal readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> WarningsByKind = new(StringComparer.Ordinal);
    }

    /// <summary>Repeated warnings of one kind are shown this many times, then summarized.</summary>
    internal const int MaxWarningsPerKind = 5;

    private readonly ILogger _inner;
    private readonly Counters _counters;

    internal BuildDiagnosticLogger(ILogger inner)
        : this(inner, new Counters())
    {
    }

    private BuildDiagnosticLogger(ILogger inner, Counters counters)
    {
        _inner = inner;
        _counters = counters;
    }

    internal int WarningCount => Volatile.Read(ref _counters.WarningCount);
    internal int ErrorCount => Volatile.Read(ref _counters.ErrorCount);

    internal BuildDiagnosticLogger ForwardTo(ILogger inner) => new(inner, _counters);

    public void Debug(string message) => _inner.Debug(message);

    public void Info(string message) => _inner.Info(message);

    public void Warn(string message)
    {
        Interlocked.Increment(ref _counters.WarningCount);
        var kind = WarningKind(message);
        if (kind is not null && _counters.WarningsByKind.AddOrUpdate(kind, 1, static (_, n) => n + 1) > MaxWarningsPerKind)
        {
            return;
        }

        _inner.Warn(message);
    }

    /// <summary>
    /// Logs one summary line per warning kind that exceeded <see cref="MaxWarningsPerKind"/>,
    /// so large sites do not print thousands of identical lines. Counts are not reset.
    /// </summary>
    internal void FlushWarningSummary()
    {
        foreach (var (kind, total) in _counters.WarningsByKind.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (total > MaxWarningsPerKind)
            {
                _inner.Warn($"event=diagnostics.summary kind={kind} total={total} shown={MaxWarningsPerKind} suppressed={total - MaxWarningsPerKind}");
            }
        }
    }

    // "event=x code=y ..." groups by event+code; "seo.some_rule route=..." by its leading rule id.
    // Free-form messages return null and are never suppressed.
    internal static string? WarningKind(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return null;
        }

        var first = FirstToken(message, 0);
        if (first.StartsWith("event=", StringComparison.Ordinal))
        {
            var kind = first["event=".Length..];
            var codeIndex = message.IndexOf(" code=", StringComparison.Ordinal);
            return codeIndex < 0 ? kind : kind + ":" + FirstToken(message, codeIndex + " code=".Length);
        }

        if (!IsRuleId(first))
        {
            return null;
        }

        // Audit lines ("publish.audit severity=… code=…") group per code, not per prefix.
        var ruleCodeIndex = message.IndexOf(" code=", StringComparison.Ordinal);
        return ruleCodeIndex < 0 ? first : first + ":" + FirstToken(message, ruleCodeIndex + " code=".Length);
    }

    private static string FirstToken(string message, int start)
    {
        var end = message.IndexOf(' ', start);
        return end < 0 ? message[start..] : message[start..end];
    }

    private static bool IsRuleId(string token)
    {
        if (token.Length == 0 || !token.Contains('.') || token[0] == '.' || token[^1] == '.')
        {
            return false;
        }

        foreach (var c in token)
        {
            if (!(c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '.'))
            {
                return false;
            }
        }

        return true;
    }

    public void Error(string message)
    {
        Interlocked.Increment(ref _counters.ErrorCount);
        _inner.Error(message);
    }
}
