using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
namespace Bukit.Engine;

internal sealed record SpecialListDefinition(
    RouteInfo Route,
    IReadOnlyList<RoutedContentDocument> Items,
    bool IncludeContent,
    IReadOnlyDictionary<string, ContentField>? PageFields = null,
    ListPageContext? PageContext = null);

public static class SpecialListRouteBuilder
{
    internal static List<SpecialListDefinition> Build(
        IReadOnlyList<RoutedContentDocument> routed,
        IReadOnlyDictionary<string, CollectionConfig>? collections,
        string layoutsDir,
        string listPageContentMode,
        string outputPathEncoding,
        ThemeTemplateResolver? templateResolver = null)
    {
        var graph = ListRouteGraphBuilder.Build(routed, collections, outputPathEncoding, templateResolver);
        return ListRouteRenderPlanBuilder
            .Build(graph, routed, layoutsDir, listPageContentMode)
            .ToList();
    }

    internal static bool TryMatchFieldValue(IReadOnlyDictionary<string, ContentField>? fields, string field, string expectedValue)
    {
        return FilteredListMatcher.Matches(fields, new FilteredListConfig
        {
            Field = field,
            Value = expectedValue,
            ListRoute = "/"
        });
    }

}
