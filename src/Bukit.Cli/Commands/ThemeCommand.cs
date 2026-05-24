using YamlDotNet.RepresentationModel;
using Bukit.Theme;

namespace Bukit.Cli.Commands;

public static class ThemeCommand
{
    public static Task<int> RunAsync(ArgReader reader)
    {
        var sub = reader.GetArg(1);
        if (string.IsNullOrWhiteSpace(sub))
        {
            return Task.FromResult(2);
        }

        return sub switch
        {
            "create" => CreateAsync(reader),
            "list" => ListAsync(reader),
            "use" => UseAsync(reader),
            "info" => InfoAsync(reader),
            "params" => ParamsAsync(reader),
            "preview" => PreviewAsync(reader),
            "wizard" => ThemeWizardCommand.RunAsync(reader),
            "pack" => ThemePackCommand.RunAsync(reader),
            "install" => ThemeInstallCommand.RunAsync(reader),
            "search" => ThemeRegistryCommand.SearchAsync(reader),
            "doctor" => DoctorAsync(reader),
            "list-components" => ListComponentsAsync(reader),
            "export-catalog" => ExportCatalogAsync(reader),
            _ => Task.FromResult(Unknown(sub))
        };
    }

    private static async Task<int> CreateAsync(ArgReader reader)
    {
        var name = reader.GetArg(2);
        if (!IsSafeThemeName(name))
        {
            Console.Error.WriteLine("Missing or invalid theme name.");
            return 2;
        }

        var resolved = ConfigPathResolver.Resolve(reader);
        var rootDir = resolved.RootDir;
        var from = (reader.GetOption("--from") ?? "starter").Trim();
        if (!IsSafeThemeName(from))
        {
            Console.Error.WriteLine("Invalid source theme name.");
            return 2;
        }

        if (string.Equals(name, from, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Source and destination theme names must be different.");
            return 2;
        }

        var force = reader.HasFlag("--force");
        var themesDir = Path.Combine(rootDir, "themes");
        var themeRoot = Path.Combine(themesDir, name!);
        if (Directory.Exists(themeRoot))
        {
            if (!force)
            {
                Console.Error.WriteLine($"Theme already exists: {name}");
                return 2;
            }

            Directory.Delete(themeRoot, recursive: true);
        }

        var brand = reader.GetOption("--brand");
        var primaryColor = reader.GetOption("--primary-color");
        var accentColor = reader.GetOption("--accent-color");

        if (string.Equals(from, "starter", StringComparison.OrdinalIgnoreCase))
        {
            StarterThemeScaffold.WriteTo(rootDir, name!, primaryColor, accentColor);
        }
        else
        {
            var sourceRoot = Path.Combine(themesDir, from);
            if (!Directory.Exists(sourceRoot))
            {
                Console.Error.WriteLine($"Source theme not found: {from}");
                return 2;
            }

            CopyDirectory(sourceRoot, themeRoot);
            ApplyCssColorOverrides(themeRoot, primaryColor, accentColor);
        }

        Console.WriteLine($"Theme created: {name}");

        if (reader.HasFlag("--use"))
        {
            return await SetThemeAsync(name!, reader, brand, primaryColor, accentColor);
        }

        return 0;
    }

    private static Task<int> ListAsync(ArgReader reader)
    {
        var resolved = ConfigPathResolver.Resolve(reader);
        var rootDir = resolved.RootDir;

        var themesDir = Path.Combine(rootDir, "themes");
        if (!Directory.Exists(themesDir))
        {
            Console.WriteLine("No themes directory found. Create one with: bukit theme create <name>");
            return Task.FromResult(0);
        }

        var themeDirs = Directory.GetDirectories(themesDir)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hasAny = false;
        foreach (var dir in themeDirs)
        {
            var name = Path.GetFileName(dir);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var layouts = Path.Combine(dir, "layouts");
            var assets = Path.Combine(dir, "assets");
            var stat = Path.Combine(dir, "static");
            if (!Directory.Exists(layouts) && !Directory.Exists(assets) && !Directory.Exists(stat))
            {
                continue;
            }

            hasAny = true;
            var manifest = ThemeManifest.Load(dir);
            var version = manifest?.Version ?? "—";
            var desc = manifest?.Description ?? "";
            if (!string.IsNullOrEmpty(desc) && desc.Length > 42)
            {
                desc = desc[..40] + "..";
            }

            var tags = "";
            if (manifest?.Tags is { Count: > 0 })
            {
                tags = "[" + string.Join(", ", manifest.Tags) + "]";
            }
            else if (manifest?.DeclaredParamCount > 0)
            {
                tags = $"[params: {manifest.DeclaredParamCount}]";
            }

            Console.WriteLine($"  {name,-14} {version,-8} {desc,-42} {tags}");
        }

        if (!hasAny)
        {
            Console.WriteLine("No themes found. Create one with: bukit theme create <name>");
        }

        return Task.FromResult(0);
    }

    private static Task<int> InfoAsync(ArgReader reader)
    {
        var name = ResolveThemeName(reader);
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("Missing theme name.");
            return Task.FromResult(2);
        }

        var resolved = ConfigPathResolver.Resolve(reader);
        var rootDir = resolved.RootDir;
        var themeRoot = Path.Combine(rootDir, "themes", name);
        if (!Directory.Exists(themeRoot))
        {
            Console.Error.WriteLine($"Theme not found: {name}");
            return Task.FromResult(2);
        }

        var manifest = ThemeManifest.Load(themeRoot);
        Console.WriteLine($"Name:        {manifest?.Name ?? name}");
        Console.WriteLine($"Version:     {manifest?.Version ?? "—"}");
        Console.WriteLine($"Author:      {manifest?.Author ?? "—"}");
        Console.WriteLine($"License:     {manifest?.License ?? "—"}");
        Console.WriteLine($"Homepage:    {manifest?.Homepage ?? "—"}");
        Console.WriteLine($"Requires:    {manifest?.RequiresBukit ?? "—"}");
        Console.WriteLine($"Description: {manifest?.Description ?? "—"}");

        if (manifest?.Tags is { Count: > 0 })
        {
            Console.WriteLine($"Tags:        {string.Join(", ", manifest.Tags)}");
        }

        if (manifest is { Params.Count: > 0 })
        {
            Console.WriteLine();
            Console.WriteLine("Declared parameters:");
            foreach (var p in manifest.Params)
            {
                var def = p.Default is not null ? $" (default: {p.Default})" : "";
                Console.WriteLine($"  {p.Key,-20} {p.Type ?? "string",-10} {p.Label}{def}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Template files:");
        var layoutsDir = Path.Combine(themeRoot, "layouts");
        if (Directory.Exists(layoutsDir))
        {
            var files = Directory.GetFiles(layoutsDir, "*", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                var relative = Path.GetRelativePath(layoutsDir, file);
                Console.WriteLine($"  {relative}");
            }
        }

        return Task.FromResult(0);
    }

    private static Task<int> PreviewAsync(ArgReader reader)
    {
        var name = reader.GetArg(2) ?? ResolveThemeName(reader);
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("Missing theme name.");
            return Task.FromResult(2);
        }

        var resolved = ConfigPathResolver.Resolve(reader);
        var themeRoot = Path.Combine(resolved.RootDir, "themes", name);
        if (!Directory.Exists(themeRoot))
        {
            Console.Error.WriteLine($"Theme not found: {name}");
            return Task.FromResult(2);
        }

        var manifest = ThemeManifest.Load(themeRoot);
        Console.WriteLine($"Theme preview: {manifest?.Name ?? name}");
        Console.WriteLine($"Version:      {manifest?.Version ?? "—"}");
        Console.WriteLine($"Description:  {manifest?.Description ?? "—"}");
        Console.WriteLine($"Homepage:     {manifest?.Homepage ?? "—"}");
        Console.WriteLine($"Thumbnail:    {manifest?.Thumbnail ?? "—"}");
        if (manifest?.Tags is { Count: > 0 })
        {
            Console.WriteLine($"Tags:         {string.Join(", ", manifest.Tags)}");
        }

        var v2Manifest = ThemeManifestLoader.Load(themeRoot);
        if (v2Manifest is not null)
        {
            PrintSections(v2Manifest, themeRoot);
            PrintComponents(v2Manifest);
            PrintTokens(v2Manifest, themeRoot);
            PrintLayouts(v2Manifest, themeRoot);
        }

        PrintFileStats(themeRoot);
        Console.WriteLine($"Local path:   {themeRoot}");
        return Task.FromResult(0);
    }

    private static void PrintSections(ThemeManifestV2 manifest, string themeRoot)
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

    private static void PrintComponents(ThemeManifestV2 manifest)
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

    private static void PrintTokens(ThemeManifestV2 manifest, string themeRoot)
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

    private static void PrintLayouts(ThemeManifestV2 manifest, string themeRoot)
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

    private static void PrintFileStats(string themeRoot)
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

    private static Task<int> ParamsAsync(ArgReader reader)
    {
        var name = ResolveThemeName(reader);
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("Missing theme name.");
            return Task.FromResult(2);
        }

        var resolved = ConfigPathResolver.Resolve(reader);
        var rootDir = resolved.RootDir;
        var themeRoot = Path.Combine(rootDir, "themes", name);
        if (!Directory.Exists(themeRoot))
        {
            Console.Error.WriteLine($"Theme not found: {name}");
            return Task.FromResult(2);
        }

        var manifest = ThemeManifest.Load(themeRoot);
        if (manifest?.Params is not { Count: > 0 })
        {
            Console.WriteLine($"No parameters declared in theme '{name}'.");
            Console.WriteLine("Add a 'params' section to themes/<name>/theme.yaml to declare parameters.");
            return Task.FromResult(0);
        }

        Console.WriteLine($"Parameters for theme '{name}':");
        foreach (var p in manifest.Params)
        {
            var def = p.Default is not null ? $" (default: {p.Default})" : "";
            Console.WriteLine($"  {p.Key,-22} {p.Type ?? "string",-10} {p.Label}{def}");
        }

        return Task.FromResult(0);
    }

