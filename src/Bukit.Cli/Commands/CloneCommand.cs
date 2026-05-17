using System.Text.Json;
using Bukit.Cli.Cli.Binding;

namespace Bukit.Cli.Commands;

public static class CloneCommand
{
    public static Task<int> RunAsync(ArgReader reader)
    {
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["--tokens"] = reader.GetOption("--tokens"),
            ["--layout"] = reader.GetOption("--layout"),
            ["--theme"] = reader.GetOption("--theme"),
            ["--brand"] = reader.GetOption("--brand"),
            ["--behaviors"] = reader.GetOption("--behaviors"),
        };
        if (reader.HasFlag("--use")) options["--use"] = "true";
        if (reader.HasFlag("--force")) options["--force"] = "true";

        return RunAsync(new CliBoundCommand(
            options
                .Where(x => x.Value is not null)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>()),
            reader);
    }

    public static async Task<int> RunAsync(CliBoundCommand command)
    {
        var configPath = command.GetString("--config");
        var site = command.GetString("--site");
        string rootDir;

        if (!string.IsNullOrWhiteSpace(configPath))
        {
            var fullConfigPath = Path.GetFullPath(configPath);
            rootDir = Path.GetDirectoryName(fullConfigPath) ?? Directory.GetCurrentDirectory();
        }
        else if (!string.IsNullOrWhiteSpace(site))
        {
            rootDir = Directory.GetCurrentDirectory();
        }
        else
        {
            var defaultFullConfigPath = Path.GetFullPath("site.yaml");
            rootDir = Path.GetDirectoryName(defaultFullConfigPath) ?? Directory.GetCurrentDirectory();
        }

        return await RunCoreAsync(command, rootDir, null);
    }

    private static async Task<int> RunAsync(CliBoundCommand command, ArgReader reader)
    {
        var resolved = ConfigPathResolver.Resolve(reader);
        return await RunCoreAsync(command, resolved.RootDir, reader);
    }

    private static async Task<int> RunCoreAsync(CliBoundCommand command, string rootDir, ArgReader? reader)
    {
        var tokensPath = command.GetString("--tokens");
        var layoutPath = command.GetString("--layout");
        var themeName = command.GetString("--theme") ?? "cloned";
        var brand = command.GetString("--brand");
        var behaviorsPath = command.GetString("--behaviors");
        var use = command.GetBool("--use");
        var force = command.GetBool("--force");

        if (string.IsNullOrWhiteSpace(tokensPath))
        {
            Console.Error.WriteLine("Missing required option: --tokens <file>");
            return 2;
        }

        if (!CloneModels.IsSafeThemeName(themeName))
        {
            Console.Error.WriteLine("Invalid theme name.");
            return 2;
        }

        var themeDir = Path.Combine(rootDir, "themes", themeName);
        if (Directory.Exists(themeDir))
        {
            if (!force)
            {
                Console.Error.WriteLine($"Theme already exists: {themeName}. Use --force to overwrite.");
                return 2;
            }

            Directory.Delete(themeDir, recursive: true);
        }

        var tokensFullPath = Path.GetFullPath(tokensPath);
        if (!File.Exists(tokensFullPath))
        {
            Console.Error.WriteLine($"Tokens file not found: {tokensFullPath}");
            return 2;
        }

        CloneTokens tokens;
        try
        {
            var tokensJson = await File.ReadAllTextAsync(tokensFullPath);
            tokens = CloneTokens.FromJson(tokensJson);
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Failed to parse tokens file: {ex.Message}");
            return 2;
        }

        CloneLayoutInfo layout;
        if (layoutPath is not null)
        {
            var layoutFullPath = Path.GetFullPath(layoutPath);
            if (!File.Exists(layoutFullPath))
            {
                Console.Error.WriteLine($"Layout file not found: {layoutFullPath}");
                return 2;
            }

            try
            {
                var layoutJson = await File.ReadAllTextAsync(layoutFullPath);
                layout = CloneLayoutInfo.FromJson(layoutJson);
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"Failed to parse layout file: {ex.Message}");
                return 2;
            }
        }
        else
        {
            layout = CloneLayoutInfo.Default;
        }

        CloneBehaviors behaviors;
        if (behaviorsPath is not null)
        {
            var behaviorsFullPath = Path.GetFullPath(behaviorsPath);
            if (!File.Exists(behaviorsFullPath))
            {
                Console.Error.WriteLine($"Behaviors file not found: {behaviorsFullPath}");
                return 2;
            }

            try
            {
                var behaviorsJson = await File.ReadAllTextAsync(behaviorsFullPath);
                behaviors = CloneBehaviors.FromJson(behaviorsJson);
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"Failed to parse behaviors file: {ex.Message}");
                return 2;
            }
        }
        else
        {
            behaviors = CloneBehaviors.Default;
        }

        CloneThemeGenerator.WriteTo(rootDir, themeName, tokens, layout, brand, behaviors);
        Console.WriteLine($"Theme cloned: {themeName}");

        if (use && reader is not null)
        {
            return await ThemeCommand.SetThemeAsync(themeName, reader,
                brand: brand, primaryColor: tokens.Primary, accentColor: tokens.Accent);
        }

        return 0;
    }
}
