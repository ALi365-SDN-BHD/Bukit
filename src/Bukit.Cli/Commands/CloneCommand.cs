using System.Text;
using System.Text.Json;
using Bukit.Cli.Cli.Binding;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

public static class CloneCommand
{
    public static Task<int> RunAsync(ArgReader reader)
    {
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["--tokens"] = reader.GetOption("--tokens"),
            ["--layout"] = reader.GetOption("--layout"),
            ["--page"] = reader.GetOption("--page"),
            ["--sections"] = reader.GetOption("--sections"),
            ["--theme"] = reader.GetOption("--theme"),
            ["--brand"] = reader.GetOption("--brand"),
            ["--behaviors"] = reader.GetOption("--behaviors"),
            ["--icons"] = reader.GetOption("--icons"),
            ["--assets"] = reader.GetOption("--assets"),
            ["--visual-threshold"] = reader.GetOption("--visual-threshold"),
            ["--fidelity"] = reader.GetOption("--fidelity"),
        };
        if (reader.HasFlag("--use")) options["--use"] = "true";
        if (reader.HasFlag("--force")) options["--force"] = "true";
        if (reader.HasFlag("--verify")) options["--verify"] = "true";
        if (reader.HasFlag("--fail-on-visual-diff")) options["--fail-on-visual-diff"] = "true";

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
        var pagePath = command.GetString("--page");
        var sectionsPath = command.GetString("--sections");
        var themeName = command.GetString("--theme") ?? "cloned";
        var brand = command.GetString("--brand");
        var behaviorsPath = command.GetString("--behaviors");
        var iconsPath = command.GetString("--icons");
        var assetsPath = command.GetString("--assets");
        var use = command.GetBool("--use");
        var force = command.GetBool("--force");
        var verify = command.GetBool("--verify");
        var failOnVisualDiff = command.GetBool("--fail-on-visual-diff");
        var visualThreshold = ParseVisualThreshold(command.GetString("--visual-threshold"));
        if (visualThreshold is null)
            return 2;
        var fidelityHtmlDir = command.GetString("--fidelity");

        if (!string.IsNullOrWhiteSpace(fidelityHtmlDir))
        {
            return await RunFidelityAsync(rootDir, themeName, fidelityHtmlDir, force, use, reader);
        }

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
            var (parsed, tokenError) = CloneTokens.FromJson(tokensJson);
            if (tokenError is not null)
            {
                Console.Error.WriteLine($"Failed to parse tokens file: {tokenError}");
                return 2;
            }

            tokens = parsed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Failed to read tokens file: {ex.Message}");
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

