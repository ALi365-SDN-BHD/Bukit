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
    public required IReadOnlyList<RoutedContentDocument> RoutedDocuments { get; init; }
    public IReadOnlyList<RouteInfo> StaticHtmlRoutes { get; set; } = Array.Empty<RouteInfo>();
    public CanonicalContentGraph ContentGraph { get; init; } = CanonicalContentGraph.Empty;
    public IContentBodyStore BodyStore { get; init; } = NullContentBodyStore.Instance;
    public IReadOnlyDictionary<string, SeoIndexEntry> SeoIndex { get; set; } = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase);
    public List<RoutedContentDocument> DerivedDocuments { get; } = new();
    public List<(RouteInfo Route, DateTimeOffset LastModified)> DerivedRoutes { get; } = new();
    public List<PluginExecutionInfo> PluginExecutions { get; } = new();
    public Dictionary<string, object> Data { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Func<string, string>? TemplateResolver { get; init; }
    public required ILogger Logger { get; init; }

    public string ResolveTemplateKind(string kind)
    {
        if (TemplateResolver is null)
        {
            throw new ConfigException($"No template resolver is available for plugin template kind '{kind}'.", DiagnosticCode.ConfigInvalidValue);
        }

        return TemplateResolver(kind);
    }
}
