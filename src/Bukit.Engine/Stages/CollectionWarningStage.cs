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
                    if (typeVal.Equals("post", StringComparison.OrdinalIgnoreCase) ||
                        typeVal.Equals("page", StringComparison.OrdinalIgnoreCase))
                    {
                        var collectionVal = c?.ToString() ?? "(unknown)";
                        input.Logger.Warn(
                            $"[WARN] Content \"{item.Id}\" defines both type={typeVal} and collection={collectionVal}. " +
                            "Collection routing takes precedence; type is treated as legacy metadata.");
                        warned++;
                    }
                }
                continue;
            }

            if (item.Meta.TryGetValue("type", out var t) &&
                t is not null &&
                !string.IsNullOrWhiteSpace(t.ToString()))
            {
                var typeVal = t.ToString()!;
                if (typeVal.Equals("post", StringComparison.OrdinalIgnoreCase) ||
                    typeVal.Equals("page", StringComparison.OrdinalIgnoreCase))
                {
                    input.Logger.Warn(
                        $"[DEPRECATED] Content \"{item.Id}\" uses type={typeVal} without collection. " +
                        "Legacy routing is enabled. Please migrate to content.collection and site.collections.");
                    warned++;
                }
            }
        }

        return Task.FromResult(new ContentStageOutput(input.Items, input.BodyStore, Name, warned, null));
    }
}
