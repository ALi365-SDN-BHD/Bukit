using System.Collections;
using System.Diagnostics;
using Bukit.Engine.Abstractions.Plugins;

namespace Bukit.Engine.Plugins;

internal sealed class TrackedHtmlTransform : IHtmlTransform
{
    private readonly IHtmlTransform _inner;
    private readonly bool _warnOnFailure;
    private readonly BuildContext _buildContext;
    private long _invocationCount;
    private long _elapsedTimestampTicks;
    private string? _firstError;

    internal TrackedHtmlTransform(
        string pluginName,
        IHtmlTransform inner,
        bool warnOnFailure,
        BuildContext buildContext)
    {
        Name = pluginName;
        _inner = inner;
        _warnOnFailure = warnOnFailure;
        _buildContext = buildContext;
    }

    public string Name { get; }

    internal long InvocationCount => Interlocked.Read(ref _invocationCount);

    internal long ElapsedTimestampTicks => Interlocked.Read(ref _elapsedTimestampTicks);

    public string Transform(HtmlTransformContext context, string html)
    {
        Interlocked.Increment(ref _invocationCount);
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            return _inner.Transform(context, html);
        }
        catch (Exception exception)
        {
            var isFirstFailure = Interlocked.CompareExchange(
                ref _firstError,
                exception.Message,
                null) is null;
            if (isFirstFailure)
            {
                if (_warnOnFailure)
                {
                    _buildContext.Logger.Warn($"plugin {Name} {HtmlTransformHooks.HtmlTransform} failed: {exception.Message}");
                }
                else
                {
                    _buildContext.Logger.Error($"plugin {Name} {HtmlTransformHooks.HtmlTransform} failed: {exception.Message}");
                }
            }

            if (!_warnOnFailure)
            {
                throw;
            }

            return html;
        }
        finally
        {
            Interlocked.Add(ref _elapsedTimestampTicks, Stopwatch.GetTimestamp() - startedAt);
        }
    }

    internal PluginExecutionInfo CreateExecutionInfo()
    {
        var firstError = Volatile.Read(ref _firstError);
        var durationMs = ElapsedTimestampTicks == 0
            ? 0
            : (long)(ElapsedTimestampTicks * 1000d / Stopwatch.Frequency);
        return new PluginExecutionInfo(
            Name,
            HtmlTransformHooks.HtmlTransform,
            durationMs,
            firstError is null,
            firstError);
    }
}

internal sealed class CollectedHtmlTransforms : IReadOnlyList<IHtmlTransform>
{
    private readonly BuildContext _buildContext;
    private readonly IReadOnlyList<TrackedHtmlTransform> _transforms;
    private readonly object _recordExecutionsLock = new();
    private bool _executionsRecorded;

    internal CollectedHtmlTransforms(
        BuildContext buildContext,
        IReadOnlyList<TrackedHtmlTransform> transforms)
    {
        _buildContext = buildContext;
        _transforms = transforms;
    }

    public int Count => _transforms.Count;

    public IHtmlTransform this[int index] => _transforms[index];

    public IEnumerator<IHtmlTransform> GetEnumerator()
        => _transforms.Cast<IHtmlTransform>().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal void RecordExecutions()
    {
        lock (_recordExecutionsLock)
        {
            if (_executionsRecorded)
            {
                return;
            }

            foreach (var transform in _transforms)
            {
                _buildContext.PluginExecutions.Add(transform.CreateExecutionInfo());
            }

            _executionsRecorded = true;
        }
    }
}
