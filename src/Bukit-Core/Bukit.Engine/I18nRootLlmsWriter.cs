using Bukit.Engine.Plugins.BuiltIn;

namespace Bukit.Engine;

internal sealed class I18nRootLlmsWriter : II18nRootProjectionWriter
{
    public string Name => "llms";

    public IReadOnlyList<string> RepresentationKinds => ["llms", "llms-full"];

    public void Write(I18nRootProjectionWriterContext context, PublishRepresentation representation)
    {
        switch (representation.Kind)
        {
            case "llms":
                GenerateRootLlms(context);
                break;
            case "llms-full":
                GenerateRootLlmsFull(context);
                break;
        }
    }

    private static void GenerateRootLlms(I18nRootProjectionWriterContext context)
    {
        if (!context.Config.Site.Seo.Geo.Enabled || !context.Config.Site.Seo.Geo.LlmsTxt)
        {
            return;
        }

        var state = I18nMergedVariantState.Create(context.Results);
        LlmsTxtPlugin.WriteLlmsTxt(
            context.Config,
            context.OutputDir,
            context.RootBaseUrl,
            state.RoutedDocuments,
            state.DerivedDocuments,
            state.SeoIndex,
            state.SeoModels,
            context.Config.Site.Seo.Geo);
    }

    private static void GenerateRootLlmsFull(I18nRootProjectionWriterContext context)
    {
        if (!context.Config.Site.Seo.Geo.Enabled || !context.Config.Site.Seo.Geo.LlmsFullTxt)
        {
            return;
        }

        var state = I18nMergedVariantState.Create(context.Results);
        LlmsTxtPlugin.WriteLlmsFullTxt(
            context.Config,
            context.OutputDir,
            context.RootBaseUrl,
            state.RoutedDocuments,
            state.DerivedDocuments,
            state.ContentGraph,
            state.SeoIndex,
            state.BodyStore);
    }
}
