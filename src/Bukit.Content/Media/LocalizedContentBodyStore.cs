using Bukit.Engine.Abstractions.Content;
namespace Bukit.Content.Media;

public sealed class LocalizedContentBodyStore : IContentBodyStore
{
    private readonly IContentBodyStore _inner;
    private readonly ContentImageRewritePipeline _pipeline;

    public LocalizedContentBodyStore(IContentBodyStore inner, ContentImageRewritePipeline pipeline)
    {
        _inner = inner;
        _pipeline = pipeline;
    }

    public async Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
    {
        var body = await _inner.GetAsync(document, cancellationToken);
        var html = await _pipeline.RewriteBodyHtmlAsync(body.Html, cancellationToken) ?? string.Empty;
        return body with { Html = html };
    }
}
