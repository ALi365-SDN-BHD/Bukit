using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Engine.Stages;

internal sealed class ContentGraphValidateStage : IContentStage
{
    public string Name => "ContentGraphValidate";

    public Task<ContentStageOutput> ExecuteAsync(ContentStageInput input, CancellationToken cancellationToken)
    {
        var graph = CanonicalContentGraphBuilder.BuildFromDocuments(input.Documents);
        var schema = ContentModelSchemaFactory.FromConfig(input.Config);

        var issues = CanonicalContentValidator.Validate(graph)
            .Concat(ContentModelSchemaValidator.Validate(graph, schema))
            .Concat(input.Documents.SelectMany(ToSchemaErrors))
            .GroupBy(x => $"{x.SourcePath}:{x.Field}:{x.Code}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();

        var globalMode = (input.Config.Build.SchemaFailMode ?? "warn").Trim().ToLowerInvariant();
        var documentsById = input.Documents
            .GroupBy(document => document.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var classified = issues
            .Select(issue => (Issue: issue, Mode: ResolveFailMode(issue, documentsById, input.Config, globalMode)))
            .Where(item => !string.Equals(item.Mode, "off", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var errors = classified.Select(item => item.Issue).ToArray();

        foreach (var (error, _) in classified)
        {
            input.Logger.Warn($"event=canonical.validation code={error.Code} field={error.Field} source={error.SourcePath} message={error.Message}");
        }

        if (classified.Any(item => string.Equals(item.Mode, "strict", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConfigException($"Canonical content validation failed with {errors.Length} error(s).", DiagnosticCode.SchemaStrictModeBlocked);
        }

        return Task.FromResult(new ContentStageOutput(input.Documents, input.BodyStore, Name, 0, errors));
    }

    private static IEnumerable<ContentValidationIssue> ToSchemaErrors(ContentDocument document)
        => document.Diagnostics
            .Where(diagnostic => string.Equals(diagnostic.Severity, "error", StringComparison.OrdinalIgnoreCase))
            .Select(diagnostic => new ContentValidationIssue(
                diagnostic.Field ?? "content",
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.SourceId ?? document.Id));

    private static string ResolveFailMode(
        ContentValidationIssue issue,
        IReadOnlyDictionary<string, ContentDocument> documentsById,
        Bukit.Config.AppConfig config,
        string globalMode)
    {
        if (string.IsNullOrWhiteSpace(issue.SourcePath) ||
            !documentsById.TryGetValue(issue.SourcePath, out var document))
        {
            return globalMode;
        }

        var collection = ContentFieldReader.GetCollection(document);
        Bukit.Config.CollectionConfig? collectionConfig = null;
        if (!string.IsNullOrWhiteSpace(collection) &&
            config.Site.Collections is { Count: > 0 } collections)
        {
            collections.TryGetValue(collection, out collectionConfig);
        }

        return ContentSchemaValidator.ResolveSchemaFailMode(collectionConfig, globalMode);
    }
}
