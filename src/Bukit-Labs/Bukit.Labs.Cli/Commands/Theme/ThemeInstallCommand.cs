using System.Formats.Tar;
using System.IO.Compression;
using Bukit.Cli.Shared;
using Bukit.Cli.Shared.Cli.Binding;

namespace Bukit.Labs.Cli.Commands;

public static class ThemeInstallCommand
{
    public static async Task<int> RunAsync(CliBoundCommand command)
    {
        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
        var rootDir = resolved.RootDir;
        var themesDir = Path.Combine(rootDir, "themes");
        var force = command.GetBool("--force");

        var registryName = command.GetString("--registry");
        if (!string.IsNullOrWhiteSpace(registryName))
        {
            return await InstallFromRegistryAsync(registryName, themesDir, force, command);
        }

        var source = command.GetArgument(1);
        if (string.IsNullOrWhiteSpace(source) || source.StartsWith('-'))
        {
            Console.Error.WriteLine("Missing source. Usage: bukit theme install <path|url>  or  bukit theme install --registry <name> (Experimental)");
            return 2;
        }

        Directory.CreateDirectory(themesDir);

        if (source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return await InstallFromUrlAsync(source, themesDir, force);
        }

        return await InstallFromArchiveAsync(source, themesDir, force);
    }

    private static async Task<int> InstallFromRegistryAsync(string name, string themesDir, bool force, CliBoundCommand command)
    {
        Console.WriteLine("Experimental: theme registry/search/install is not covered by the Bukit 1.0 GA compatibility promise.");
        Console.WriteLine($"Looking up '{name}' in registry...");

        var entry = await ThemeRegistryCommand.ResolveAsync(name, command);
        if (entry is null)
        {
            Console.Error.WriteLine($"Theme '{name}' not found in registry.");
            return 2;
        }

        if (entry.Download?.Url is null)
        {
            Console.Error.WriteLine($"Theme '{name}' has no download URL in registry.");
            return 2;
        }

        Console.WriteLine($"Found: {entry.Name} v{entry.Version} by {entry.Author ?? "unknown"}");

        if (!CloneModels.IsSafeThemeName(entry.Name))
        {
            Console.Error.WriteLine($"Theme registry entry has unsafe name: {entry.Name}");
            return 2;
        }

        var themeDest = ResolveThemeDestination(themesDir, entry.Name);
        if (Directory.Exists(themeDest))
        {
            if (!force)
            {
                Console.Error.WriteLine($"Theme already exists: {entry.Name}. Use --force to overwrite.");
                return 2;
            }

            Directory.Delete(themeDest, recursive: true);
        }

        Console.WriteLine($"Downloading: {entry.Download.Url}");

        try
        {
            using var http = ThemeRegistryCommand.CreateSafeHttpClient(TimeSpan.FromMinutes(5));

            var tempFile = Path.GetTempFileName() + ".tar.gz";
            try
            {
                var response = await http.GetAsync(entry.Download.Url, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Download failed: HTTP {(int)response.StatusCode}");
                    return 2;
                }

                await using var fs = File.Create(tempFile);
                await response.Content.CopyToAsync(fs);
                await fs.FlushAsync();

                if (!string.IsNullOrWhiteSpace(entry.Download.Sha256))
                {
                    Console.Write("Verifying SHA256... ");
                    var ok = await ThemeRegistryCommand.VerifySha256Async(tempFile, entry.Download.Sha256);
                    if (!ok)
                    {
                        Console.WriteLine("FAIL");
                        Console.Error.WriteLine("SHA256 mismatch. The download may be corrupted.");
                        return 2;
                    }

                    Console.WriteLine("OK");
                }

                var result = await ExtractAndInstallAsync(tempFile, themesDir, force, entry.Name);
                if (result == 0)
                {
                    Console.WriteLine($"Activate: bukit theme use {entry.Name}");
                }

                return result;
            }
            finally
            {
                DeleteFileBestEffort(tempFile);
            }
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Download failed: {ex.Message}");
            return 2;
        }
    }

