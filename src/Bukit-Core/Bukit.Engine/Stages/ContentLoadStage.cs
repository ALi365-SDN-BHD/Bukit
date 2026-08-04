using System.Diagnostics;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Engine.Stages;

internal sealed class ContentLoadStage : IContentStage
{
    private readonly IContentProviderFactory _factory;

    public ContentLoadStage(IContentProviderFactory factory)
    {
        _factory = factory;
    }

    public string Name => "ContentLoad";

    public async Task<ContentStageOutput> ExecuteAsync(ContentStageInput input, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        var provider = _factory.Create(input.Config, input.RootDir, input.Overrides.IsCI, input.Logger);
        var rawResult = await provider.LoadRawAsync(cancellationToken);
        IReadOnlyList<ContentDocument> documents;
        try
        {
            ContentCollectionContractValidator.Validate(rawResult.Documents);
            var schema = ContentModelSchemaFactory.FromConfig(input.Config);
            documents = ContentDocumentNormalizer.ToDocuments(rawResult.Documents, schema);
        }
        catch
        {
            await DisposeBodyStoreAsync(rawResult.BodyStore);
            throw;
        }

        sw.Stop();
        input.Logger.Info($"event=content.loaded mode=raw count={documents.Count}");

        return new ContentStageOutput(documents, rawResult.BodyStore, Name, sw.ElapsedMilliseconds, null);
    }

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
