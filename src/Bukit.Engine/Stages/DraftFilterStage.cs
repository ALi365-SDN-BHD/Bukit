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
        var filtered = input.Items.Where(i => ContentFieldReader.GetBool(i.Fields, "draft") is not true).ToList();

        if (filtered.Count < before)
        {
            input.Logger.Info($"event=content.draft_filtered removed={before - filtered.Count}");
        }

        return Task.FromResult(new ContentStageOutput(filtered, input.BodyStore, Name, 0, null));
    }
}
