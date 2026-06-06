using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Engine.Stages;

internal sealed class DraftFilterStage : IContentStage
{
    public string Name => "DraftFilter";

    public Task<ContentStageOutput> ExecuteAsync(ContentStageInput input, CancellationToken cancellationToken)
    {
        if (input.Config.Build.Draft)
        {
            return Task.FromResult(new ContentStageOutput(input.Documents, input.BodyStore, Name, 0, null));
        }

        var documents = input.Documents.ToArray();
        var documentCount = documents.Length;
        var before = documentCount;
        var filteredDocuments = documents
            .Where(document => ContentFieldReader.GetBool(document.Fields, "draft") is not true)
            .ToArray();

        if (filteredDocuments.Length < before)
        {
            input.Logger.Info($"event=content.draft_filtered removed={before - filteredDocuments.Length}");
        }

        return Task.FromResult(new ContentStageOutput(filteredDocuments, input.BodyStore, Name, 0, null));
    }
}
