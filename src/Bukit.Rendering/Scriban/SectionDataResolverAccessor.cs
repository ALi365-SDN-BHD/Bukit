using Bukit.Engine.Abstractions.Content;
using Bukit.Theme;

namespace Bukit.Rendering.Scriban;

public sealed class SectionDataResolverAccessor
{
    internal IReadOnlyList<ContentItem>? AllItems { get; set; }
    internal ThemeComponentRegistry? Registry { get; set; }

    public IReadOnlyList<ContentItem>? ResolveData(PageSectionDefinition sectionDef)
    {
        return null;
    }
}
