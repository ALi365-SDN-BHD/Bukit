using Bukit.Shared;

namespace Bukit.Engine;

internal sealed class BuildDiagnosticLogger : ILogger
{
    private sealed class Counters
    {
        internal int WarningCount;
        internal int ErrorCount;
    }

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
        _inner.Warn(message);
    }

    public void Error(string message)
    {
        Interlocked.Increment(ref _counters.ErrorCount);
        _inner.Error(message);
    }
}
