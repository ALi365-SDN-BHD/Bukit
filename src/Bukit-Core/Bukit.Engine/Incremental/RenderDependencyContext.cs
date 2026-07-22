using Bukit.Config;
using Bukit.Rendering;

namespace Bukit.Engine.Incremental;

internal sealed record RenderDependencyContext(
    AppConfig Config,
    SiteModel SiteModel,
    BuildExecutionMode ExecutionMode,
    string AnalyticsRendererContractVersion);
