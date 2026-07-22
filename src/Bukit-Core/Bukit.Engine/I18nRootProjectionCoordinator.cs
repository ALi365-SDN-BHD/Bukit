using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Rendering;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed record I18nRootProjectionWriterContext(
    AppConfig Config,
    string OutputDir,
    string RootBaseUrl,
    IReadOnlyList<BuildVariantResult> Results,
    ILogger Logger);

internal static class I18nRootProjectionCoordinator
{
    private static readonly I18nRootProjectionWriterRegistry WriterRegistry =
        I18nRootProjectionWriterRegistry.CreateDefault();

    internal static IReadOnlyList<PublishProjectionResult> GenerateRootOutputs(
        AppConfig config,
        string outputDir,
        string rootBaseUrl,
        IReadOnlyList<BuildVariantResult> results,
        ILogger logger)
    {
        var projectionContext = new PublishProjectionContext(
            Config: config,
            OutputDir: outputDir,
            ContentGraph: CanonicalContentGraph.Empty,
            SeoIndex: new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase),
            SeoModels: new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase),
            RoutedDocuments: Array.Empty<RoutedContentDocument>(),
            BaseUrl: rootBaseUrl,
            Logger: logger,
            VariantResults: results,
            DerivedDocuments: Array.Empty<RoutedContentDocument>());
        var writerContext = ToWriterContext(projectionContext);

        return WriterRegistry.BuildPlan(PublishRepresentationRegistry.AggregateRepresentations())
            .Select(entry => Project(writerContext, entry.Representation, entry.Writer))
            .ToArray();
    }

    internal static PublishProjectionResult Project(
        PublishProjectionContext context,
        PublishRepresentation representation)
    {
        var writerContext = ToWriterContext(context);
        var writer = WriterRegistry.Resolve(representation);
        if (writer is not null)
        {
            writer.Write(writerContext, representation);
        }

        return new PublishProjectionResult(
            representation,
            I18nRootProjectionInventory.BuildOutputs(
                writerContext.OutputDir,
                representation,
                writerContext.Results));
    }

    private static PublishProjectionResult Project(
        I18nRootProjectionWriterContext context,
        PublishRepresentation representation,
        II18nRootProjectionWriter writer)
    {
        writer.Write(context, representation);
        return new PublishProjectionResult(
            representation,
            I18nRootProjectionInventory.BuildOutputs(context.OutputDir, representation, context.Results));
    }

    private static I18nRootProjectionWriterContext ToWriterContext(PublishProjectionContext context)
        => new(
            context.Config,
            context.OutputDir,
            context.BaseUrl,
            context.VariantResults ?? Array.Empty<BuildVariantResult>(),
            context.Logger ?? new ConsoleLogger(LogLevel.Error));
}
