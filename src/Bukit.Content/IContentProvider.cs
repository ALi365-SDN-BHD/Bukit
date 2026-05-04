namespace Bukit.Content;

public interface IContentProvider
{
    Task<ContentLoadResult> LoadAsync(CancellationToken cancellationToken = default);
}

