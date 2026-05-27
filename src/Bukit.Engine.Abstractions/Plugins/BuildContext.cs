using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;

namespace Bukit.Engine.Abstractions.Plugins;

public sealed class BuildContext
{
    public required AppConfig Config { get; init; }
    public required string RootDir { get; init; }
    public required string OutputDir { get; init; }
    public required string BaseUrl { get; init; }
    public required string LayoutsDir { get; init; }
    public required IReadOnlyList<(ContentItem Item, RouteInfo Route)> Routed { get; init; }
    public IContentBodyStore BodyStore { get; init; } = NullContentBodyStore.Instance;
    public IReadOnlyDictionary<string, SeoIndexEntry> SeoIndex { get; set; } = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase);
    public List<(ContentItem Item, RouteInfo Route)> DerivedRouted { get; } = new();
    public List<(RouteInfo Route, DateTimeOffset LastModified)> DerivedRoutes { get; } = new();
    public List<PluginExecutionInfo> PluginExecutions { get; } = new();
    public Dictionary<string, object> Data { get; } = new(StringComparer.OrdinalIgnoreCase);
    public required ILogger Logger { get; init; }
}
