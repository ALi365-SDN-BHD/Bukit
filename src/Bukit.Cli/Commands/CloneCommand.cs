using Bukit.Cli.Cli.Binding;

namespace Bukit.Cli.Commands;

public static class CloneCommand
{
    public static Task<int> RunAsync(ArgReader reader)
    {
        var command = CloneCommandOptions.BuildCommand(reader);
        return RunAsync(command, reader);
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

        var (options, errorCode) = CloneCommandOptions.Parse(command);
        if (options is null) return errorCode;
        return await RunCoreAsync(options, rootDir, command, reader: null);
    }

    private static async Task<int> RunAsync(CliBoundCommand command, ArgReader reader)
    {
        var resolved = ConfigPathResolver.Resolve(reader);
        var (options, errorCode) = CloneCommandOptions.Parse(command);
        if (options is null) return errorCode;
        return await RunCoreAsync(options, resolved.RootDir, command, reader);
    }

    private static async Task<int> RunCoreAsync(CloneCommandOptions options, string rootDir, CliBoundCommand command, ArgReader? reader)
    {
        if (!string.IsNullOrWhiteSpace(options.Fidelity))
        {
            return await CloneFidelityRunner.RunAsync(rootDir, options.Theme, options.Fidelity, options.Force, options.Use, reader);
        }

        if (string.IsNullOrWhiteSpace(options.Tokens))
        {
            Console.Error.WriteLine("Missing required option: --tokens <file>");
            return 2;
        }

        var themeDir = Path.Combine(rootDir, "themes", options.Theme);
        if (Directory.Exists(themeDir))
        {
            if (!options.Force)
            {
                Console.Error.WriteLine($"Theme already exists: {options.Theme}. Use --force to overwrite.");
                return 2;
            }

            Directory.Delete(themeDir, recursive: true);
        }

        var (tokens, tokensError) = await CloneInputLoader.LoadTokensAsync(options.Tokens);
        if (tokensError != 0) return tokensError;

        var (layout, layoutError) = await CloneInputLoader.LoadLayoutAsync(options.Layout);
        if (layoutError != 0) return layoutError;

        var (behaviors, behaviorsError) = await CloneInputLoader.LoadBehaviorsAsync(options.Behaviors);
        if (behaviorsError != 0) return behaviorsError;

        var (icons, iconsError) = await CloneInputLoader.LoadIconsAsync(options.Icons);
        if (iconsError != 0) return iconsError;

        var (assets, assetsError) = await CloneInputLoader.LoadAssetsAsync(options.Assets);
        if (assetsError != 0) return assetsError;

        if (options.Assets is not null)
        {
            await CloneAssetDownloader.DownloadAssetsAsync(rootDir, options.Theme, assets);
        }

        CloneGenerationSummary summary;
        if (options.Page is not null || options.Sections is not null)
        {
            var page = options.Page is null
                ? ClonePageInfo.Default
                : await CloneInputLoader.LoadPageAsync(options.Page);
            var sections = options.Sections is null
                ? Array.Empty<CloneSectionInfo>()
                : await CloneInputLoader.LoadSectionsAsync(options.Sections);

            var contentResult = CloneContentWriter.WriteTo(rootDir, options.Theme, tokens, page, sections, assets, behaviors, options.Brand);
            CloneAssetDownloader.WriteIcons(rootDir, options.Theme, icons, out var iconCount);
            summary = new CloneGenerationSummary
            {
                FileCount = contentResult.ThemeFileCount,
                BehaviorCount = CloneAssetDownloader.CountBehaviors(behaviors),
                IconCount = iconCount,
                AssetCount = assets.Count,
                SectionCount = contentResult.SectionCount,
                ContentFileCount = contentResult.ContentFileCount,
                DataFileCount = contentResult.DataFileCount,
                ConfigUpdated = contentResult.ConfigUpdated,
                Warnings = contentResult.Warnings
            };
        }
        else
        {
            summary = CloneThemeGenerator.WriteTo(rootDir, options.Theme, tokens, layout, options.Brand, behaviors, icons, assets);
        }

        Console.WriteLine($"Theme cloned: {options.Theme}");
        Console.WriteLine($"  Files: {summary.FileCount}");
        if (summary.ContentFileCount > 0)
            Console.WriteLine($"  Content files: {summary.ContentFileCount}");
        if (summary.DataFileCount > 0)
            Console.WriteLine($"  Data modules: {summary.DataFileCount}");
        if (summary.BehaviorCount > 0)
            Console.WriteLine($"  Behaviors: {summary.BehaviorCount}");
        if (summary.IconCount > 0)
            Console.WriteLine($"  Icons: {summary.IconCount}");
        if (summary.AssetCount > 0)
            Console.WriteLine($"  Assets: {summary.AssetCount} (theme asset dirs created)");
        if (summary.SectionCount > 0)
            Console.WriteLine($"  Extra sections: {summary.SectionCount}");
        if (summary.ConfigUpdated)
            Console.WriteLine("  Config: site.yaml updated for content + data sources");
        foreach (var warning in summary.Warnings)
            Console.WriteLine($"  Warning: {warning}");

        if (options.Use && reader is not null)
        {
            var useResult = await ThemeCommand.SetThemeAsync(options.Theme, reader,
                brand: options.Brand, primaryColor: tokens.Primary, accentColor: tokens.Accent);
            if (useResult != 0)
                return useResult;
        }

        if (options.Verify)
        {
            var verifyResult = await CloneVerifier.VerifyCloneAsync(command, rootDir, options.FailOnVisualDiff, options.VisualThreshold);
            if (verifyResult != 0)
                return verifyResult;
        }

        return 0;
    }

    private static double? ParseVisualThreshold(string? text) => CloneCommandOptions.ParseVisualThreshold(text);

    private static int CountBehaviors(CloneBehaviors? b) => CloneAssetDownloader.CountBehaviors(b);
}

