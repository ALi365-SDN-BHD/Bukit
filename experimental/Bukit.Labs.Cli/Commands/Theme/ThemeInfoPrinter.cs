using Bukit.Theme;

namespace Bukit.Labs.Cli.Commands;

internal static class ThemeInfoPrinter
{
    internal static void PrintSections(ThemeManifestV2 manifest, string themeRoot)
    {
        if (manifest.Sections is not { Count: > 0 }) return;

        Console.WriteLine();
        Console.WriteLine($"Sections ({manifest.Sections.Count}):");
        foreach (var (name, section) in manifest.Sections.OrderBy(s => s.Key))
        {
            var desc = section.Description ?? "";
            if (desc.Length > 54) desc = desc[..52] + "..";
            var plugin = section.Plugin is not null ? $" [plugin: {section.Plugin}]" : "";
            Console.WriteLine($"  {name,-24} {desc}{plugin}");
        }
    }

    internal static void PrintComponents(ThemeManifestV2 manifest)
    {
        if (manifest.Components is not { Count: > 0 }) return;

        Console.WriteLine();
        Console.WriteLine($"Components ({manifest.Components.Count}):");
        foreach (var (name, comp) in manifest.Components.OrderBy(c => c.Key))
        {
            var props = comp.Props is { Count: > 0 }
                ? $" props: [{string.Join(", ", comp.Props.Keys)}]"
                : "";
            Console.WriteLine($"  {name,-24}{props}");
        }
    }

    internal static void PrintTokens(ThemeManifestV2 manifest, string themeRoot)
    {
        var tokensPath = !string.IsNullOrWhiteSpace(manifest.Tokens)
            ? Path.Combine(themeRoot, manifest.Tokens)
            : Path.Combine(themeRoot, "tokens.yaml");

        if (!File.Exists(tokensPath)) return;

        var loader = new ThemeTokensLoader();
        var tokens = loader.Load(themeRoot, tokensPath);
        if (tokens is null) return;

        var groups = new List<string>();
        if (tokens.Colors is { Count: > 0 }) groups.Add($"colors ({tokens.Colors.Count})");
        if (tokens.Font is { Count: > 0 }) groups.Add($"font ({tokens.Font.Count})");
        if (tokens.Radius is { Count: > 0 }) groups.Add($"radius ({tokens.Radius.Count})");
        if (tokens.Spacing is { Count: > 0 }) groups.Add($"spacing ({tokens.Spacing.Count})");
        if (tokens.Layout is { Count: > 0 }) groups.Add($"layout ({tokens.Layout.Count})");

        if (groups.Count == 0) return;

        Console.WriteLine();
        Console.WriteLine($"Design tokens: {string.Join(", ", groups)}");

        if (tokens.Colors is { Count: > 0 })
        {
            var sampleCount = Math.Min(tokens.Colors.Count, 8);
            var samples = tokens.Colors.Take(sampleCount)
                .Select(kv => $"  {kv.Key}: {kv.Value}")
                .ToList();
            Console.WriteLine("  Color samples:");
            foreach (var s in samples) Console.WriteLine(s);
            if (tokens.Colors.Count > 8)
                Console.WriteLine($"  ... and {tokens.Colors.Count - 8} more");
        }
    }

    internal static void PrintLayouts(ThemeManifestV2 manifest, string themeRoot)
    {
        var layoutsDir = Path.Combine(themeRoot, "layouts");
        if (!Directory.Exists(layoutsDir)) return;

        var layoutFiles = Directory.GetFiles(layoutsDir, "*.scriban", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(layoutsDir, "*.html", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(layoutsDir, "*.sbn", SearchOption.AllDirectories))
            .Distinct()
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(f => Path.GetRelativePath(layoutsDir, f))
            .ToList();

        if (layoutFiles.Count == 0) return;

        Console.WriteLine();
        Console.WriteLine($"Layout templates ({layoutFiles.Count}):");
        foreach (var f in layoutFiles)
            Console.WriteLine($"  {f}");
    }

    internal static void PrintFileStats(string themeRoot)
    {
        var assetCount = 0;
        var assetsDir = Path.Combine(themeRoot, "assets");
        if (Directory.Exists(assetsDir))
            assetCount = Directory.GetFiles(assetsDir, "*", SearchOption.AllDirectories).Length;

        var staticCount = 0;
        var staticDir = Path.Combine(themeRoot, "static");
        if (Directory.Exists(staticDir))
            staticCount = Directory.GetFiles(staticDir, "*", SearchOption.AllDirectories).Length;

        if (assetCount > 0 || staticCount > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"Assets: {assetCount} files  |  Static: {staticCount} files");
        }
    }
}
