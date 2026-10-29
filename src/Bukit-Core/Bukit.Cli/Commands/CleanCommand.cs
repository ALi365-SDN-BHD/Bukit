using Bukit.Cli.Shared;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

public static class CleanCommand
{
    public static Task<int> RunAsync(CliBoundCommand command)
        => RunAsync(command, "text");

    internal static Task<int> RunAsync(CliBoundCommand command, string logFormat)
    {
        var configPath = command.GetString("--config");
        var site = command.GetString("--site");
        var dirOption = command.GetString("--dir");
        var usesConfiguredOutput = !string.IsNullOrWhiteSpace(configPath) || !string.IsNullOrWhiteSpace(site);

        string rootDir;
        string outputDir;
        if (usesConfiguredOutput)
        {
            var resolved = ConfigPathResolver.Resolve(configPath, site);
            rootDir = resolved.RootDir;
            var (config, _) = CommandConfigLoader.Load(resolved.FullConfigPath, logFormat, isCi: false);
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

        try
        {
            var metadataDir = Path.Combine(rootDir, ".bukit");
            if (string.Equals(outputDir, metadataDir, StringComparison.OrdinalIgnoreCase)
                || outputDir.StartsWith(metadataDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new ConfigException($"Refusing to clean project metadata directory: {outputDir}. How to fix: select a dedicated output directory like 'dist' or 'public'.", DiagnosticCode.BuildOutputUnsafe);
            }

            OutputDirectoryCleaner.CleanIfExists(rootDir, outputDir);
        }
        catch (ConfigException ex)
        {
            Console.Error.WriteLine(DiagnosticExceptionFormatter.Format(ex));
            return Task.FromResult(2);
        }

        DeleteIfExists(Path.GetFullPath(Path.Combine(rootDir, ".cache")));

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
