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
            var hasCollection = item.Meta.TryGetValue("collection", out var c) &&
                                c is not null &&
                                !string.IsNullOrWhiteSpace(c.ToString());

            if (hasCollection)
            {
                if (item.Meta.TryGetValue("type", out var typeObj) &&
                    typeObj is not null &&
                    !string.IsNullOrWhiteSpace(typeObj.ToString()))
                {
                    var typeVal = typeObj.ToString()!;
                    var collectionVal = c?.ToString() ?? "(unknown)";
                    input.Logger.Warn(
                        $"[WARN] Content \"{item.Id}\" defines both type={typeVal} and collection={collectionVal}. " +
                        "Collection routing uses collection; type remains content metadata.");
                    warned++;
                }
                continue;
            }

            if (item.Meta.TryGetValue("type", out var t) &&
                t is not null &&
                !string.IsNullOrWhiteSpace(t.ToString()))
            {
                var typeVal = t.ToString()!;
                input.Logger.Warn(
                    $"[WARN] Content \"{item.Id}\" uses type={typeVal} without collection. " +
                    "Routing must be provided by site.collections, site.permalinks, or route front matter.");
                warned++;
            }
        }

        return Task.FromResult(new ContentStageOutput(input.Items, input.BodyStore, Name, warned, null));
    }
}
