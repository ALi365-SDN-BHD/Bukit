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
