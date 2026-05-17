using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bukit.Cli.Commands;

public sealed record CloneTokens
{
    public string? Bg { get; init; }
    public string? Surface { get; init; }
    public string? SurfaceMuted { get; init; }
    public string? Text { get; init; }
    public string? Muted { get; init; }
    public string? Border { get; init; }
    public string? Primary { get; init; }
    public string? PrimaryStrong { get; init; }
    public string? Accent { get; init; }

    public string? Radius { get; init; }
    public string? ContentMax { get; init; }
    public string? WideMax { get; init; }
    public string? Shadow { get; init; }

    public string? CardShadow { get; init; }
    public string? ModalShadow { get; init; }
    public string? DropdownShadow { get; init; }

    public string? NavPadding { get; init; }
    public string? ContainerPadding { get; init; }
    public string? SectionGap { get; init; }
    public SpacingScale? SpacingScale { get; init; }

    public ResponsiveBreakpoints? ResponsiveBreakpoints { get; init; }

    public string? FontFamily { get; init; }
    public string? HeadingFontFamily { get; init; }
    public string? CodeFontFamily { get; init; }
    public string? GoogleFontsUrl { get; init; }

    public string? HoverLift { get; init; }
    public string? HoverShadow { get; init; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static CloneTokens FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CloneTokens();
        }

        try
        {
            return JsonSerializer.Deserialize<CloneTokensWrapper>(json, JsonOptions)?.Tokens
                   ?? JsonSerializer.Deserialize<CloneTokens>(json, JsonOptions)
                   ?? new CloneTokens();
        }
        catch (JsonException)
        {
            return new CloneTokens();
        }
    }

    private sealed class CloneTokensWrapper
    {
        public CloneTokens? Tokens { get; set; }
    }
}

public sealed record SpacingScale
{
    public string? Xs { get; init; }
    public string? Sm { get; init; }
    public string? Md { get; init; }
    public string? Lg { get; init; }
    public string? Xl { get; init; }
}

public sealed record ResponsiveBreakpoints
{
    public string? Mobile { get; init; }
    public string? Tablet { get; init; }
    public string? Desktop { get; init; }
}

public sealed record CloneLayoutInfo
{
    public string? SiteTitle { get; init; }
    public string? HeroHeading { get; init; }
    public string? HeroSubtext { get; init; }
    public bool HasFeaturesSection { get; init; }
    public bool HasCTASection { get; init; }
    public bool HasHeroCta { get; init; }
    public string? HeroCtaText { get; init; }
    public string? HeroCtaUrl { get; init; }
    public List<NavLinkInfo> NavLinks { get; init; } = [];
    public List<FooterLinkInfo> FooterLinks { get; init; } = [];
    public List<SectionInfo> ExtraSections { get; init; } = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static CloneLayoutInfo Default => new();

    public static CloneLayoutInfo FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Default;
        }

        return JsonSerializer.Deserialize<CloneLayoutInfo>(json, JsonOptions) ?? Default;
    }
}

public sealed record ClonePageInfo
{
    public string? Title { get; init; }
    public string? Slug { get; init; }
    public string? Url { get; init; }
    public string? Summary { get; init; }
    public string? Description { get; init; }
    public string? BodyMarkdown { get; init; }
    public string? ContentHtml { get; init; }
    public ClonePageSeo? Seo { get; init; }
    public List<CloneViewportCapture> Screenshots { get; init; } = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static ClonePageInfo Default => new();

    public static ClonePageInfo FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Default;
        }

        return JsonSerializer.Deserialize<ClonePageInfo>(json, JsonOptions) ?? Default;
    }
}

public sealed record ClonePageSeo
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Image { get; init; }
    public string? Robots { get; init; }
}

public sealed record CloneSectionsDocument
{
    public List<CloneSectionInfo> Sections { get; init; } = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static IReadOnlyList<CloneSectionInfo> FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<CloneSectionInfo>>(json, JsonOptions) ?? [];
        }

        var wrapped = JsonSerializer.Deserialize<CloneSectionsDocument>(json, JsonOptions);
        return wrapped?.Sections ?? [];
    }
}

public sealed record CloneSectionInfo
{
    public string? Id { get; init; }
    public string? Type { get; init; }
    public string? Semantic { get; init; }
    public string? Title { get; init; }
    public string? Heading { get; init; }
    public string? Eyebrow { get; init; }
    public string? Subheading { get; init; }
    public string? Text { get; init; }
    public string? ContentHtml { get; init; }
    public int? Order { get; init; }
    public string? ClassName { get; init; }
    public IReadOnlyDictionary<string, string>? Styles { get; init; }
    public CloneBox? Bounds { get; init; }
    public IReadOnlyDictionary<string, string>? ComputedStyles { get; init; }
    public List<CloneSectionButton> Buttons { get; init; } = [];
    public List<CloneSectionItem> Items { get; init; } = [];
    public List<CloneComponentInfo> Components { get; init; } = [];
    public List<string> ImageUrls { get; init; } = [];
    public List<CloneSectionAsset> Assets { get; init; } = [];
    public List<SectionState> States { get; init; } = [];
    public List<CloneInteractionInfo> Interactions { get; init; } = [];
    public SectionResponsiveInfo? Responsive { get; init; }

