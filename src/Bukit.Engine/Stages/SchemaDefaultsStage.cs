namespace Bukit.Engine.Stages;

internal sealed class SchemaDefaultsStage : IContentStage
{
    public string Name => "SchemaDefaults";

    public Task<ContentStageOutput> ExecuteAsync(ContentStageInput input, CancellationToken cancellationToken)
    {
        var items = ContentSchemaValidator.ApplyDefaults(input.Config.Site.Collections, input.Items);
        return Task.FromResult(new ContentStageOutput(items, input.BodyStore, Name, 0, null));
    }
}
