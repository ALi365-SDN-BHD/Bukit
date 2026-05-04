using Bukit.Cli.Intent;

namespace Bukit.Cli.Commands;

public static class IntentCommand
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
            "init" => InitAsync(reader),
            "validate" => ValidateAsync(reader),
            "apply" => ApplyAsync(reader),
            _ => Task.FromResult(Unknown(sub))
        };
    }

    private static Task<int> InitAsync(ArgReader reader)
    {
        var outPath = reader.GetOption("--out") ?? "intent.yaml";
        var fullOutPath = Path.GetFullPath(outPath);
        Bukit.Cli.Intent.IntentWizard.RunInteractive(fullOutPath);
        Console.WriteLine($"Wrote intent: {fullOutPath}");
        Console.WriteLine("Next:");
        Console.WriteLine($"  bukit intent validate \"{fullOutPath}\"");
        Console.WriteLine($"  bukit intent apply \"{fullOutPath}\" --out site.yaml");
        return Task.FromResult(0);
    }

    private static Task<int> ValidateAsync(ArgReader reader)
    {
        var intentPath = reader.GetArg(2);
        if (string.IsNullOrWhiteSpace(intentPath))
        {
            Console.Error.WriteLine("Missing intent path.");
            PrintHelp();
            return Task.FromResult(2);
        }

        var full = Path.GetFullPath(intentPath);
        var rootDir = ResolveRootDir(reader);
        var intent = IntentLoader.Load(full);
        var validation = IntentValidator.Validate(intent, rootDir);
        Print(validation);
        return Task.FromResult(validation.IsValid ? 0 : 1);
    }

    private static Task<int> ApplyAsync(ArgReader reader)
    {
        var intentPath = reader.GetArg(2);
        if (string.IsNullOrWhiteSpace(intentPath))
        {
            Console.Error.WriteLine("Missing intent path.");
            PrintHelp();
            return Task.FromResult(2);
        }

        var outPath = reader.GetOption("--out") ?? "site.yaml";
        var (validation, rootDir) = IntentApplier.Apply(intentPath, outPath);
        Print(validation);
        if (!validation.IsValid)
        {
            return Task.FromResult(1);
        }

        Console.WriteLine($"Wrote config: {Path.GetFullPath(outPath)}");
        if (Path.GetFullPath(outPath).StartsWith(Path.GetFullPath(Path.Combine(rootDir, "sites")) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Tip: use `bukit build --site <name>` for sites/<name>.yaml");
        }

        return Task.FromResult(0);
    }

    private static string ResolveRootDir(ArgReader reader)
    {
        var rootDir = reader.GetOption("--root-dir");
        if (!string.IsNullOrWhiteSpace(rootDir))
        {
            return Path.GetFullPath(rootDir);
        }

        var outPath = reader.GetOption("--out");
        if (string.IsNullOrWhiteSpace(outPath))
        {
            return Directory.GetCurrentDirectory();
        }

        var fullOutPath = Path.GetFullPath(outPath);
        var cwd = Directory.GetCurrentDirectory();
        var sitesDir = Path.GetFullPath(Path.Combine(cwd, "sites"));

        if (fullOutPath.StartsWith(sitesDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return cwd;
        }

        return Path.GetDirectoryName(fullOutPath) ?? cwd;
    }

    private static void Print(IntentValidationResult validation)
    {
        foreach (var e in validation.Errors)
        {
            Console.Error.WriteLine($"✖ {e}");
        }

        foreach (var w in validation.Warnings)
        {
            Console.WriteLine($"⚠ {w}");
        }
    }

    private static int Unknown(string sub)
    {
        Console.Error.WriteLine($"Unknown intent subcommand: {sub}");
        PrintHelp();
        return 2;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("bukit intent");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  bukit intent init [--out <intent.yaml>]");
        Console.WriteLine("  bukit intent validate <intent.yaml> [--root-dir <dir> | --out <path>]");
        Console.WriteLine("  bukit intent apply <intent.yaml> [--out <path>]");
    }
}