    private static async Task<int> InstallFromUrlAsync(string url, string themesDir, bool force)
    {
        Console.WriteLine($"Downloading: {url}");

        try
        {
            using var http = ThemeRegistryCommand.CreateSafeHttpClient(TimeSpan.FromMinutes(5));

            var tempFile = Path.GetTempFileName() + ".tar.gz";
            try
            {
                var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    Console.Error.WriteLine($"Download failed: HTTP {(int)response.StatusCode}");
                    return 2;
                }

                await using var fs = File.Create(tempFile);
                await response.Content.CopyToAsync(fs);
                await fs.FlushAsync();

                return await ExtractAndInstallAsync(tempFile, themesDir, force, themeName: null);
            }
            finally
            {
                DeleteFileBestEffort(tempFile);
            }
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Download failed: {ex.Message}");
            return 2;
        }
    }

    private static async Task<int> InstallFromArchiveAsync(string sourcePath, string themesDir, bool force)
    {
        var fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath))
        {
            Console.Error.WriteLine($"File not found: {fullSourcePath}");
            return 2;
        }

        return await ExtractAndInstallAsync(fullSourcePath, themesDir, force, themeName: null);
    }

    private static async Task<int> ExtractAndInstallAsync(string archivePath, string themesDir, bool force, string? themeName)
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "bukit-extract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpDir);

        try
        {
            using var fileStream = File.OpenRead(archivePath);
            using var gzip = new GZipStream(fileStream, CompressionMode.Decompress);
            using var reader = new TarReader(gzip);

            TarEntry? entry;
            while ((entry = await reader.GetNextEntryAsync()) is not null)
            {
                if (entry.EntryType is TarEntryType.Directory) continue;

                var entryPath = entry.Name.TrimStart('/');
                if (string.IsNullOrWhiteSpace(entryPath)) continue;

                var destPath = Path.GetFullPath(Path.Combine(tmpDir, entryPath));
                if (!destPath.StartsWith(tmpDir + Path.DirectorySeparatorChar, Bukit.Shared.PlatformPathHelper.PathComparison)) continue;

                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrWhiteSpace(destDir)) Directory.CreateDirectory(destDir);

                await entry.ExtractToFileAsync(destPath, overwrite: true);
            }
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine($"Invalid archive: {ex.Message}");
            return 2;
        }
        finally
        {
        }

        themeName ??= DetectThemeName(tmpDir);
        if (string.IsNullOrWhiteSpace(themeName))
        {
            var baseName = Path.GetFileNameWithoutExtension(archivePath);
            if (baseName.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
                baseName = baseName[..^4];
            themeName = baseName.Length > 0 ? baseName : "installed-theme";
        }

        if (!CloneModels.IsSafeThemeName(themeName))
        {
            Console.Error.WriteLine($"Unsafe theme name: {themeName}");
            return 2;
        }

        var themeDest = ResolveThemeDestination(themesDir, themeName);
        var result = InstallExtractedDir(tmpDir, themeDest, force, themeName);

        DeleteDirectoryBestEffort(tmpDir);

        return result;
    }

    private static void DeleteFileBestEffort(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Warning: failed to delete temporary file '{path}': {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"Warning: failed to delete temporary file '{path}': {ex.Message}");
        }
    }

    private static void DeleteDirectoryBestEffort(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Warning: failed to delete temporary directory '{path}': {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"Warning: failed to delete temporary directory '{path}': {ex.Message}");
        }
    }

    private static string ResolveThemeDestination(string themesDir, string themeName)
    {
        var fullThemesDir = Path.GetFullPath(themesDir);
        var fullDest = Path.GetFullPath(Path.Combine(fullThemesDir, themeName));
        var safeRoot = fullThemesDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullDest.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Theme destination escapes themes directory: {themeName}");
        }

        return fullDest;
    }

    private static string? DetectThemeName(string extractedDir)
    {
        var themeYamlPath = Path.Combine(extractedDir, "theme.yaml");
        if (!File.Exists(themeYamlPath))
            themeYamlPath = Directory.GetFiles(extractedDir, "theme.yaml", SearchOption.AllDirectories).FirstOrDefault();

        if (themeYamlPath is not null)
        {
            var manifest = ThemeManifest.Load(Path.GetDirectoryName(themeYamlPath)!);
            return manifest?.Name;
        }

        var dirs = Directory.GetDirectories(extractedDir);
        if (dirs.Length == 1)
        {
            var innerName = Path.GetFileName(dirs[0]);
            if (Directory.Exists(Path.Combine(dirs[0], "layouts")) &&
                innerName is not ("src" or "dist" or "build" or "node_modules" or ".git"))
                return innerName;
        }

        return null;
    }

    private static int InstallExtractedDir(string sourceDir, string destDir, bool force, string themeName)
    {
        if (Directory.Exists(destDir))
        {
            if (!force)
            {
                Console.Error.WriteLine($"Theme already exists: {themeName}. Use --force to overwrite.");
                return 2;
            }

            Directory.Delete(destDir, recursive: true);
        }

        if (!Directory.Exists(Path.Combine(sourceDir, "layouts")))
        {
            foreach (var inner in Directory.GetDirectories(sourceDir))
            {
                if (Directory.Exists(Path.Combine(inner, "layouts")))
                {
                    sourceDir = inner;
                    break;
                }
            }
        }

        CopyDirectory(sourceDir, destDir);
        Console.WriteLine($"Theme installed: {themeName}");
        Console.WriteLine($"Location: themes/{themeName}/");
        return 0;
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
}
