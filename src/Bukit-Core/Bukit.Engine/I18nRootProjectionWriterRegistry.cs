namespace Bukit.Engine;

internal interface II18nRootProjectionWriter
{
    string Name { get; }

    IReadOnlyList<string> RepresentationKinds { get; }

    void Write(I18nRootProjectionWriterContext context, PublishRepresentation representation);
}

internal sealed record I18nRootProjectionPlanEntry(
    PublishRepresentation Representation,
    II18nRootProjectionWriter Writer);

internal sealed class I18nRootProjectionWriterRegistry
{
    private readonly IReadOnlyList<II18nRootProjectionWriter> _writers;

    private I18nRootProjectionWriterRegistry(IReadOnlyList<II18nRootProjectionWriter> writers)
    {
        _writers = writers;
    }

    internal IReadOnlyList<II18nRootProjectionWriter> Writers => _writers;

    internal static I18nRootProjectionWriterRegistry CreateDefault()
        => new(
        [
            new I18nRootSitemapWriter(),
            new I18nRootFeedWriter(),
            new I18nRootSearchWriter(),
            new I18nRootLlmsWriter(),
            new I18nRootRobotsWriter(),
            new I18nRootAgentManifestWriter()
        ]);

    internal IReadOnlyList<I18nRootProjectionPlanEntry> BuildPlan(
        IEnumerable<PublishRepresentation> representations)
    {
        var plan = new List<I18nRootProjectionPlanEntry>();
        foreach (var representation in representations)
        {
            var writer = Resolve(representation);
            if (writer is not null)
            {
                plan.Add(new I18nRootProjectionPlanEntry(representation, writer));
            }
        }

        return plan;
    }

    internal II18nRootProjectionWriter? Resolve(PublishRepresentation representation)
        => _writers.FirstOrDefault(writer =>
            writer.RepresentationKinds.Contains(representation.Kind, StringComparer.OrdinalIgnoreCase));
}
