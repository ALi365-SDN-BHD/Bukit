using Bukit.Engine.Abstractions.Content;
using Bukit.Theme;

namespace Bukit.Rendering.Scriban;

public sealed class SectionDataResolverAccessor
{
    internal IReadOnlyList<ContentDocument>? AllDocuments { get; set; }
    internal ThemeComponentRegistry? Registry { get; set; }

    public IReadOnlyList<ContentDocument>? ResolveData(PageSectionDefinition sectionDef)
    {
        return null;
    }
}
