using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Engine;

public interface IContentProviderFactory
{
    IContentProvider Create(AppConfig config, string rootDir, bool isCi, ILogger logger);
    Task<ContentLoadResult> LocalizeContentImagesAsync(
        ContentLoadResult result,
        MediaConfig media,
        string rootDir,
        string cacheDir,
        ILogger logger,
        CancellationToken cancellationToken);
}
