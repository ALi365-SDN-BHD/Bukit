using System.Security.Cryptography;
using Bukit.Cli.Cli.Binding;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

public static class ThemeRegistryCommand
{
    private const string DefaultRegistryUrl =
        "https://raw.githubusercontent.com/ALi365-SDN-BHD/bukit-themes/main/themes.yaml";

    private const double CacheTtlHours = 24;

    public static async Task<int> SearchAsync(CliBoundCommand command)
    {
        Console.WriteLine("Experimental: theme registry/search/install is not covered by the Bukit 1.0 GA compatibility promise.");

        var query = command.GetArgument(1);
        if (!string.IsNullOrWhiteSpace(query) && query.StartsWith('-'))
            query = null;

        var refresh = command.GetBool("--refresh");
        var registryUrl = command.GetString("--registry-url") ?? DefaultRegistryUrl;

        var load = await LoadRegistryDetailedAsync(registryUrl, refresh);
        PrintDiagnostics(load.Diagnostics);
        var index = load.Index;
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
        Console.WriteLine($"{list.Count} theme(s) found. Experimental install: bukit theme install --registry <name>");

        if (index.Registry?.Updated is not null)
            Console.WriteLine($"Registry updated: {index.Registry.Updated}");

        return 0;
    }

    public static async Task<RegistryThemeEntry?> ResolveAsync(string name, CliBoundCommand command)
    {
        var registryUrl = command.GetString("--registry-url") ?? DefaultRegistryUrl;
        var refresh = command.GetBool("--refresh");
        var load = await LoadRegistryDetailedAsync(registryUrl, refresh);
        PrintDiagnostics(load.Diagnostics);
        var index = load.Index;
        if (index is null) return null;

        return index.Themes.FirstOrDefault(
            t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    internal static async Task<RegistryIndex?> LoadRegistryAsync(string registryUrl, bool forceRefresh)
        => (await LoadRegistryDetailedAsync(registryUrl, forceRefresh)).Index;

    internal static async Task<ThemeRegistryLoadResult> LoadRegistryDetailedAsync(string registryUrl, bool forceRefresh)
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
                var cached = await TryLoadCacheAsync(cacheFile);
                if (cached is not null)
                {
                    return new ThemeRegistryLoadResult(cached, []);
                }
            }
        }

        try
        {
            using var http = CreateSafeHttpClient(TimeSpan.FromSeconds(30));

            var response = await http.GetStringAsync(registryUrl);
            var index = RegistryIndex.Parse(response);
            if (index is null)
            {
                return await TryLoadCacheFallbackAsync(
                    cacheFile,
                    new ThemeRegistryDiagnostic(
                        "BKT-THEME-REGISTRY-0003",
                        "yaml_invalid",
                        "Theme registry YAML is invalid. Falling back to cached registry if available.",
                        IsWarning: false));
            }

            Directory.CreateDirectory(cacheDir);
            await File.WriteAllTextAsync(cacheFile, response);
            return new ThemeRegistryLoadResult(index, []);
        }
        catch (HttpRequestException ex)
        {
            var diagnostic = CreateHttpDiagnostic(ex);
            return await TryLoadCacheFallbackAsync(cacheFile, diagnostic);
        }
        catch (TaskCanceledException)
        {
            return await TryLoadCacheFallbackAsync(
                cacheFile,
                new ThemeRegistryDiagnostic(
                    "BKT-THEME-REGISTRY-0001",
                    "network_blocked",
                    "Theme registry request timed out. Falling back to cached registry if available.",
                    IsWarning: false));
        }
    }

    private static async Task<RegistryIndex?> TryLoadCacheAsync(string cacheFile)
    {
        try
        {
            var cached = await File.ReadAllTextAsync(cacheFile);
            return RegistryIndex.Parse(cached);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<ThemeRegistryLoadResult> TryLoadCacheFallbackAsync(string cacheFile, ThemeRegistryDiagnostic failure)
    {
        if (File.Exists(cacheFile))
        {
            var cached = await TryLoadCacheAsync(cacheFile);
            if (cached is not null)
            {
                return new ThemeRegistryLoadResult(
                    cached,
                    [
                        failure,
                        new ThemeRegistryDiagnostic(
                            "BKT-THEME-REGISTRY-0004",
                            "cache_fallback_used",
                            $"Using cached theme registry from {NormalizePath(cacheFile)}.",
                            IsWarning: true)
                    ]);
            }
        }

        return new ThemeRegistryLoadResult(failure);
    }

    private static ThemeRegistryDiagnostic CreateHttpDiagnostic(HttpRequestException ex)
    {
        if (ex.Message.Contains("SSRF blocked:", StringComparison.OrdinalIgnoreCase))
        {
            return new ThemeRegistryDiagnostic(
                "BKT-THEME-REGISTRY-0002",
                "ssrf_blocked",
                ex.Message,
                IsWarning: false);
        }

        return new ThemeRegistryDiagnostic(
            "BKT-THEME-REGISTRY-0001",
            "network_blocked",
            $"Theme registry request failed: {ex.Message}",
            IsWarning: false);
    }

    private static void PrintDiagnostics(IReadOnlyList<ThemeRegistryDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            var writer = diagnostic.IsWarning ? Console.Out : Console.Error;
            writer.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
        }
    }

    internal static HttpClient CreateSafeHttpClient(TimeSpan timeout)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = SsrfGuard.SsrfSafeConnectAsync
        };
        var http = new HttpClient(handler) { Timeout = timeout };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("bukit-cli");
        return http;
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

    private static string NormalizePath(string path)
        => path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
}

internal sealed record ThemeRegistryLoadResult(
    RegistryIndex? Index,
    IReadOnlyList<ThemeRegistryDiagnostic> Diagnostics)
{
    internal ThemeRegistryLoadResult(params ThemeRegistryDiagnostic[] diagnostics)
        : this(null, diagnostics)
    {
    }
}

internal sealed record ThemeRegistryDiagnostic(
    string Code,
    string Kind,
    string Message,
    bool IsWarning);
