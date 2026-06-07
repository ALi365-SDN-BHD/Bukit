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

        var errors = CanonicalContentValidator.Validate(graph)
            .Concat(ContentModelSchemaValidator.Validate(graph, schema))
            .Concat(input.Documents.SelectMany(ToSchemaErrors))
            .GroupBy(x => $"{x.SourcePath}:{x.Field}:{x.Code}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();

        foreach (var error in errors)
        {
            input.Logger.Warn($"event=canonical.validation code={error.Code} field={error.Field} source={error.SourcePath} message={error.Message}");
        }

        var failMode = (input.Config.Build.SchemaFailMode ?? "warn").Trim().ToLowerInvariant();
        if (errors.Length > 0 && string.Equals(failMode, "strict", StringComparison.OrdinalIgnoreCase))
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
}
