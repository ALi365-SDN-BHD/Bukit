namespace Bukit.Content;

public sealed record ContentLoadResult(IReadOnlyList<ContentItem> Items, IContentBodyStore BodyStore);
