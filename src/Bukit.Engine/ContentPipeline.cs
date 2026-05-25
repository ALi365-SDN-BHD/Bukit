using Bukit.Config;
using Bukit.Content;
using Bukit.Shared;

namespace Bukit.Engine;

public sealed record ContentPipelineResult(
    IReadOnlyList<ContentItem> Items,
    IContentBodyStore BodyStore,
    IReadOnlyList<ContentSchemaValidator.SchemaValidationError> SchemaErrors);

public sealed class ContentPipeline
{
    private readonly IContentProviderFactory _contentProviderFactory;
    private readonly ILogger _logger;

    public ContentPipeline(IContentProviderFactory contentProviderFactory, ILogger logger)
    {
        _contentProviderFactory = contentProviderFactory;
        _logger = logger;
    }

    public async Task<ContentPipelineResult> ExecuteAsync(
        AppConfig config,
        string rootDir,
        ConfigOverrides overrides,
        string mediaCacheDir,
        CancellationToken cancellationToken = default)
    {
        var provider = _contentProviderFactory.Create(config, rootDir, overrides.IsCI, _logger);
        var loadResult = await provider.LoadAsync(cancellationToken);
        loadResult = await _contentProviderFactory.LocalizeContentImagesAsync(loadResult, config.Content.Media, rootDir, mediaCacheDir, _logger, cancellationToken);
        var items = loadResult.Items;
        var bodyStore = loadResult.BodyStore;

        if (!config.Build.Draft)
        {
            var before = items.Count;
            items = items.Where(i =>
                !(i.Meta.TryGetValue("draft", out var d) && d is true or "true" or "True")).ToList();
            if (items.Count < before)
            {
                _logger.Info($"event=content.draft_filtered removed={before - items.Count}");
            }
        }

        _logger.Info($"event=content.loaded count={items.Count}");

        items = ContentSchemaValidator.ApplyDefaults(config.Site.Collections, items);
        var schemaErrors = ValidateContentSchemas(config.Site.Collections, items, _logger);
        if (schemaErrors.Count > 0)
        {
            var schemaFailMode = (config.Build.SchemaFailMode ?? "warn").Trim().ToLowerInvariant();
            if (schemaFailMode == "strict")
            {
                throw new ConfigException($"Schema validation failed with {schemaErrors.Count} error(s).");
            }
        }

        return new ContentPipelineResult(items, bodyStore, schemaErrors);
    }

    private static List<ContentSchemaValidator.SchemaValidationError> ValidateContentSchemas(
        IReadOnlyDictionary<string, CollectionConfig>? collections,
        IReadOnlyList<ContentItem> items,
        ILogger logger)
    {
        var allErrors = new List<ContentSchemaValidator.SchemaValidationError>();

        if (collections is null || collections.Count == 0)
        {
            return allErrors;
        }

        foreach (var item in items)
        {
            var collectionName = GetEffectiveCollection(item);
            if (string.IsNullOrWhiteSpace(collectionName) ||
                !collections.TryGetValue(collectionName, out var collection) ||
                collection.Schema is null || collection.Schema.Count == 0)
            {
                continue;
            }

            var errors = ContentSchemaValidator.Validate(item.Meta, collection.Schema, item.Id);
            if (errors.Count > 0)
            {
                allErrors.AddRange(errors);
                foreach (var error in errors)
                {
                    logger.Warn($"event=schema.validation code={error.Code} field={error.Field} source={error.SourcePath} message={error.Message}");
                }
            }
        }

        return allErrors;
    }

    private static string GetEffectiveCollection(ContentItem item)
    {
        if (item.Meta.TryGetValue("collection", out var c) && c is not null && !string.IsNullOrWhiteSpace(c.ToString()))
        {
            return c.ToString()!;
        }

        if (item.Meta.TryGetValue("type", out var t) && t is not null && !string.IsNullOrWhiteSpace(t.ToString()))
        {
            return t.ToString()!;
        }

        return "page";
    }
}
