using Bukit.Config;

namespace Bukit.Engine;

internal sealed class I18nRootSearchWriter : II18nRootProjectionWriter
{
    public IReadOnlyList<string> RepresentationKinds => ["search"];

    public void Write(I18nRootProjectionWriterContext context, PublishRepresentation representation)
    {
        _ = representation;
        var searchMode = SiteModeResolver.ResolveSearchMode(context.Config.Site);
        var builder = context.SearchIndexBuilder ?? new DefaultSearchIndexBuilder();
        if (searchMode == "merged")
        {
            builder.GenerateMergedSearchIndex(
                context.OutputDir,
                context.Results,
                context.Config.Site.SearchIncludeDerived,
                context.Config.Site.Search.MaxContentLength);
        }
        else if (searchMode == "index")
        {
            builder.GenerateSearchIndexIndex(context.OutputDir, context.Results);
        }
    }
}
