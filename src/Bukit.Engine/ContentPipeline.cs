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
    IReadOnlyList<ContentDocument> Documents,
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

        var legacyContentGraph = CanonicalContentGraphBuilder.Build(currentItems);
        var documents = BuildDocuments(currentItems, legacyContentGraph);
        var contentGraph = CanonicalContentGraphBuilder.BuildFromDocuments(documents);
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
            documents,
            bodyCache?.Metrics,
            contentGraph);
    }

    private static IReadOnlyList<ContentDocument> BuildDocuments(
        IReadOnlyList<ContentItem> items,
        CanonicalContentGraph contentGraph)
    {
        var recordsById = contentGraph.Records.ToDictionary(record => record.Identity.Id, StringComparer.OrdinalIgnoreCase);
        var documents = new List<ContentDocument>(items.Count);
        foreach (var item in items)
        {
            if (!recordsById.TryGetValue(item.Id, out var record))
            {
                continue;
            }

            documents.Add(new ContentDocument(
                record,
                new ContentBodyRef(item.ContentHtml, item.BodyKey, null, null),
                new ContentRoutePolicy(null, null, null, null, record.Classification.Collection),
                new ContentPublishPolicy(
                    string.Equals(record.Identity.Status, "draft", StringComparison.OrdinalIgnoreCase),
                    NoIndex: false,
                    NoFollow: false,
                    ExcludeFromFeed: false,
                    ExcludeFromSearch: false,
                    ExcludeFromSitemap: false,
                    IsDataModule: false),
                item.Fields ?? new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<ContentDiagnostic>()));
        }

        return documents;
    }
}