    public bool HasStates => States.Count > 0;
    public string DisplayTitle => Title ?? Heading ?? Type ?? Semantic ?? "Section";
}

public sealed record CloneSectionButton
{
    public string? Label { get; init; }
    public string? Url { get; init; }
    public string? Variant { get; init; }
}

public sealed record CloneSectionItem
{
    public string? Title { get; init; }
    public string? Text { get; init; }
    public string? Description { get; init; }
    public string? Url { get; init; }
    public string? Image { get; init; }
    public string? Icon { get; init; }
    public CloneBox? Bounds { get; init; }
    public IReadOnlyDictionary<string, string>? ComputedStyles { get; init; }
}

public sealed record CloneSectionAsset
{
    public string Type { get; init; } = "content";
    public string Src { get; init; } = "";
    public string? Alt { get; init; }
    public string? LocalPath { get; init; }
    public string? Media { get; init; }
    public string? Width { get; init; }
    public string? Height { get; init; }
}

public sealed record CloneComponentInfo
{
    public string? Id { get; init; }
    public string? Type { get; init; }
    public string? Selector { get; init; }
    public string? Text { get; init; }
    public string? Html { get; init; }
    public CloneBox? Bounds { get; init; }
    public IReadOnlyDictionary<string, string>? ComputedStyles { get; init; }
    public List<SectionState> States { get; init; } = [];
    public List<CloneInteractionInfo> Interactions { get; init; } = [];
}

public sealed record CloneInteractionInfo
{
    public string? Type { get; init; }
    public string? Trigger { get; init; }
    public string? Target { get; init; }
    public string? Description { get; init; }
    public IReadOnlyDictionary<string, string>? States { get; init; }
}

public sealed record CloneBox
{
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? Width { get; init; }
    public double? Height { get; init; }
}

public sealed record CloneViewportCapture
{
    public string? Name { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public string? Screenshot { get; init; }
}

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

public sealed record CloneIcon
{
    public string Name { get; init; } = "icon";
    public string Svg { get; init; } = "";
    public string? Width { get; init; }
    public string? Height { get; init; }
}

public sealed record CloneAsset
{
    public string Type { get; init; } = "content";
    public string Src { get; init; } = "";
    public string? Alt { get; init; }
    public string? LocalPath { get; init; }
    public string? Media { get; init; }
    public string? Width { get; init; }
    public string? Height { get; init; }
    public string? Integrity { get; init; }
    public string? Failure { get; init; }
}

public sealed record CloneGenerationSummary
{
    public int FileCount { get; init; }
    public int BehaviorCount { get; init; }
    public int IconCount { get; init; }
    public int AssetCount { get; init; }
    public int SectionCount { get; init; }
    public int ContentFileCount { get; init; }
    public int DataFileCount { get; init; }
    public bool ConfigUpdated { get; init; }
    public bool VerifyPassed { get; init; }
    public List<string> Warnings { get; init; } = [];
}

public sealed record CloneBehaviors
{
    public bool StickyHeader { get; init; }
    public bool CardHoverLift { get; init; }
    public bool AnimateOnScroll { get; init; }
    public bool ScrollShrinkNav { get; init; }

    public bool DarkModeToggle { get; init; }
    public bool MobileHamburger { get; init; }
    public bool SmoothScroll { get; init; }
    public bool BackToTop { get; init; }

    public bool HasModal { get; init; }
    public bool HasDropdown { get; init; }
    public bool HasTabs { get; init; }

    public string? AnimationStyle { get; init; }
    public int ScrollThreshold { get; init; } = 60;
    public bool UseLenis { get; init; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static CloneBehaviors Default => new();

    public bool HasExtraPartials => HasModal || HasDropdown || HasTabs;

    public bool HasAnyCssBehavior =>
        StickyHeader || ScrollShrinkNav || CardHoverLift || AnimateOnScroll || MobileHamburger || DarkModeToggle || HasModal || HasDropdown || HasTabs;

    public bool HasAnyJsBehavior =>
        ScrollShrinkNav || DarkModeToggle || MobileHamburger || SmoothScroll || BackToTop || HasModal || HasDropdown || HasTabs || UseLenis;

    public static CloneBehaviors FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Default;
        }

        try
        {
            return JsonSerializer.Deserialize<CloneBehaviors>(json, JsonOptions) ?? Default;
        }
        catch (JsonException)
        {
            return Default;
        }
    }
}

internal static class CloneModels
{
    public static bool IsSafeThemeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (name is "." or "..")
        {
            return false;
        }

        return !Path.IsPathRooted(name) &&
               name.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) < 0;
    }
}
