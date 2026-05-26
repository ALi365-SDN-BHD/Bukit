using Bukit.Config;
using Bukit.Content;
using Bukit.Shared;

namespace Bukit.Engine.Stages;

public interface IContentStage
{
    string Name { get; }

    Task<ContentStageOutput> ExecuteAsync(ContentStageInput input, CancellationToken cancellationToken);
}

public sealed record ContentStageInput(
    IReadOnlyList<ContentItem> Items,
    IContentBodyStore BodyStore,
    AppConfig Config,
    ConfigOverrides Overrides,
    string RootDir,
    string MediaCacheDir,
    ILogger Logger);

public sealed record ContentStageOutput(
    IReadOnlyList<ContentItem> Items,
    IContentBodyStore BodyStore,
    string StageName,
    long DurationMs,
    IReadOnlyList<ContentSchemaValidator.SchemaValidationError>? SchemaErrors);
