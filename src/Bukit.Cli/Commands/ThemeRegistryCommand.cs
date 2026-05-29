using System.Security.Cryptography;
using Bukit.Cli.Cli.Binding;

namespace Bukit.Cli.Commands;

public static class ThemeRegistryCommand
{
    private const string DefaultRegistryUrl =
        "https://raw.githubusercontent.com/ALi365-SDN-BHD/bukit-themes/main/themes.yaml";

    private const double CacheTtlHours = 24;

    public static Task<int> SearchAsync(ArgReader reader)
    {
        var registry = BukitCliSpecs.CreateRegistry();
        var parentSpec = registry.Resolve("theme");
        var subSpec = registry.ResolveSubcommand(parentSpec!, "search");
        var command = CliBoundCommandFactory.Create(reader, subSpec);
        return SearchAsync(command);
    }

    public static async Task<int> SearchAsync(CliBoundCommand command)
    {
        var query = command.GetArgument(1);
        if (!string.IsNullOrWhiteSpace(query) && query.StartsWith('-'))
            query = null;

        var refresh = command.GetBool("--refresh");
        var registryUrl = command.GetString("--registry-url") ?? DefaultRegistryUrl;

        var index = await LoadRegistryAsync(registryUrl, refresh);
        if (index is null)
        {
            Console.Error.WriteLine("Failed to load theme registry.");
            Console.Error.WriteLine($"  URL: {registryUrl}");
            return 1;
        }

        if (index.Themes.Count == 0)
        {
            Console.WriteLine("No themes in registry.");
            return 0;
        }

        var results = index.Themes.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            results = results.Where(t =>
                t.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (t.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                t.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase)));
        }

        var list = results.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();

        if (list.Count == 0)
        {
            Console.WriteLine($"No themes matching '{query}'.");
            return 0;
        }

        Console.WriteLine();
        foreach (var t in list)
        {
            var version = t.Version.Length > 8 ? t.Version[..8] : t.Version;
            var desc = t.Description ?? "";
            if (desc.Length > 42) desc = desc[..40] + "..";
            var tags = t.Tags.Count > 0 ? "[" + string.Join(", ", t.Tags.Take(3)) + "]" : "";

            Console.WriteLine($"  {t.Name,-18} v{version,-8} {desc,-42} {tags}");
        }

        Console.WriteLine();
        Console.WriteLine($"{list.Count} theme(s) found. Install: bukit theme install --registry <name>");

        if (index.Registry?.Updated is not null)
            Console.WriteLine($"Registry updated: {index.Registry.Updated}");

        return 0;
    }

    public static async Task<RegistryThemeEntry?> ResolveAsync(string name, CliBoundCommand command)
    {
        var registryUrl = command.GetString("--registry-url") ?? DefaultRegistryUrl;
        var refresh = command.GetBool("--refresh");
        var index = await LoadRegistryAsync(registryUrl, refresh);
        if (index is null) return null;

        return index.Themes.FirstOrDefault(
            t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    internal static async Task<RegistryIndex?> LoadRegistryAsync(string registryUrl, bool forceRefresh)
    {
        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".bukit", "registry");
        var cacheFile = Path.Combine(cacheDir, "themes.yaml");

        if (!forceRefresh && File.Exists(cacheFile))
        {
            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(cacheFile);
            if (age < TimeSpan.FromHours(CacheTtlHours))
            {
                try
                {
                    var cached = await File.ReadAllTextAsync(cacheFile);
                    return RegistryIndex.Parse(cached);
                }
                catch
                {
                }
            }
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("bukit-cli");

            var response = await http.GetStringAsync(registryUrl);
            var index = RegistryIndex.Parse(response);
            if (index is not null)
            {
                Directory.CreateDirectory(cacheDir);
                await File.WriteAllTextAsync(cacheFile, response);
            }

            return index;
        }
        catch
        {
            if (File.Exists(cacheFile))
            {
                try
                {
                    var cached = await File.ReadAllTextAsync(cacheFile);
                    return RegistryIndex.Parse(cached);
                }
                catch
                {
                }
            }

            return null;
        }
    }

    public static async Task<bool> VerifySha256Async(string filePath, string expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256)) return true;

        try
        {
            using var stream = File.OpenRead(filePath);
            using var sha = SHA256.Create();
            var hash = await sha.ComputeHashAsync(stream);
            var actual = Convert.ToHexStringLower(hash);
            return string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    internal static string CacheFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".bukit", "registry", "themes.yaml");
}
