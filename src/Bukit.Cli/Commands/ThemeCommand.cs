using YamlDotNet.RepresentationModel;

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
            return Task.FromResult(0);
        }

        var themeDirs = Directory.GetDirectories(themesDir)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

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

            Console.WriteLine(name);
        }

        return Task.FromResult(0);
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

    private static Task<int> SetThemeAsync(
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
