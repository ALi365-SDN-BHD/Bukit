using Bukit.Engine.Abstractions.Content;
namespace Bukit.Content.Media;

public sealed class LocalizedContentBodyStore : IContentBodyStore, IAsyncDisposable
{
    private readonly IContentBodyStore _inner;
    private readonly ContentImageRewritePipeline _pipeline;
    private IDisposable? _ownedLocalizer;
    private int _disposeState;

    public LocalizedContentBodyStore(IContentBodyStore inner, ContentImageRewritePipeline pipeline)
        : this(inner, pipeline, null)
    {
    }

    public LocalizedContentBodyStore(
        IContentBodyStore inner,
        ContentImageRewritePipeline pipeline,
        IDisposable? ownedLocalizer)
    {
        _inner = inner;
        _pipeline = pipeline;
        _ownedLocalizer = ownedLocalizer;
    }

    public async Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
    {
        var body = await _inner.GetAsync(document, cancellationToken);
        var html = await _pipeline.RewriteBodyHtmlAsync(body.Html, cancellationToken) ?? string.Empty;
        return body with { Html = html };
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        var ownedLocalizer = Interlocked.Exchange(ref _ownedLocalizer, null);
        try
        {
            ownedLocalizer?.Dispose();
        }
        finally
        {
            if (!ReferenceEquals(_inner, ownedLocalizer))
            {
                if (_inner is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else if (_inner is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }
}
