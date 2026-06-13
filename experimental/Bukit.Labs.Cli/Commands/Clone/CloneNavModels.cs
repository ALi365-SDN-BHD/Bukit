namespace Bukit.Labs.Cli.Commands;

public sealed record NavLinkInfo
{
    public string? Label { get; init; }
    public string? Url { get; init; }
}

public sealed record FooterLinkInfo
{
    public string? Label { get; init; }
    public string? Url { get; init; }
}

public sealed record SectionInfo
{
    public string Semantic { get; init; } = "content";
    public string? Heading { get; init; }
    public string? ContentHtml { get; init; }
    public List<string> ImageUrls { get; init; } = [];
    public List<SectionState> States { get; init; } = [];
    public SectionResponsiveInfo? Responsive { get; init; }

    public bool HasStates => States.Count > 0;
    public bool HasResponsive => Responsive is not null;
}

public sealed record SectionState
{
    public string? Label { get; init; }
    public string? ContentHtml { get; init; }
    public string? Screenshot { get; init; }
    public IReadOnlyDictionary<string, string>? ComputedStyles { get; init; }
}

public sealed record SectionResponsiveInfo
{
    public string? ColumnsDesktop { get; init; }
    public string? ColumnsTablet { get; init; }
    public string? ColumnsMobile { get; init; }
    public string? MaxWidthDesktop { get; init; }
    public string? MaxWidthTablet { get; init; }
    public string? MaxWidthMobile { get; init; }
    public IReadOnlyDictionary<string, CloneViewportSectionInfo>? Viewports { get; init; }
}

public sealed record CloneViewportSectionInfo
{
    public CloneBox? Bounds { get; init; }
    public IReadOnlyDictionary<string, string>? Styles { get; init; }
    public string? Screenshot { get; init; }
}
