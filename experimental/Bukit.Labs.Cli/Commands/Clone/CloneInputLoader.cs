using System.Text.Json;

namespace Bukit.Labs.Cli.Commands;

internal static class CloneInputLoader
{
    public static async Task<(CloneTokens value, int errorCode)> LoadTokensAsync(string tokensPath)
    {
        var tokensFullPath = Path.GetFullPath(tokensPath);
        if (!File.Exists(tokensFullPath))
        {
            Console.Error.WriteLine($"Tokens file not found: {tokensFullPath}");
            return (new CloneTokens(), 2);
        }

        try
        {
            var tokensJson = await File.ReadAllTextAsync(tokensFullPath);
            var (parsed, tokenError) = CloneTokens.FromJson(tokensJson);
            if (tokenError is not null)
            {
                Console.Error.WriteLine($"Failed to parse tokens file: {tokenError}");
                return (new CloneTokens(), 2);
            }

            return (parsed, 0);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Failed to read tokens file: {ex.Message}");
            return (new CloneTokens(), 2);
        }
    }

    public static async Task<(CloneLayoutInfo value, int errorCode)> LoadLayoutAsync(string? layoutPath)
    {
        if (layoutPath is null)
            return (CloneLayoutInfo.Default, 0);

        var layoutFullPath = Path.GetFullPath(layoutPath);
        if (!File.Exists(layoutFullPath))
        {
            Console.Error.WriteLine($"Layout file not found: {layoutFullPath}");
            return (CloneLayoutInfo.Default, 2);
        }

        try
        {
            var layoutJson = await File.ReadAllTextAsync(layoutFullPath);
            return (CloneLayoutInfo.FromJson(layoutJson), 0);
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Failed to parse layout file: {ex.Message}");
            return (CloneLayoutInfo.Default, 2);
        }
    }

    public static async Task<(CloneBehaviors value, int errorCode)> LoadBehaviorsAsync(string? behaviorsPath)
    {
        if (behaviorsPath is null)
            return (CloneBehaviors.Default, 0);

        var behaviorsFullPath = Path.GetFullPath(behaviorsPath);
        if (!File.Exists(behaviorsFullPath))
        {
            Console.Error.WriteLine($"Behaviors file not found: {behaviorsFullPath}");
            return (CloneBehaviors.Default, 2);
        }

        try
        {
            var behaviorsJson = await File.ReadAllTextAsync(behaviorsFullPath);
            return (CloneBehaviors.FromJson(behaviorsJson), 0);
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Failed to parse behaviors file: {ex.Message}");
            return (CloneBehaviors.Default, 2);
        }
    }

    public static async Task<(List<CloneIcon> value, int errorCode)> LoadIconsAsync(string? iconsPath)
    {
        if (iconsPath is null)
            return ([], 0);

        var iconsFullPath = Path.GetFullPath(iconsPath);
        if (!File.Exists(iconsFullPath))
        {
            Console.Error.WriteLine($"Icons file not found: {iconsFullPath}");
            return ([], 2);
        }

        try
        {
            var iconsJson = await File.ReadAllTextAsync(iconsFullPath);
            var list = JsonSerializer.Deserialize(iconsJson, CloneInputJsonContext.Default.ListCloneIcon) ?? [];
            return (list, 0);
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Failed to parse icons file: {ex.Message}");
            return ([], 2);
        }
    }

    public static async Task<(List<CloneAsset> value, int errorCode)> LoadAssetsAsync(string? assetsPath)
    {
        if (assetsPath is null)
            return ([], 0);

        var assetsFullPath = Path.GetFullPath(assetsPath);
        if (!File.Exists(assetsFullPath))
        {
            Console.Error.WriteLine($"Assets file not found: {assetsFullPath}");
            return ([], 2);
        }

        try
        {
            var assetsJson = await File.ReadAllTextAsync(assetsFullPath);
            var list = JsonSerializer.Deserialize(assetsJson, CloneInputJsonContext.Default.ListCloneAsset) ?? [];
            return (list, 0);
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Failed to parse assets file: {ex.Message}");
            return ([], 2);
        }
    }

    internal static async Task<ClonePageInfo> LoadPageAsync(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Page file not found: {fullPath}", fullPath);
        }

        var json = await File.ReadAllTextAsync(fullPath);
        return ClonePageInfo.FromJson(json);
    }

    internal static async Task<IReadOnlyList<CloneSectionInfo>> LoadSectionsAsync(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Sections file not found: {fullPath}", fullPath);
        }

        var json = await File.ReadAllTextAsync(fullPath);
        return CloneSectionsDocument.FromJson(json);
    }
}
