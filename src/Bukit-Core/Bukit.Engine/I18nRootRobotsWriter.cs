namespace Bukit.Engine;

internal sealed class I18nRootRobotsWriter : II18nRootProjectionWriter
{
    public IReadOnlyList<string> RepresentationKinds => ["robots"];

    public void Write(I18nRootProjectionWriterContext context, PublishRepresentation representation)
    {
        _ = representation;
        var seoIndex = I18nMergedVariantState.BuildSeoIndex(context.Results);
        RobotsTxtWriter.WriteIfRequested(
            context.Config,
            context.OutputDir,
            context.RootBaseUrl,
            seoIndex);
    }
}
