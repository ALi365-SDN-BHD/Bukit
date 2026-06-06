using Bukit.Engine.Abstractions.Content;
namespace Bukit.Content;

public interface IContentProvider
{
    Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default);
}
