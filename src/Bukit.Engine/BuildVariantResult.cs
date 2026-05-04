using Bukit.Content;
using Bukit.Engine.Plugins;
using Bukit.Routing;

namespace Bukit.Engine;

internal sealed record BuildVariantResult(
    string Language,
    string OutputDir,
    string BaseUrl,
    bool SearchSnippetsEnabled,
    IContentBodyStore BodyStore,
    IReadOnlyList<(ContentItem Item, RouteInfo Route)> Routed,
    IReadOnlyList<(ContentItem Item, RouteInfo Route)> DerivedRouted,
    IReadOnlyList<(RouteInfo Route, DateTimeOffset LastModified)> DerivedRoutes,
    IReadOnlyList<PluginExecutionInfo> PluginExecutions,
    int RenderedCount,
    int SkippedCount,
    IReadOnlyDictionary<string, int> RenderReasons,
    BuildStageMetrics StageMetrics);
