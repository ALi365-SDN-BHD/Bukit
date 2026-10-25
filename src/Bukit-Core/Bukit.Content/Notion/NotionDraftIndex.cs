using Bukit.Engine.Abstractions.Content;
namespace Bukit.Content.Notion;

internal sealed class NotionDraftIndex<T> where T : class
{
    private readonly IReadOnlyDictionary<string, T> _byPageId;

    private NotionDraftIndex(IReadOnlyDictionary<string, T> byPageId)
    {
        _byPageId = byPageId;
    }

    internal static NotionDraftIndex<T> From(IEnumerable<T> items, Func<T, string> pageIdSelector)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(pageIdSelector);

        var byPageId = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var pageId = pageIdSelector(item);
            if (string.IsNullOrWhiteSpace(pageId))
            {
                continue;
            }

            byPageId[pageId] = item;
        }

        return new NotionDraftIndex<T>(byPageId);
    }

    internal T GetRequired(string pageId)
    {
        if (_byPageId.TryGetValue(pageId, out var item))
        {
            return item;
        }

        throw new InvalidOperationException($"Unable to find Notion draft for page '{pageId}'.");
    }
}
