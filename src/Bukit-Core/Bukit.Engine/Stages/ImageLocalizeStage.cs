using Bukit.Engine.Abstractions.Content;
using System.Diagnostics;
using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Engine.Stages;

internal sealed class ImageLocalizeStage : IContentStage
{
    private readonly IContentProviderFactory _factory;

    public ImageLocalizeStage(IContentProviderFactory factory)
    {
        _factory = factory;
    }

    public string Name => "ImageLocalize";

    public async Task<ContentStageOutput> ExecuteAsync(ContentStageInput input, CancellationToken cancellationToken)
    {
        var ownedBodyStore = input.BodyStore;
        try
        {
            var loadResult = new RawContentLoadResult(ToRawDocuments(input.Documents), ownedBodyStore);
            var sw = Stopwatch.StartNew();
            loadResult = await _factory.LocalizeContentImagesAsync(
                loadResult, input.Config.Content.Media, input.RootDir,
                input.MediaCacheDir, input.Logger, cancellationToken);
            ownedBodyStore = loadResult.BodyStore;
            sw.Stop();

            var schema = ContentModelSchemaFactory.FromConfig(input.Config);
            var documents = ContentDocumentNormalizer.ToDocuments(loadResult.Documents, schema);

            return new ContentStageOutput(documents, loadResult.BodyStore, Name, sw.ElapsedMilliseconds, null);
        }
        catch
        {
            await DisposeBodyStoreAsync(ownedBodyStore);
            throw;
        }
    }

    private static IReadOnlyList<RawContentDocument> ToRawDocuments(IReadOnlyList<ContentDocument> documents)
        => documents
            .Select(document => new RawContentDocument(
                Id: document.Id,
                Title: document.Title,
                Slug: document.Slug,
                PublishAt: document.PublishAt,
                Body: new RawBody(document.Body.Html, document.Body.BodyKey, document.Body.Markdown, document.Body.PlainText),
                Properties: RawContentValue.FromFields(document.CustomFields),
                Source: document.Source,
                CustomFields: document.CustomFields))
            .ToArray();

    private static async ValueTask DisposeBodyStoreAsync(IContentBodyStore bodyStore)
    {
        if (bodyStore is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (bodyStore is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
