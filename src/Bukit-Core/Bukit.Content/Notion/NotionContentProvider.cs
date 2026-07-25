using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Content.Notion;

internal sealed class NotionContentProvider : IContentProvider, INotionRelationFallbackResolverProvider
{
    private readonly NotionContentSource _source;

    public NotionContentProvider(NotionProviderOptions options, ILogger? logger = null)
        : this(options, logger, () => new NotionApiClient(options))
    {
    }

    internal NotionContentProvider(
        NotionProviderOptions options,
        ILogger? logger,
        Func<NotionApiClient> clientFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clientFactory);
        _source = new NotionContentSource(
            options.ToSourceOptions(),
            logger,
            () => new NotionContentClient(clientFactory().Transport));
    }

    public Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
        => _source.LoadRawAsync(cancellationToken);

    public INotionRelationFallbackResolver RelationFallbackResolver
        => _source.CreateRelationFallbackResolver();
}
