using System.Diagnostics;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Normalization;
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
    private readonly IContentProviderFactory? _contentProviderFactory;

    public ContentPipeline(IReadOnlyList<IContentStage> stages, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(stages);
        ArgumentNullException.ThrowIfNull(logger);
        _stages = stages;
        _logger = logger;
    }

    public ContentPipeline(IContentProviderFactory contentProviderFactory, ILogger logger)
        : this(
            new IContentStage[]
        {
            new ContentLoadStage(contentProviderFactory),
            new ImageLocalizeStage(contentProviderFactory),
            new DraftFilterStage(),
            new SchemaDefaultsStage(),
            new SchemaValidateStage(),
            new CollectionWarningStage()
        },
            logger,
            contentProviderFactory)
    {
    }

    private ContentPipeline(IReadOnlyList<IContentStage> stages, ILogger logger, IContentProviderFactory contentProviderFactory)
        : this(stages, logger)
    {
        _contentProviderFactory = contentProviderFactory;
    }

    public async Task<ContentPipelineResult> ExecuteAsync(
        AppConfig config,
        string rootDir,
        ConfigOverrides overrides,
        string mediaCacheDir,
        CancellationToken cancellationToken = default)
    {
        if (_contentProviderFactory is not null)
        {
            var provider = _contentProviderFactory.Create(config, rootDir, overrides.IsCI, _logger);
            if (provider is IRawContentProvider rawProvider)
            {
                return await ExecuteRawFirstAsync(rawProvider, config, rootDir, overrides, mediaCacheDir, cancellationToken);
            }
        }

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

    private async Task<ContentPipelineResult> ExecuteRawFirstAsync(
        IRawContentProvider rawProvider,
        AppConfig config,
        string rootDir,
        ConfigOverrides overrides,
        string mediaCacheDir,
        CancellationToken cancellationToken)
    {
        var loadStopwatch = Stopwatch.StartNew();
        var rawResult = await rawProvider.LoadRawAsync(cancellationToken);
        loadStopwatch.Stop();
        _logger.Info($"event=content.stage stage=RawContentLoad duration_ms={loadStopwatch.ElapsedMilliseconds}");
        _logger.Info($"event=content.raw_loaded count={rawResult.Documents.Count}");

        var normalizeStopwatch = Stopwatch.StartNew();
        var normalizer = new ContentNormalizer();
        var documents = rawResult.Documents
            .Select(document => normalizer.Normalize(document, ContentModelSchema.Default))
            .Where(document => overrides.Draft == true || !document.Publish.Draft)
            .ToArray();
        normalizeStopwatch.Stop();
        _logger.Info($"event=content.stage stage=ContentNormalization duration_ms={normalizeStopwatch.ElapsedMilliseconds}");

        var graphStopwatch = Stopwatch.StartNew();
        var contentGraph = CanonicalContentGraphBuilder.BuildFromDocuments(documents);
        var allSchemaErrors = new List<ContentSchemaValidator.SchemaValidationError>();
        foreach (var diagnostic in documents.SelectMany(document => document.Diagnostics))
        {
            allSchemaErrors.Add(new ContentSchemaValidator.SchemaValidationError(
                diagnostic.Field ?? string.Empty,
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.SourceId));
        }

        var canonicalErrors = CanonicalContentValidator.Validate(contentGraph);
        if (canonicalErrors.Count > 0)
        {
            allSchemaErrors.AddRange(canonicalErrors);
            foreach (var error in canonicalErrors)
            {
                _logger.Warn($"event=canonical.validation code={error.Code} field={error.Field} source={error.SourcePath} message={error.Message}");
            }
        }

        graphStopwatch.Stop();
        _logger.Info($"event=content.stage stage=ContentGraphValidation duration_ms={graphStopwatch.ElapsedMilliseconds}");

        var bodyCache = new BodyCacheDecorator(rawResult.BodyStore);
        return new ContentPipelineResult(
            BuildLegacyItems(documents),
            bodyCache,
            allSchemaErrors,
            documents,
            bodyCache.Metrics,
            contentGraph);
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

    private static IReadOnlyList<ContentItem> BuildLegacyItems(IReadOnlyList<ContentDocument> documents)
    {
        return documents.Select(document =>
        {
            var record = document.Record;
            var fields = new Dictionary<string, ContentField>(document.CustomFields, StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", record.Classification.Type),
                ["collection"] = new("text", record.Classification.Collection),
                ["status"] = new("text", record.Identity.Status),
                ["language"] = new("text", record.Presentation.Language),
                ["review_status"] = new("text", record.Trust.ReviewStatus)
            };

            if (!string.IsNullOrWhiteSpace(record.Presentation.Summary))
            {
                fields["summary"] = new ContentField("text", record.Presentation.Summary!);
            }

            if (record.Classification.Tags.Count > 0)
            {
                fields["tags"] = new ContentField("list", record.Classification.Tags.ToArray());
            }

            if (document.Publish.Draft)
            {
                fields["draft"] = new ContentField("bool", true);
            }

            return new ContentItem(
                record.Identity.Id,
                record.Presentation.Title,
                record.Identity.Slug,
                record.Lifecycle.PublishedAt,
                document.Body.Html,
                fields,
                document.Body.BodyKey);
        }).ToArray();
    }
}
