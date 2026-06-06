using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Engine.Stages;

internal sealed class CollectionWarningStage : IContentStage
{
    public string Name => "CollectionWarning";

    public Task<ContentStageOutput> ExecuteAsync(ContentStageInput input, CancellationToken cancellationToken)
    {
        var warned = 0;

        foreach (var document in input.Documents)
        {
            var collection = ContentFieldReader.GetCollection(document);
            var hasCollection = !string.IsNullOrWhiteSpace(collection);
            var type = ContentFieldReader.GetContentType(document);

            if (hasCollection)
            {
                if (!string.IsNullOrWhiteSpace(type))
                {
                        input.Logger.Warn(
                        $"[WARN] Content \"{document.Id}\" defines both type={type} and collection={collection}. " +
                        "Collection routing uses collection; type remains content metadata.");
                    warned++;
                }
                continue;
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                input.Logger.Warn(
                    $"[WARN] Content \"{document.Id}\" uses type={type} without collection. " +
                    "Routing must be provided by site.collections, site.permalinks, or route front matter.");
                warned++;
            }
        }

        return Task.FromResult(new ContentStageOutput(input.Documents, input.BodyStore, Name, warned, null));
    }
}
