using System.Diagnostics;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Stages;
using Bukit.Shared;

namespace Bukit.Engine;

public sealed record ContentPipelineResult(
    IReadOnlyList<ContentDocument> Documents,
    IContentBodyStore BodyStore,
    IReadOnlyList<ContentValidationIssue> SchemaErrors,
    BodyCacheMetrics? BodyCacheMetrics = null,
    CanonicalContentGraph? ContentGraph = null);

public sealed class ContentPipeline
{
    private readonly IReadOnlyList<IContentStage> _stages;
    private readonly ILogger _logger;

    public ContentPipeline(IReadOnlyList<IContentStage> stages, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(stages);
        ArgumentNullException.ThrowIfNull(logger);
        _stages = stages;
        _logger = logger;
    }

    public ContentPipeline(IContentProviderFactory contentProviderFactory, ILogger logger)
        : this(new IContentStage[]
        {
            new ContentLoadStage(contentProviderFactory),
            new ImageLocalizeStage(contentProviderFactory),
            new DraftFilterStage(),
            new ContentGraphValidateStage(),
            new CollectionWarningStage()
        }, logger)
    {
    }

    public async Task<ContentPipelineResult> ExecuteAsync(
        AppConfig config,
        string rootDir,
        ConfigOverrides overrides,
        string mediaCacheDir,
        CancellationToken cancellationToken = default)
    {
        var input = new ContentStageInput(
            Array.Empty<ContentDocument>(),
            EmptyContentBodyStore.Instance,
            config,
            overrides,
            rootDir,
            mediaCacheDir,
            _logger);

        return await ExecuteAsync(input, cancellationToken);
    }

    internal async Task<ContentPipelineResult> ExecuteAsync(
        ContentStageInput input,
        CancellationToken cancellationToken)
    {
        var currentDocuments = input.Documents;
        var currentBodyStore = input.BodyStore;
        BodyCacheDecorator? bodyCache = null;
        var ownsBodyCache = false;
        List<ContentValidationIssue>? allSchemaErrors = null;

        try
        {
            foreach (var stage in _stages)
            {
                var stageInput = input with { Documents = currentDocuments, BodyStore = currentBodyStore };
                var sw = Stopwatch.StartNew();

                var output = await stage.ExecuteAsync(stageInput, cancellationToken);

                sw.Stop();
                var actualDuration = output.DurationMs > 0 ? output.DurationMs : sw.ElapsedMilliseconds;
                _logger.Info($"event=content.stage stage={stage.Name} duration_ms={actualDuration}");

                currentDocuments = output.Documents;
                currentBodyStore = output.BodyStore;

                if (stage.Name == "ImageLocalize")
                {
                    bodyCache = new BodyCacheDecorator(currentBodyStore, 10000, cancellationToken);
                    currentBodyStore = bodyCache;
                    ownsBodyCache = true;
                }

                if (output.SchemaErrors is { Count: > 0 } errors)
                {
                    allSchemaErrors ??= new List<ContentValidationIssue>();
                    allSchemaErrors.AddRange(errors);
                }
            }
        }
        catch
        {
            if (ownsBodyCache && bodyCache is not null)
            {
                await bodyCache.DisposeAsync();
            }
            throw;
        }

        var contentGraph = CanonicalContentGraphBuilder.BuildFromDocuments(currentDocuments);

        return new ContentPipelineResult(
            currentDocuments,
            currentBodyStore,
            (IReadOnlyList<ContentValidationIssue>?)allSchemaErrors ?? Array.Empty<ContentValidationIssue>(),
            bodyCache?.Metrics,
            contentGraph);
    }
}
