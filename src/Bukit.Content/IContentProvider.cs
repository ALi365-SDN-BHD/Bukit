using Bukit.Engine.Abstractions.Content;
namespace Bukit.Content;

public interface IContentProvider
{
    Task<ContentLoadResult> LoadAsync(CancellationToken cancellationToken = default);
}

public interface IRawContentProvider
{
    Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default);
}

public sealed record RawContentLoadResult(IReadOnlyList<RawContentDocument> Documents, IContentBodyStore BodyStore);
