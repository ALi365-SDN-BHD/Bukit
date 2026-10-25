using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Engine.Plugins.BuiltIn;

internal sealed class DocumentTraversalState
{
    private readonly int _maxNodes;
    private readonly int _maxDepth;
    private readonly long _maxScalarChars;
    private readonly ProjectionBudgetState _projectionBudget;
    private readonly string _relativePath;
    private readonly CancellationToken _cancellationToken;
    private int _nodeCount;

    internal DocumentTraversalState(
        int maxNodes,
        int maxDepth,
        long maxScalarChars,
        ProjectionBudgetState projectionBudget,
        string relativePath,
        CancellationToken cancellationToken)
    {
        _maxNodes = maxNodes;
        _maxDepth = maxDepth;
        _maxScalarChars = maxScalarChars;
        _projectionBudget = projectionBudget;
        _relativePath = relativePath;
        _cancellationToken = cancellationToken;
    }

    internal void Visit(int depth)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (depth > _maxDepth)
        {
            throw new ConfigException(
                $"Data document depth exceeds the maximum of {_maxDepth} at {_relativePath}.",
                DiagnosticCode.ConfigInvalidValue);
        }

        _nodeCount++;
        if (_nodeCount > _maxNodes)
        {
            throw new ConfigException(
                $"Data document contains more than {_maxNodes} nodes at {_relativePath}.",
                DiagnosticCode.ConfigInvalidValue);
        }
    }

    internal void VisitScalar(long length)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (length > _maxScalarChars)
        {
            throw new ConfigException(
                $"Data document contains a scalar longer than {_maxScalarChars} characters at {_relativePath}.",
                DiagnosticCode.ConfigInvalidValue);
        }
    }

    internal void VisitResultString(int length)
        => _projectionBudget.VisitString(length, _relativePath);

    internal void VisitResultEntry()
        => _projectionBudget.VisitEntry(_relativePath);

    internal string TrackResultString(string text)
    {
        VisitResultString(text.Length);
        return text;
    }
}

internal sealed class ProjectionBudgetState
{
    private readonly long _maxChars;
    private readonly int _maxEntries;
    private readonly CancellationToken _cancellationToken;
    private long _chars;
    private int _entries;

    internal ProjectionBudgetState(
        long maxChars,
        int maxEntries,
        CancellationToken cancellationToken)
    {
        _maxChars = maxChars;
        _maxEntries = maxEntries;
        _cancellationToken = cancellationToken;
    }

    internal void VisitString(int length, string relativePath)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (_chars > _maxChars - length)
        {
            throw new ConfigException(
                $"Data document projects to more than {_maxChars} characters at {relativePath}.",
                DiagnosticCode.ConfigInvalidValue);
        }

        _chars += length;
    }

    internal void VisitEntry(string relativePath)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (_entries >= _maxEntries)
        {
            throw new ConfigException(
                $"Data document projects to more than {_maxEntries} collection entries at {relativePath}.",
                DiagnosticCode.ConfigInvalidValue);
        }

        _entries++;
    }

    internal void VisitMapEntry(string key, string relativePath)
    {
        VisitEntry(relativePath);
        VisitString(key.Length, relativePath);
    }
}
