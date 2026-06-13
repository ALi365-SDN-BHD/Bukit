using YamlDotNet.RepresentationModel;
using Bukit.Theme;
using Bukit.Cli;
using Bukit.Cli.Cli.Binding;

namespace Bukit.Labs.Cli.Commands;

public static class ThemeCommand
{
    public static Task<int> RunAsync(CliBoundCommand command)
        => Task.FromResult(Unknown(command.GetArgument(0) ?? string.Empty));

    internal static async Task<int> CreateAsync(CliBoundCommand command)
    {
        var name = command.GetArgument(1);
        if (!ThemeFileHelper.IsSafeThemeName(name))
        {
            Console.Error.WriteLine("Missing or invalid theme name.");
            return 2;
        }

        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
        var rootDir = resolved.RootDir;
        var from = (command.GetString("--from") ?? "starter").Trim();
        if (!ThemeFileHelper.IsSafeThemeName(from))
        {
            Console.Error.WriteLine("Invalid source theme name.");
            return 2;
        }

        if (string.Equals(name, from, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Source and destination theme names must be different.");
            return 2;
        }

        var force = command.GetBool("--force");
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

        var brand = command.GetString("--brand");
        var primaryColor = command.GetString("--primary-color");
        var accentColor = command.GetString("--accent-color");

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

            ThemeFileHelper.CopyDirectory(sourceRoot, themeRoot);
            ThemeFileHelper.ApplyCssColorOverrides(themeRoot, primaryColor, accentColor);
        }

        Console.WriteLine($"Theme created: {name}");

        if (command.GetBool("--use"))
        {
            var resolvedForSet = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
            return await SetThemeAsync(name!, resolvedForSet.FullConfigPath, resolvedForSet.RootDir, brand, primaryColor, accentColor);
        }

        return 0;
    }

    internal static Task<int> ListAsync(CliBoundCommand command)
    {
        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
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

    internal static Task<int> InfoAsync(CliBoundCommand command)
    {
        var name = ResolveThemeName(command);
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("Missing theme name.");
            return Task.FromResult(2);
        }

        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
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

    internal static Task<int> PreviewAsync(CliBoundCommand command)
    {
        var name = command.GetArgument(1) ?? ResolveThemeName(command);
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("Missing theme name.");
            return Task.FromResult(2);
        }

        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
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
            ThemeInfoPrinter.PrintSections(v2Manifest, themeRoot);
            ThemeInfoPrinter.PrintComponents(v2Manifest);
            ThemeInfoPrinter.PrintTokens(v2Manifest, themeRoot);
            ThemeInfoPrinter.PrintLayouts(v2Manifest, themeRoot);
        }

        ThemeInfoPrinter.PrintFileStats(themeRoot);
        Console.WriteLine($"Local path:   {themeRoot}");
        return Task.FromResult(0);
    }

    internal static Task<int> ParamsAsync(CliBoundCommand command)
    {
        var name = ResolveThemeName(command);
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("Missing theme name.");
            return Task.FromResult(2);
        }

        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
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

    private static string? ResolveThemeName(CliBoundCommand command)
    {
        var raw = command.GetArgument(1);
        if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith('-'))
        {
            return ResolveActiveThemeName(command);
        }

        return raw;
    }

    private static string? ResolveActiveThemeName(CliBoundCommand command)
    {
        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
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

    internal static Task<int> UseAsync(CliBoundCommand command)
    {
        var name = command.GetArgument(1);
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("Missing theme name.");
            return Task.FromResult(2);
        }

        var resolvedForSet = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
        return SetThemeAsync(name, resolvedForSet.FullConfigPath, resolvedForSet.RootDir, brand: null, primaryColor: null, accentColor: null);
    }

    public static Task<int> SetThemeAsync(
        string name,
        string fullConfigPath,
        string rootDir,
        string? brand,
        string? primaryColor,
        string? accentColor)
    {

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

    internal static Task<int> DoctorAsync(CliBoundCommand command)
    {
        var themeRoot = ResolveFullThemeRoot(command);
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

    internal static Task<int> ListComponentsAsync(CliBoundCommand command)
    {
        var themeRoot = ResolveFullThemeRoot(command);
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

    internal static Task<int> ExportCatalogAsync(CliBoundCommand command)
    {
        var themeRoot = ResolveFullThemeRoot(command);
        if (themeRoot is null) return Task.FromResult(2);

        var manifest = ThemeManifestLoader.Load(themeRoot);
        if (manifest is null)
        {
            Console.Error.WriteLine("theme.yaml not found.");
            return Task.FromResult(2);
        }

        var registry = new ThemeComponentRegistry(themeRoot, manifest, null);

        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
        var cacheDir = Path.Combine(resolved.RootDir, ".cache");
        var outputPath = Path.Combine(cacheDir, "theme-catalog.json");

        ThemeCatalogWriter.WriteToFile(manifest, registry, outputPath);
        Console.WriteLine($"Theme catalog exported: {outputPath}");
        return Task.FromResult(0);
    }

    internal static string? ResolveFullThemeRoot(CliBoundCommand command)
    {
        var name = command.GetArgument(1);
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('-'))
        {
            name = ResolveActiveThemeName(command);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("Missing theme name.");
            return null;
        }

        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
        var themeRoot = Path.Combine(resolved.RootDir, "themes", name);
        if (!Directory.Exists(themeRoot))
        {
            Console.Error.WriteLine($"Theme not found: {name}");
            return null;
        }

        return themeRoot;
    }
}