    private static string? ResolveThemeName(ArgReader reader)
    {
        var raw = reader.GetArg(2);
        if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith('-'))
        {
            return ResolveActiveThemeName(reader);
        }

        return raw;
    }

    private static string? ResolveActiveThemeName(ArgReader reader)
    {
        var resolved = ConfigPathResolver.Resolve(reader);
        if (!File.Exists(resolved.FullConfigPath))
        {
            return null;
        }

        try
        {
            var yaml = File.ReadAllText(resolved.FullConfigPath);
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));
            if (stream.Documents.Count > 0 &&
                stream.Documents[0].RootNode is YamlMappingNode root &&
                root.Children.TryGetValue(new YamlScalarNode("theme"), out var themeNode) &&
                themeNode is YamlMappingNode themeMap &&
                themeMap.Children.TryGetValue(new YamlScalarNode("name"), out var nameNode) &&
                nameNode is YamlScalarNode nameScalar)
            {
                return nameScalar.Value;
            }
        }
        catch
        {
        }

        return null;
    }

    private static Task<int> UseAsync(ArgReader reader)
    {
        var name = reader.GetArg(2);
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("Missing theme name.");
            return Task.FromResult(2);
        }

        return SetThemeAsync(name, reader, brand: null, primaryColor: null, accentColor: null);
    }

    internal static Task<int> SetThemeAsync(
        string name,
        ArgReader reader,
        string? brand,
        string? primaryColor,
        string? accentColor)
    {
        var resolved = ConfigPathResolver.Resolve(reader);
        var fullConfigPath = resolved.FullConfigPath;
        var rootDir = resolved.RootDir;

        var themesDir = Path.Combine(rootDir, "themes");
        var themeRoot = Path.Combine(themesDir, name);
        if (!Directory.Exists(themeRoot))
        {
            Console.Error.WriteLine($"Theme not found: {name}");
            return Task.FromResult(2);
        }

        if (!File.Exists(fullConfigPath))
        {
            Console.Error.WriteLine($"Config not found: {fullConfigPath}");
            return Task.FromResult(2);
        }

        var yaml = File.ReadAllText(fullConfigPath);
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            root = new YamlMappingNode();
            stream.Documents.Clear();
            stream.Documents.Add(new YamlDocument(root));
        }

        var themeNode = GetOrCreateMapping(root, "theme");
        themeNode.Children[new YamlScalarNode("name")] = new YamlScalarNode(name);
        var hasParams =
            !string.IsNullOrWhiteSpace(brand) ||
            !string.IsNullOrWhiteSpace(primaryColor) ||
            !string.IsNullOrWhiteSpace(accentColor);
        var paramsNode = hasParams ? GetOrCreateMapping(themeNode, "params") : null;
        if (!string.IsNullOrWhiteSpace(brand))
        {
            paramsNode!.Children[new YamlScalarNode("brand")] = new YamlScalarNode(brand);
            paramsNode.Children[new YamlScalarNode("footer_text")] = new YamlScalarNode(brand);
        }

        if (!string.IsNullOrWhiteSpace(primaryColor))
        {
            paramsNode!.Children[new YamlScalarNode("primary_color")] = new YamlScalarNode(primaryColor);
        }

        if (!string.IsNullOrWhiteSpace(accentColor))
        {
            paramsNode!.Children[new YamlScalarNode("accent_color")] = new YamlScalarNode(accentColor);
        }

        using var writer = new StringWriter();
        stream.Save(writer, assignAnchors: false);
        File.WriteAllText(fullConfigPath, writer.ToString());

        Console.WriteLine($"Theme set: {name}");
        return Task.FromResult(0);
    }

    private static YamlMappingNode GetOrCreateMapping(YamlMappingNode parent, string key)
    {
        var k = new YamlScalarNode(key);
        if (parent.Children.TryGetValue(k, out var existing) && existing is YamlMappingNode map)
        {
            return map;
        }

        var created = new YamlMappingNode();
        parent.Children[k] = created;
        return created;
    }

    private static int Unknown(string sub)
    {
        Console.Error.WriteLine($"Unknown theme subcommand: {sub}");
        return 2;
    }

    private static Task<int> DoctorAsync(ArgReader reader)
    {
        var themeRoot = ResolveFullThemeRoot(reader);
        if (themeRoot is null) return Task.FromResult(2);

        var manifest = ThemeManifestLoader.Load(themeRoot);
        if (manifest is null)
        {
            Console.Error.WriteLine("theme.yaml not found. Doctor requires a componentized theme.");
            return Task.FromResult(2);
        }

        ThemeComponentRegistry? parentRegistry = null;
        if (!string.IsNullOrWhiteSpace(manifest.Extends))
        {
            var parentRoot = Path.Combine(Path.GetDirectoryName(themeRoot)!, manifest.Extends);
            var parentManifest = ThemeManifestLoader.Load(parentRoot);
            if (parentManifest is not null)
            {
                parentRegistry = new ThemeComponentRegistry(parentRoot, parentManifest, null);
            }
        }

        var registry = new ThemeComponentRegistry(themeRoot, manifest, parentRegistry);
        var result = ThemeDoctorCommand.Diagnose(themeRoot, manifest, registry);
        ThemeDoctorCommand.PrintReport(result);
        return Task.FromResult(0);
    }

    private static Task<int> ListComponentsAsync(ArgReader reader)
    {
        var themeRoot = ResolveFullThemeRoot(reader);
        if (themeRoot is null) return Task.FromResult(2);

        var manifest = ThemeManifestLoader.Load(themeRoot);
        if (manifest is null)
        {
            Console.Error.WriteLine("theme.yaml not found.");
            return Task.FromResult(2);
        }

        var registry = new ThemeComponentRegistry(themeRoot, manifest, null);

        Console.WriteLine();
        Console.WriteLine("Sections:");
        foreach (var name in registry.GetAllSectionNames().OrderBy(n => n))
        {
            var def = registry.ResolveSection(name);
            var desc = def?.Description ?? "";
            if (desc.Length > 50) desc = desc[..48] + "..";
            Console.WriteLine($"  {name,-24} {desc}");
        }

        Console.WriteLine();
        Console.WriteLine("Components:");
        foreach (var name in registry.GetAllComponentNames().OrderBy(n => n))
        {
            var def = registry.ResolveComponent(name);
            var props = def?.Props is not null ? string.Join(", ", def.Props.Keys) : "";
            Console.WriteLine($"  {name,-24} props: [{props}]");
        }

        return Task.FromResult(0);
    }

    private static Task<int> ExportCatalogAsync(ArgReader reader)
    {
        var themeRoot = ResolveFullThemeRoot(reader);
        if (themeRoot is null) return Task.FromResult(2);

        var manifest = ThemeManifestLoader.Load(themeRoot);
        if (manifest is null)
        {
            Console.Error.WriteLine("theme.yaml not found.");
            return Task.FromResult(2);
        }

        var registry = new ThemeComponentRegistry(themeRoot, manifest, null);

        var resolved = ConfigPathResolver.Resolve(reader);
        var cacheDir = Path.Combine(resolved.RootDir, ".cache");
        var outputPath = Path.Combine(cacheDir, "theme-catalog.json");

        ThemeCatalogWriter.WriteToFile(manifest, registry, outputPath);
        Console.WriteLine($"Theme catalog exported: {outputPath}");
        return Task.FromResult(0);
    }

    private static string? ResolveFullThemeRoot(ArgReader reader)
    {
        var name = reader.GetArg(2);
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('-'))
        {
            name = ResolveActiveThemeName(reader);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("Missing theme name.");
            return null;
        }

        var resolved = ConfigPathResolver.Resolve(reader);
        var themeRoot = Path.Combine(resolved.RootDir, "themes", name);
        if (!Directory.Exists(themeRoot))
        {
            Console.Error.WriteLine($"Theme not found: {name}");
            return null;
        }

        return themeRoot;
    }

    private static bool IsSafeThemeName(string? name)
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

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (var dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, dir);
            Directory.CreateDirectory(Path.Combine(destinationDir, relative));
        }

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var destination = Path.Combine(destinationDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private static void ApplyCssColorOverrides(string themeRoot, string? primaryColor, string? accentColor)
    {
        var stylePath = Path.Combine(themeRoot, "assets", "style.css");
        if (!File.Exists(stylePath))
        {
            return;
        }

        var css = File.ReadAllText(stylePath);
        var updated = StarterThemeScaffold.ApplyColorOverrides(css, primaryColor, accentColor);
        if (!string.Equals(css, updated, StringComparison.Ordinal))
        {
            File.WriteAllText(stylePath, updated);
        }
    }
}
