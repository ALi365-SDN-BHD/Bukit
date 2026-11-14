using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Engine;

/// <remarks>
/// A config built in code may set only <c>site.languages</c>: the factory derives
/// <see cref="SiteConfig.Language"/> from it, as <see cref="ConfigLoader"/> does.
/// </remarks>
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
