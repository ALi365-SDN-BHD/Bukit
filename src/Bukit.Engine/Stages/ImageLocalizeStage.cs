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
        var loadResult = new RawContentLoadResult(ToRawDocuments(input.Documents), input.BodyStore);

        var sw = Stopwatch.StartNew();
        loadResult = await _factory.LocalizeContentImagesAsync(
            loadResult, input.Config.Content.Media, input.RootDir,
            input.MediaCacheDir, input.Logger, cancellationToken);
        sw.Stop();

        var documents = ContentDocumentNormalizer.ToDocuments(loadResult.Documents);

        return new ContentStageOutput(documents, loadResult.BodyStore, Name, sw.ElapsedMilliseconds, null);
    }

    private static IReadOnlyList<RawContentDocument> ToRawDocuments(IReadOnlyList<ContentDocument> documents)
        => documents
            .Select(document => new RawContentDocument(
                document.Id,
                document.Title,
                document.Slug,
                document.PublishAt,
                document.ContentHtml,
                document.Fields,
                document.BodyKey))
            .ToArray();
}
