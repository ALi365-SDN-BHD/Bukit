using System.Diagnostics;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Stages;
using Bukit.Shared;

namespace Bukit.Engine;

public sealed record ContentPipelineResult(
    IReadOnlyList<ContentItem> Items,
    IContentBodyStore BodyStore,
    IReadOnlyList<ContentSchemaValidator.SchemaValidationError> SchemaErrors,
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
            new SchemaDefaultsStage(),
            new SchemaValidateStage(),
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
            Array.Empty<ContentItem>(),
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
        var currentItems = input.Items;
        var currentBodyStore = input.BodyStore;
        BodyCacheDecorator? bodyCache = null;
        List<ContentSchemaValidator.SchemaValidationError>? allSchemaErrors = null;

        foreach (var stage in _stages)
        {
            var stageInput = input with { Items = currentItems, BodyStore = currentBodyStore };
            var sw = Stopwatch.StartNew();

            var output = await stage.ExecuteAsync(stageInput, cancellationToken);

            sw.Stop();
            var actualDuration = output.DurationMs > 0 ? output.DurationMs : sw.ElapsedMilliseconds;
            _logger.Info($"event=content.stage stage={stage.Name} duration_ms={actualDuration}");

            currentItems = output.Items;
            currentBodyStore = output.BodyStore;

            if (stage.Name == "ImageLocalize")
            {
                bodyCache = new BodyCacheDecorator(currentBodyStore);
                currentBodyStore = bodyCache;
            }

            if (output.SchemaErrors is { Count: > 0 } errors)
            {
                allSchemaErrors ??= new List<ContentSchemaValidator.SchemaValidationError>();
                allSchemaErrors.AddRange(errors);
            }
        }

        var contentGraph = CanonicalContentGraphBuilder.Build(currentItems);
        var canonicalErrors = CanonicalContentValidator.Validate(contentGraph);
        if (canonicalErrors.Count > 0)
        {
            allSchemaErrors ??= new List<ContentSchemaValidator.SchemaValidationError>();
            allSchemaErrors.AddRange(canonicalErrors);
            foreach (var error in canonicalErrors)
            {
                _logger.Warn($"event=canonical.validation code={error.Code} field={error.Field} source={error.SourcePath} message={error.Message}");
            }
        }

        return new ContentPipelineResult(
            currentItems,
            currentBodyStore,
            (IReadOnlyList<ContentSchemaValidator.SchemaValidationError>?)allSchemaErrors ?? Array.Empty<ContentSchemaValidator.SchemaValidationError>(),
            bodyCache?.Metrics,
            contentGraph);
    }
}
