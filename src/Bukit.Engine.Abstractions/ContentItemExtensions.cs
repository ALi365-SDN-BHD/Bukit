namespace Bukit.Engine.Abstractions.Content;

public static class ContentItemExtensions
{
    public static string GetCollection(this ContentItem item, string defaultCollection = "")
    {
        if (item.Meta.TryGetValue("collection", out var collection) && collection is not null && !string.IsNullOrWhiteSpace(collection.ToString()))
        {
            return collection.ToString()!;
        }

        return defaultCollection;
    }
}
