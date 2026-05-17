using System.Text;
using System.Text.Json;

namespace Bukit.Cli.Commands;

internal static class CloneResearchWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static void WriteTo(
        string rootDir,
        CloneTokens tokens,
        ClonePageInfo page,
        IReadOnlyList<CloneSectionInfo> sections,
        IReadOnlyList<CloneAsset> assets,
        CloneBehaviors? behaviors,
        IReadOnlyDictionary<string, string> assetMap)
    {
        WriteFile(rootDir, "docs/research/DESIGN_TOKENS.md", DesignTokens(tokens));
        WriteFile(rootDir, "docs/research/PAGE_TOPOLOGY.md", PageTopology(page, sections, assets, assetMap));
        WriteFile(rootDir, "docs/research/BEHAVIORS.md", Behaviors(sections, behaviors));

        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            var name = CloneContentWriter.SectionSpecFileName(section, i);
            WriteFile(rootDir, $"docs/research/components/{name}", ComponentSpec(section, i + 1, assetMap));
        }
    }

    private static string DesignTokens(CloneTokens tokens)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Design Tokens");
        sb.AppendLine();
        AppendValue(sb, "Background", tokens.Bg);
        AppendValue(sb, "Surface", tokens.Surface);
        AppendValue(sb, "Surface muted", tokens.SurfaceMuted);
        AppendValue(sb, "Text", tokens.Text);
        AppendValue(sb, "Muted", tokens.Muted);
        AppendValue(sb, "Border", tokens.Border);
        AppendValue(sb, "Primary", tokens.Primary);
        AppendValue(sb, "Accent", tokens.Accent);
        AppendValue(sb, "Radius", tokens.Radius);
        AppendValue(sb, "Content max", tokens.ContentMax);
        AppendValue(sb, "Wide max", tokens.WideMax);
        AppendValue(sb, "Shadow", tokens.Shadow);
        AppendValue(sb, "Font family", tokens.FontFamily);
        AppendValue(sb, "Heading font family", tokens.HeadingFontFamily);
        AppendValue(sb, "Google fonts", tokens.GoogleFontsUrl);
        if (tokens.ResponsiveBreakpoints is not null)
        {
            sb.AppendLine();
            sb.AppendLine("## Breakpoints");
            AppendValue(sb, "Mobile", tokens.ResponsiveBreakpoints.Mobile);
            AppendValue(sb, "Tablet", tokens.ResponsiveBreakpoints.Tablet);
            AppendValue(sb, "Desktop", tokens.ResponsiveBreakpoints.Desktop);
        }
        return sb.ToString();
    }

    private static string PageTopology(ClonePageInfo page, IReadOnlyList<CloneSectionInfo> sections, IReadOnlyList<CloneAsset> assets, IReadOnlyDictionary<string, string> assetMap)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Page Topology");
        sb.AppendLine();
        AppendValue(sb, "Title", page.Title ?? page.Seo?.Title);
        AppendValue(sb, "Source URL", page.Url);
        AppendValue(sb, "Description", page.Description ?? page.Seo?.Description);
        if (page.Screenshots.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Viewports");
            foreach (var shot in page.Screenshots)
                sb.AppendLine($"- {shot.Name ?? shot.Width?.ToString() ?? "viewport"}: {shot.Width}x{shot.Height}, `{shot.Screenshot}`");
        }

        sb.AppendLine();
        sb.AppendLine("## Sections");
        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            sb.AppendLine($"- {i + 1}. `{section.Type ?? section.Semantic ?? "rich_section"}`: {section.DisplayTitle}");
        }

        sb.AppendLine();
        sb.AppendLine("## Assets");
        foreach (var asset in assets)
        {
            var local = assetMap.TryGetValue(asset.Src, out var value) ? value : asset.LocalPath;
            sb.AppendLine($"- `{asset.Type}` {asset.Src} -> `{local}`");
        }
        return sb.ToString();
    }

    private static string Behaviors(IReadOnlyList<CloneSectionInfo> sections, CloneBehaviors? behaviors)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Behaviors");
        sb.AppendLine();
        if (behaviors is not null)
            sb.AppendLine("```json\n" + JsonSerializer.Serialize(behaviors, JsonOptions) + "\n```");

        sb.AppendLine();
        sb.AppendLine("## Section Interactions");
        foreach (var section in sections)
        {
            if (section.Interactions.Count == 0 && section.Components.All(c => c.Interactions.Count == 0))
                continue;
            sb.AppendLine($"### {section.DisplayTitle}");
            foreach (var interaction in section.Interactions)
                sb.AppendLine($"- {interaction.Trigger ?? interaction.Type ?? "interaction"} -> {interaction.Target}: {interaction.Description}");
            foreach (var component in section.Components)
            foreach (var interaction in component.Interactions)
                sb.AppendLine($"- `{component.Selector ?? component.Type ?? component.Id}` {interaction.Trigger ?? interaction.Type} -> {interaction.Target}: {interaction.Description}");
        }
        return sb.ToString();
    }

    private static string ComponentSpec(CloneSectionInfo section, int order, IReadOnlyDictionary<string, string> assetMap)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Section {order:000}: {section.DisplayTitle}");
        sb.AppendLine();
        AppendValue(sb, "Type", section.Type ?? section.Semantic);
        AppendValue(sb, "ID", section.Id);
        AppendValue(sb, "Heading", section.Heading ?? section.Title);
        AppendValue(sb, "Subheading", section.Subheading);
        AppendJson(sb, "Bounds", section.Bounds);
        AppendJson(sb, "Computed Styles", section.ComputedStyles);
        AppendJson(sb, "Inline Styles", section.Styles);
        AppendJson(sb, "Responsive", section.Responsive);
        AppendJson(sb, "States", section.States.Count == 0 ? null : section.States);
        AppendJson(sb, "Interactions", section.Interactions.Count == 0 ? null : section.Interactions);
        AppendJson(sb, "Components", section.Components.Count == 0 ? null : section.Components);
        var localAssets = section.ImageUrls.Concat(section.Assets.Select(a => a.Src))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(x => assetMap.TryGetValue(x, out var local) ? local : x)
            .ToList();
        AppendJson(sb, "Assets", localAssets.Count == 0 ? null : localAssets);
        if (!string.IsNullOrWhiteSpace(section.ContentHtml))
        {
            sb.AppendLine();
            sb.AppendLine("## Content HTML");
            sb.AppendLine("```html");
            sb.AppendLine(section.ContentHtml);
            sb.AppendLine("```");
        }
        return sb.ToString();
    }

    private static void AppendValue(StringBuilder sb, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            sb.AppendLine($"- {label}: `{value}`");
    }

    private static void AppendJson(StringBuilder sb, string label, object? value)
    {
        if (value is null)
            return;
        sb.AppendLine();
        sb.AppendLine($"## {label}");
        sb.AppendLine("```json");
        sb.AppendLine(JsonSerializer.Serialize(value, JsonOptions));
        sb.AppendLine("```");
    }

    private static void WriteFile(string rootDir, string relativePath, string content)
    {
        var path = Path.Combine(rootDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
