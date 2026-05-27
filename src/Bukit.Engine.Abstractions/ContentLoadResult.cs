namespace Bukit.Engine.Abstractions.Content;

public sealed record ContentLoadResult(IReadOnlyList<ContentItem> Items, IContentBodyStore BodyStore);
