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
        var loadResult = await provider.LoadAsync(cancellationToken);

        sw.Stop();
        input.Logger.Info($"event=content.loaded count={loadResult.Items.Count}");

        return new ContentStageOutput(loadResult.Items, loadResult.BodyStore, Name, sw.ElapsedMilliseconds, null);
    }
}
