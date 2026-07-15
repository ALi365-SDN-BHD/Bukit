using Bukit.Config;

namespace Bukit.Engine.Analytics;

internal sealed record AnalyticsRenderContext(
    string RouteUrl,
    string OutputPath,
    bool IsListPage,
    BuildExecutionMode ExecutionMode);
