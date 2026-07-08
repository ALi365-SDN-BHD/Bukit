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
    ListRouteGraph? ListRouteGraph = null,
    IReadOnlyList<RoutedContentDocument>? DerivedDocuments = null,
    IReadOnlyList<PublishProjectionResult>? ProjectionResults = null,
    IReadOnlyList<RouteInfo>? StaticRoutes = null,
    IReadOnlyList<PluginOutputTrackingInfo>? PluginOutputs = null)
{
    public IReadOnlyList<RoutedContentDocument> DerivedDocuments { get; init; } = DerivedDocuments ?? Array.Empty<RoutedContentDocument>();
    public IReadOnlyList<PublishProjectionResult> ProjectionResults { get; init; } = ProjectionResults ?? Array.Empty<PublishProjectionResult>();
    public IReadOnlyList<RouteInfo> StaticRoutes { get; init; } = StaticRoutes ?? Array.Empty<RouteInfo>();
    public IReadOnlyList<PluginOutputTrackingInfo> PluginOutputs { get; init; } = PluginOutputs ?? Array.Empty<PluginOutputTrackingInfo>();
}
