using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Engine;

public sealed class DefaultContentProviderFactory : IContentProviderFactory
{
    public IContentProvider Create(AppConfig config, string rootDir, bool isCi, ILogger logger)
        => ContentProviderFactory.Create(config, rootDir, isCi, logger);

    public Task<RawContentLoadResult> LocalizeContentImagesAsync(
        RawContentLoadResult result,
        MediaConfig media,
        string rootDir,
        string cacheDir,
        ILogger logger,
        CancellationToken cancellationToken)
        => ContentProviderFactory.LocalizeContentImagesAsync(result, media, rootDir, cacheDir, logger, cancellationToken);
}
