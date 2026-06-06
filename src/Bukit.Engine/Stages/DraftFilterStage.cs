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
            return Task.FromResult(new ContentStageOutput(input.Items, input.BodyStore, Name, 0, null));
        }

        var before = input.Items.Count;
        var filtered = input.Items.Where(i => !IsDraft(i)).ToList();

        if (filtered.Count < before)
        {
            input.Logger.Info($"event=content.draft_filtered removed={before - filtered.Count}");
        }

        return Task.FromResult(new ContentStageOutput(filtered, input.BodyStore, Name, 0, null));
    }

    private static bool IsDraft(ContentItem item)
    {
        if (item.Fields is null || !item.Fields.TryGetValue("draft", out var field) || field.Value is null)
        {
            return false;
        }

        return ValueCoercion.IsTruthy(field.Value);
    }
}
