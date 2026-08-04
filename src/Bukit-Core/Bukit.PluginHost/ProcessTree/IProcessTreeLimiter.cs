using System.Diagnostics;

namespace Bukit.PluginHost.ProcessTree;

/// <summary>
/// Owns one plugin process tree for resource accounting and deterministic termination.
/// Implementations must prove that every descendant participates in the accounting;
/// platforms that cannot prove tree control must fail creation instead of degrading
/// to parent-only limits.
/// </summary>
internal interface IProcessTreeLimiter : IAsyncDisposable
{
    /// <summary>Associates the just-started process with this limiter's tree.</summary>
    void Attach(Process process);

    /// <summary>Samples aggregated CPU time and peak memory across the whole tree.</summary>
    ValueTask<ProcessTreeUsage> SampleAsync(CancellationToken cancellationToken);

    /// <summary>Terminates the entire tree (group or job) and does not return until requested.</summary>
    void Terminate();
}
