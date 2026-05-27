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
        var loadResult = new ContentLoadResult(input.Items, input.BodyStore);

        var sw = Stopwatch.StartNew();
        loadResult = await _factory.LocalizeContentImagesAsync(
            loadResult, input.Config.Content.Media, input.RootDir,
            input.MediaCacheDir, input.Logger, cancellationToken);
        sw.Stop();

        return new ContentStageOutput(loadResult.Items, loadResult.BodyStore, Name, sw.ElapsedMilliseconds, null);
    }
}
