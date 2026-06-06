using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Engine.Stages;

internal sealed class SchemaValidateStage : IContentStage
{
    public string Name => "SchemaValidate";

    public Task<ContentStageOutput> ExecuteAsync(ContentStageInput input, CancellationToken cancellationToken)
    {
        var collections = input.Config.Site.Collections;
        var allErrors = new List<ContentSchemaValidator.SchemaValidationError>();

        if (collections is null || collections.Count == 0)
        {
            return Task.FromResult(new ContentStageOutput(input.Items, input.BodyStore, Name, 0, Array.Empty<ContentSchemaValidator.SchemaValidationError>()));
        }

        var globalFailMode = (input.Config.Build.SchemaFailMode ?? "warn").Trim().ToLowerInvariant();

        if (globalFailMode == "strict")
        {
            foreach (var item in input.Items)
            {
                var collectionName = ContentFieldReader.GetEffectiveCollection(item);
                if (string.IsNullOrWhiteSpace(collectionName) ||
                    !collections.TryGetValue(collectionName, out var collection) ||
                    collection.Schema is null || collection.Schema.Count == 0)
                {
                    continue;
                }

                var errors = ContentSchemaValidator.ValidateFields(item.Fields, collection.Schema, item.Id, failMode: "strict");
                if (errors.Count > 0)
                {
                    allErrors.AddRange(errors);
                    foreach (var error in errors)
                    {
                        input.Logger.Warn($"event=schema.validation code={error.Code} field={error.Field} source={error.SourcePath} message={error.Message}");
                    }
                }
            }

            if (allErrors.Count > 0)
            {
                throw new ConfigException($"Schema validation failed with {allErrors.Count} error(s).", DiagnosticCode.SchemaStrictModeBlocked);
            }
        }

        foreach (var item in input.Items)
        {
            var collectionName = ContentFieldReader.GetEffectiveCollection(item);
            if (string.IsNullOrWhiteSpace(collectionName) ||
                !collections.TryGetValue(collectionName, out var collection) ||
                collection.Schema is null || collection.Schema.Count == 0)
            {
                continue;
            }

            var failMode = ContentSchemaValidator.ResolveSchemaFailMode(collection, globalFailMode);
            var errors = ContentSchemaValidator.ValidateFields(item.Fields, collection.Schema, item.Id, failMode);
            if (errors.Count > 0)
            {

                if (failMode == "strict")
                {
                    throw new ConfigException($"Schema validation failed for collection '{collectionName}' with {errors.Count} error(s).", DiagnosticCode.SchemaStrictModeBlocked);
                }

                allErrors.AddRange(errors);
                foreach (var error in errors)
                {
                    input.Logger.Warn($"event=schema.validation code={error.Code} field={error.Field} source={error.SourcePath} collection={collectionName} message={error.Message}");
                }
            }
        }

        return Task.FromResult(new ContentStageOutput(input.Items, input.BodyStore, Name, 0, allErrors));
    }
}
