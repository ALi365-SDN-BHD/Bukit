using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
namespace Bukit.Engine;

internal sealed record BuildVariantResult(
    string Language,
    string OutputDir,
    string BaseUrl,
    bool SearchSnippetsEnabled,
    IContentBodyStore BodyStore,
    IReadOnlyList<(RouteInfo Route, DateTimeOffset LastModified)> DerivedRoutes,
    IReadOnlyDictionary<string, SeoIndexEntry> SeoIndex,
    IReadOnlyDictionary<string, Bukit.Rendering.SeoModel> SeoModels,
    IReadOnlyList<PluginExecutionInfo> PluginExecutions,
    int RenderedCount,
    int SkippedCount,
    IReadOnlyDictionary<string, int> RenderReasons,
    BuildStageMetrics StageMetrics,
    IReadOnlyList<RoutedContentDocument> RoutedDocuments,
    CanonicalContentGraph? ContentGraph = null,
    IReadOnlyList<RoutedContentDocument>? DerivedDocuments = null,
    IReadOnlyList<PublishProjectionResult>? ProjectionResults = null)
{
    public IReadOnlyList<RoutedContentDocument> DerivedDocuments { get; init; } = DerivedDocuments ?? Array.Empty<RoutedContentDocument>();
    public IReadOnlyList<PublishProjectionResult> ProjectionResults { get; init; } = ProjectionResults ?? Array.Empty<PublishProjectionResult>();
}
