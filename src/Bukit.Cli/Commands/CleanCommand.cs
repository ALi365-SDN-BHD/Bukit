using Bukit.Config;

namespace Bukit.Cli.Commands;

public static class CleanCommand
{
    public static Task<int> RunAsync(ArgReader reader)
    {
        var configPath = reader.GetOption("--config");
        var site = reader.GetOption("--site");
        var dirOption = reader.GetOption("--dir");

        string rootDir;
        string outputDir;
        if (!string.IsNullOrWhiteSpace(configPath) || !string.IsNullOrWhiteSpace(site))
        {
            var resolved = ConfigPathResolver.Resolve(reader);
            rootDir = resolved.RootDir;
            var config = ConfigLoader.Load(resolved.FullConfigPath);
            outputDir = Path.GetFullPath(Path.Combine(rootDir, config.Build.Output));
        }
        else
        {
            rootDir = Directory.GetCurrentDirectory();
            outputDir = Path.GetFullPath(dirOption ?? "dist");
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
