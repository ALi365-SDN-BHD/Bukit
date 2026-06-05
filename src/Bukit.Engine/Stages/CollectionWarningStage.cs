using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Engine.Stages;

internal sealed class CollectionWarningStage : IContentStage
{
    public string Name => "CollectionWarning";

    public Task<ContentStageOutput> ExecuteAsync(ContentStageInput input, CancellationToken cancellationToken)
    {
        var warned = 0;

        foreach (var item in input.Items)
        {
            var collection = item.GetCollection();
            var hasCollection = !string.IsNullOrWhiteSpace(collection);
            var type = item.GetContentType();

            if (hasCollection)
            {
                if (!string.IsNullOrWhiteSpace(type))
                {
                    input.Logger.Warn(
                        $"[WARN] Content \"{item.Id}\" defines both type={type} and collection={collection}. " +
                        "Collection routing uses collection; type remains content metadata.");
                    warned++;
                }
                continue;
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                input.Logger.Warn(
                    $"[WARN] Content \"{item.Id}\" uses type={type} without collection. " +
                    "Routing must be provided by site.collections, site.permalinks, or route front matter.");
                warned++;
            }
        }

        return Task.FromResult(new ContentStageOutput(input.Items, input.BodyStore, Name, warned, null));
    }
}
