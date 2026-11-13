using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Engine;

/// <remarks>
/// <see cref="SiteEngine"/> and <see cref="ContentPipeline"/> derive <see cref="SiteConfig.Language"/>
/// from <c>site.languages</c> before calling this factory. Callers that use it directly with a
/// config built in code should pass a config from <see cref="ConfigLoader"/>, or set
/// <see cref="SiteConfig.Language"/> to the default language themselves.
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
