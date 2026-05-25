using Bukit.Config;
using Bukit.Content;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed class FixedContentProviderFactory : IContentProviderFactory
{
    private readonly IContentProvider _provider;
    private readonly IContentProviderFactory _fallback;

    internal FixedContentProviderFactory(IContentProvider provider, IContentProviderFactory fallback)
    {
        _provider = provider;
        _fallback = fallback;
    }

    public IContentProvider Create(AppConfig config, string rootDir, bool isCi, ILogger logger)
        => _provider;

    public Task<ContentLoadResult> LocalizeContentImagesAsync(
        ContentLoadResult result,
        MediaConfig media,
        string rootDir,
        string cacheDir,
        ILogger logger,
        CancellationToken cancellationToken)
        => _fallback.LocalizeContentImagesAsync(result, media, rootDir, cacheDir, logger, cancellationToken);
}
