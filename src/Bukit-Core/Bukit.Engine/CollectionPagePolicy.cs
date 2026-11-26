using Bukit.Config;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

/// <summary>
/// Decides which documents get a page: content documents, except entries of a collection whose
/// <c>routeWhen</c> test they fail. Those stay available to content links, like data documents.
/// </summary>
internal static class CollectionPagePolicy
{
    internal static bool HasPage(ContentDocument document, SiteConfig site)
    {
        if (ContentFieldReader.IsDataItem(document))
        {
            return false;
        }

        return site.Collections is null ||
               !site.Collections.TryGetValue(ContentFieldReader.GetCollection(document), out var collection) ||
               collection.RouteWhen is not { } when ||
               Matches(document.CustomFields, when);
    }

    /// <summary>True when the field holds the value: text ignoring case, a boolean or a whole number.</summary>
    private static bool Matches(IReadOnlyDictionary<string, ContentField>? fields, CollectionRouteWhenConfig when)
    {
        if (fields is null || !fields.TryGetValue(when.Field, out var field))
        {
            return false;
        }

        var text = field.Value switch
        {
            string value => value.Trim(),
            bool flag => flag ? "true" : "false",
            _ => ContentFieldReader.WholeNumberText(field.Value)
        };
        return text is not null && string.Equals(text, when.Eq.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