        List<CloneIcon> icons = [];
        if (iconsPath is not null)
        {
            var iconsFullPath = Path.GetFullPath(iconsPath);
            if (!File.Exists(iconsFullPath))
            {
                Console.Error.WriteLine($"Icons file not found: {iconsFullPath}");
                return 2;
            }

            try
            {
                var iconsJson = await File.ReadAllTextAsync(iconsFullPath);
                icons = JsonSerializer.Deserialize(iconsJson, CloneInputJsonContext.Default.ListCloneIcon) ?? [];
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"Failed to parse icons file: {ex.Message}");
                return 2;
            }
        }

        List<CloneAsset> assets = [];
        if (assetsPath is not null)
        {
            var assetsFullPath = Path.GetFullPath(assetsPath);
            if (!File.Exists(assetsFullPath))
            {
                Console.Error.WriteLine($"Assets file not found: {assetsFullPath}");
                return 2;
            }

            try
            {
                var assetsJson = await File.ReadAllTextAsync(assetsFullPath);
                assets = JsonSerializer.Deserialize(assetsJson, CloneInputJsonContext.Default.ListCloneAsset) ?? [];
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"Failed to parse assets file: {ex.Message}");
                return 2;
            }

            await DownloadAssetsAsync(rootDir, themeName, assets);
        }

        CloneGenerationSummary summary;
        if (pagePath is not null || sectionsPath is not null)
        {
            var page = pagePath is null
                ? ClonePageInfo.Default
                : await LoadPageAsync(pagePath);
            var sections = sectionsPath is null
                ? Array.Empty<CloneSectionInfo>()
                : await LoadSectionsAsync(sectionsPath);

            var contentResult = CloneContentWriter.WriteTo(rootDir, themeName, tokens, page, sections, assets, behaviors, brand);
            WriteIcons(rootDir, themeName, icons, out var iconCount);
            summary = new CloneGenerationSummary
            {
                FileCount = contentResult.ThemeFileCount,
                BehaviorCount = CountBehaviors(behaviors),
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
            summary = CloneThemeGenerator.WriteTo(rootDir, themeName, tokens, layout, brand, behaviors, icons, assets);
        }

        Console.WriteLine($"Theme cloned: {themeName}");
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

        if (use && reader is not null)
        {
            var useResult = await ThemeCommand.SetThemeAsync(themeName, reader,
                brand: brand, primaryColor: tokens.Primary, accentColor: tokens.Accent);
            if (useResult != 0)
                return useResult;
        }

        if (verify)
        {
            var verifyResult = await CloneVerifier.VerifyCloneAsync(command, rootDir, failOnVisualDiff, visualThreshold.Value);
            if (verifyResult != 0)
                return verifyResult;
        }

        return 0;
    }

    private static async Task<ClonePageInfo> LoadPageAsync(string path)
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

    private static async Task DownloadAssetsAsync(string rootDir, string themeName, List<CloneAsset> assets)
    {
        var ssrfHandler = new System.Net.Http.SocketsHttpHandler
        {
            ConnectCallback = SsrfGuard.SsrfSafeConnectAsync
        };
        using var http = new HttpClient(ssrfHandler, disposeHandler: true);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("bukit-clone/1.0");
        http.Timeout = TimeSpan.FromSeconds(30);

        var total = 0;
        var downloaded = 0;

        foreach (var asset in assets.Where(a => !string.IsNullOrWhiteSpace(a.Src) && a.Src.StartsWith("http", StringComparison.OrdinalIgnoreCase)))
        {
            total++;
            try
            {
                var fileName = string.IsNullOrWhiteSpace(asset.LocalPath)
                    ? CloneContentWriter.AssetFileName(asset, total)
                    : Path.GetFileName(asset.LocalPath);
                var subdir = CloneContentWriter.AssetSubdir(asset.Type);
                var assetDir = Path.Combine(rootDir, "themes", themeName, "assets", subdir);
                Directory.CreateDirectory(assetDir);
                var filePath = Path.Combine(assetDir, fileName);
                if (File.Exists(filePath)) continue;

                var bytes = await http.GetByteArrayAsync(asset.Src);
                await File.WriteAllBytesAsync(filePath, bytes);
                downloaded++;
            }
            catch
            {
            }
        }

        if (downloaded > 0)
            Console.WriteLine($"  Downloaded {downloaded}/{total} assets to theme assets/");
    }

    private static void WriteIcons(string rootDir, string themeName, List<CloneIcon> icons, out int iconCount)
    {
        iconCount = 0;
        if (icons.Count == 0)
            return;

        var iconsDir = Path.Combine(rootDir, "themes", themeName, "assets", "icons");
        Directory.CreateDirectory(iconsDir);
        foreach (var icon in icons)
        {
            if (string.IsNullOrWhiteSpace(icon.Svg))
                continue;

            var fileName = SanitizeFileName(icon.Name) + ".svg";
            File.WriteAllText(Path.Combine(iconsDir, fileName), icon.Svg);
            iconCount++;
        }
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "icon";

        var chars = name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_').ToArray();
        var result = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(result) ? "icon" : result;
    }

    private static int CountBehaviors(CloneBehaviors? b)
    {
        if (b is null) return 0;
        var count = 0;
        if (b.StickyHeader) count++;
        if (b.CardHoverLift) count++;
        if (b.AnimateOnScroll) count++;
        if (b.ScrollShrinkNav) count++;
        if (b.DarkModeToggle) count++;
        if (b.MobileHamburger) count++;
        if (b.SmoothScroll) count++;
        if (b.BackToTop) count++;
        if (b.HasModal) count++;
        if (b.HasDropdown) count++;
        if (b.HasTabs) count++;
        if (b.UseLenis) count++;
        return count;
    }

    private static double? ParseVisualThreshold(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0.03d;

        if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) &&
            value is >= 0 and <= 1)
        {
            return value;
        }

        Console.Error.WriteLine("Invalid --visual-threshold value. Expected a number between 0 and 1, for example 0.03.");
        return null;
    }

    private static async Task<int> RunFidelityAsync(string rootDir, string themeName, string htmlDir, bool force, bool use, ArgReader? reader)
    {
        var fullHtmlDir = Path.GetFullPath(htmlDir);
        if (!Directory.Exists(fullHtmlDir))
        {
            Console.Error.WriteLine($"HTML directory not found: {fullHtmlDir}");
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

        Console.WriteLine($"Fidelity clone from: {fullHtmlDir}");

        CloneFidelityGenerator.FidelityResult result;
        try
        {
            result = CloneFidelityGenerator.Generate(rootDir, fullHtmlDir, themeName);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fidelity clone failed: {ex.Message}");
            return 1;
        }

        WriteFidelitySiteYaml(rootDir, themeName);
        TransferAssetsToStatic(rootDir, themeName);

        Console.WriteLine($"Theme cloned (fidelity mode): {themeName}");
        Console.WriteLine($"  Templates: {result.TemplateCount}");
        Console.WriteLine($"  Partials: {result.PartialCount}");
        Console.WriteLine($"  Assets: {result.AssetCount}");
        Console.WriteLine($"  Pages detected: {result.PageCount}");
        foreach (var w in result.Warnings)
            Console.WriteLine($"  Warning: {w}");

        if (use && reader is not null)
        {
            var useResult = await ThemeCommand.SetThemeAsync(themeName, reader, brand: null, primaryColor: null, accentColor: null);
            if (useResult != 0)
                return useResult;
        }

        return 0;
    }

    private static void WriteFidelitySiteYaml(string rootDir, string themeName)
    {
        var htmlFiles = Directory.GetFiles(
            Path.Combine(rootDir, "themes", themeName, "layouts", "pages"), "*.html")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not "index" and not "list")
            .Select(n => n!.Replace("-", " "))
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("site:");
        sb.AppendLine($"  name: {themeName}");
        sb.AppendLine($"  title: {themeName}");
        sb.AppendLine("  baseUrl: /");
        sb.AppendLine("  language: zh-CN");
        sb.AppendLine("  seo:");
        sb.AppendLine("    renderMode: 'off'");
        sb.AppendLine("  collections:");
        sb.AppendLine("    page:");
        sb.AppendLine("      permalink: '/{slug}/'");
        sb.AppendLine("      template: 'pages/page.html'");
        sb.AppendLine("      listRoute: '/'");
        sb.AppendLine("content:");
        sb.AppendLine("  provider: markdown");
        sb.AppendLine("  contentDir: content");
        sb.AppendLine("theme:");
        sb.AppendLine($"  name: {themeName}");

        var path = Path.Combine(rootDir, "site.yaml");
        if (!File.Exists(path))
        {
            File.WriteAllText(path, sb.ToString());
        }
        else
        {
            Console.WriteLine("  site.yaml already exists, skipping. Generated config would be:");
            Console.WriteLine(sb.ToString());
        }
    }

    private static void TransferAssetsToStatic(string rootDir, string themeName)
    {
        var themeBase = Path.Combine(rootDir, "themes", themeName);

        var themeAssetsDir = Path.Combine(themeBase, "assets");
        var themeStaticDir = Path.Combine(themeBase, "static");

        if (Directory.Exists(themeAssetsDir))
        {
            if (!Directory.Exists(themeStaticDir))
                Directory.CreateDirectory(themeStaticDir);

            foreach (var file in Directory.GetFiles(themeAssetsDir, "*.*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(themeAssetsDir, file);
                var dest = Path.Combine(themeStaticDir, rel);
                if (!File.Exists(dest))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Move(file, dest);
                }
            }
        }
    }
}
