using System.Diagnostics;

namespace Bukit.PluginHost.ProcessTree;

/// <summary>
/// Owns one plugin process tree for resource accounting and deterministic termination.
/// Unix implementations prove that every descendant participates from launch (process
/// group created before the tool executes). The Windows job-object implementation
/// guarantees participation only from <see cref="Attach"/> onward: a child spawned in
/// the window between process start and job assignment may escape the job, so Windows
/// containment is documented as best-effort rather than a hard race-free guarantee.
/// Platforms that cannot provide any tree control must fail creation instead of
/// degrading to parent-only limits.
/// </summary>
internal interface IProcessTreeLimiter : IAsyncDisposable
{
    /// <summary>
    /// Associates the just-started process with this limiter's tree. On Windows the
    /// association happens after <c>Process.Start()</c>, leaving a small window in which
    /// a fast-spawning child can be created before the parent joins the job.
    /// </summary>
    void Attach(Process process);

    /// <summary>Samples aggregated CPU time and peak memory across the whole tree.</summary>
    ValueTask<ProcessTreeUsage> SampleAsync(CancellationToken cancellationToken);

    /// <summary>Terminates the entire tree (group or job) and does not return until requested.</summary>
    void Terminate();
}
