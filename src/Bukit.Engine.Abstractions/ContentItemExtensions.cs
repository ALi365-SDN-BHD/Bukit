namespace Bukit.Engine.Abstractions.Content;

public static class ContentItemExtensions
{
    public static string GetCollection(this ContentItem item, string defaultCollection = "page")
    {
        if (item.Meta.TryGetValue("collection", out var collection) && collection is not null && !string.IsNullOrWhiteSpace(collection.ToString()))
        {
            return collection.ToString()!;
        }

        if (item.Meta.TryGetValue("type", out var type) && type is not null && !string.IsNullOrWhiteSpace(type.ToString()))
        {
            return type.ToString()!;
        }

        return defaultCollection;
    }
}
