using Bukit.Cli.Shared;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Config;

namespace Bukit.Cli.Commands;

public static class CleanCommand
{
    public static Task<int> RunAsync(CliBoundCommand command)
    {
        var configPath = command.GetString("--config");
        var site = command.GetString("--site");
        var dirOption = command.GetString("--dir");

        string rootDir;
        string outputDir;
        if (!string.IsNullOrWhiteSpace(configPath) || !string.IsNullOrWhiteSpace(site))
        {
            var resolved = ConfigPathResolver.Resolve(configPath, site);
            rootDir = resolved.RootDir;
            var config = ConfigLoader.Load(resolved.FullConfigPath);
            outputDir = Path.GetFullPath(Path.Combine(rootDir, config.Build.Output));
        }
        else
        {
            rootDir = Directory.GetCurrentDirectory();
            var dirValue = dirOption ?? "dist";
            var safeRoot = Path.GetFullPath(rootDir) + Path.DirectorySeparatorChar;
            outputDir = Path.GetFullPath(Path.Combine(rootDir, dirValue));
            if (!outputDir.StartsWith(safeRoot, Bukit.Shared.PlatformPathHelper.PathComparison))
            {
                Console.Error.WriteLine("--dir must be inside the current directory.");
                return Task.FromResult(2);
            }
        }

        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, recursive: true);
        }

        DeleteIfExists(Path.GetFullPath(Path.Combine(rootDir, ".cache")));
        DeleteIfExists(Path.GetFullPath(Path.Combine(rootDir, ".bukit")));

        Console.WriteLine($"Cleaned: {outputDir}");
        return Task.FromResult(0);
    }

    private static void DeleteIfExists(string dir)
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
