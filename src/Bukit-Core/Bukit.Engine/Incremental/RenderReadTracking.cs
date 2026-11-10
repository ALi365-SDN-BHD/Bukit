using Bukit.Rendering;

namespace Bukit.Engine.Incremental;

/// <summary>
/// Per-route render dependency hashing. The engine always passes a snapshot when incremental
/// rendering is on. Without one (incremental off, or direct dispatcher callers in tests) the
/// route base hash is used as given and no reads are recorded; that hash does not cover shared
/// collections.
/// </summary>
internal static class RenderReadTracking
{
    /// <summary>Runs a synchronous render and returns the collections it read.</summary>
    internal static string Render(
        RenderDependencySnapshot? snapshot,
        Func<string> render,
        out IReadOnlyList<string>? reads)
    {
        if (snapshot is null)
        {
            reads = null;
            return render();
        }

        using var scope = RenderReadRecorder.Begin();
        var html = render();
        reads = scope.Recorder.Complete();
        return html;
    }

    internal static string HashFor(
        RenderDependencySnapshot? snapshot,
        string routeBaseHash,
        IReadOnlyList<string>? reads) =>
        snapshot?.Combine(routeBaseHash, reads) ?? routeBaseHash;

    /// <summary>
    /// The reason for a render caused by a dependency hash change: a changed or unrecorded base
    /// part, or a missing read record, is <c>render_dependency_changed</c>; otherwise a collection
    /// the route read changed.
    /// </summary>
    internal static string ChangeReason(
        RenderDependencySnapshot? snapshot,
        BuildManifestEntry existing,
        string routeBaseHash) =>
        snapshot is not null && existing.RenderReads is not null &&
        string.Equals(existing.RenderBaseHash, routeBaseHash, StringComparison.Ordinal)
            ? "data_dependency_changed"
            : "render_dependency_changed";
}
