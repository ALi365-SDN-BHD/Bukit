using Bukit.Engine.Abstractions.Content;
namespace Bukit.Content;

public interface IContentProvider
{
    Task<ContentLoadResult> LoadAsync(CancellationToken cancellationToken = default);
}

