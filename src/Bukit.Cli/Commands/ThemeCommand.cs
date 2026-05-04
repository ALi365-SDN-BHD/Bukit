using YamlDotNet.RepresentationModel;

namespace Bukit.Cli.Commands;

public static class ThemeCommand
{
    public static Task<int> RunAsync(ArgReader reader)
    {
        var sub = reader.GetArg(1);
        if (string.IsNullOrWhiteSpace(sub) || sub is "help" or "--help" or "-h")
        {
            PrintHelp();
            return Task.FromResult(0);
        }

        return sub switch
        {
            "list" => ListAsync(reader),
            "use" => UseAsync(reader),
            _ => Task.FromResult(Unknown(sub))
        };
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
            PrintHelp();
            return Task.FromResult(2);
        }

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
        PrintHelp();
        return 2;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("bukit theme");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  bukit theme list [--config <path> | --site <name>]");
        Console.WriteLine("  bukit theme use <name> [--config <path> | --site <name>]");
    }
}
