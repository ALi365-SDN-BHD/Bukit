using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;

namespace Bukit.Engine;

/// <summary>Document-level search index rules shared by the search index builder and the publish audits.</summary>
internal static class SearchIndexDocuments
{
    internal static bool IsSearchExcluded(ContentDocument document)
    {
        return document.Publish.ExcludeFromSearch ||
            ContentFieldReader.GetBool(document.CustomFields, "searchExclude") is true ||
            ContentFieldReader.GetBool(document.CustomFields, "excludeFromSearch") is true;
    }

    internal static Dictionary<string, ContentDocument> BuildDocumentMap(IEnumerable<RoutedContentDocument> routed)
    {
        var result = new Dictionary<string, ContentDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var routedDocument in routed)
        {
            result[BuildPathUtils.NormalizeRelPath(routedDocument.Route.OutputPath)] = routedDocument.Document;
        }

        return result;
    }
}
