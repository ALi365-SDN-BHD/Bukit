using System.Text.Json;

namespace Bukit.Labs.Cli.Commands;

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
            return JsonSerializer.Deserialize(json, CloneInputJsonContext.Default.CloneBehaviors) ?? Default;
        }
        catch (JsonException)
        {
            return Default;
        }
    }
}
