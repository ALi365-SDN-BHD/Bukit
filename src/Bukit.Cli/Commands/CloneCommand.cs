using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Bukit.Cli.Cli.Binding;
using Bukit.Config;
using Bukit.Engine;
using Scriban;

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
            tokens = CloneTokens.FromJson(tokensJson);
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Failed to parse tokens file: {ex.Message}");
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
            var verifyResult = await VerifyCloneAsync(command, rootDir, failOnVisualDiff, visualThreshold.Value);
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

    private static async Task<IReadOnlyList<CloneSectionInfo>> LoadSectionsAsync(string path)
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
        using var http = new HttpClient();
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
                // Skip assets that fail to download
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

    private static async Task<int> VerifyCloneAsync(CliBoundCommand command, string rootDir, bool failOnVisualDiff, double visualThreshold)
    {
        var configPath = ResolveConfigPathForCommand(command, rootDir);
        try
        {
            var config = ConfigLoader.Load(configPath);
            ConfigValidator.Validate(config);
            Console.WriteLine("  Verify doctor: config valid");

            var (layoutsDir, _, _, _, _, _, _) = BuildPathUtils.ResolveThemeDirectories(rootDir, config.Theme);
            var requiredTemplates = new[]
            {
                Path.Combine(layoutsDir, "layouts", "base.html"),
                Path.Combine(layoutsDir, "pages", "index.html"),
                Path.Combine(layoutsDir, "pages", "page.html"),
                Path.Combine(layoutsDir, "pages", "post.html"),
                Path.Combine(layoutsDir, "pages", "list.html")
            };
            foreach (var templatePath in requiredTemplates)
            {
                if (!File.Exists(templatePath))
                {
                    Console.Error.WriteLine($"  Verify doctor failed: template not found: {templatePath}");
                    return 1;
                }

                var parsed = Template.Parse(await File.ReadAllTextAsync(templatePath), templatePath);
                if (parsed.HasErrors)
                {
                    Console.Error.WriteLine($"  Verify doctor failed: template parse error: {templatePath}");
                    foreach (var message in parsed.Messages)
                        Console.Error.WriteLine($"    {message}");
                    return 1;
                }
            }
            Console.WriteLine("  Verify doctor: templates present and parse");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  Verify doctor failed: {ex.Message}");
            return 1;
        }

        var buildOptions = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["--config"] = configPath
        };
        var buildResult = await BuildCommand.RunAsync(new CliBoundCommand(buildOptions, Array.Empty<string>()));
        Console.WriteLine(buildResult == 0 ? "  Verify build: passed" : "  Verify build: failed");
        var sections = command.GetString("--sections") is { } sectionsPath
            ? await LoadSectionsAsync(sectionsPath)
            : Array.Empty<CloneSectionInfo>();

        WriteBehaviorVerifyScript(rootDir);

        var visualResult = WriteVerifyReport(rootDir, configPath, buildResult == 0, visualThreshold, sections);
        if (buildResult != 0)
            return buildResult;
        if (failOnVisualDiff && visualResult.HasFailures)
        {
            Console.Error.WriteLine($"  Verify visual diff failed: {visualResult.FailedComparisons} comparison(s) exceeded threshold {visualThreshold:P2}.");
            return 1;
        }
        return 0;
    }

    private static VisualVerifyResult WriteVerifyReport(string rootDir, string configPath, bool buildPassed, double visualThreshold, IReadOnlyList<CloneSectionInfo> sections)
    {
        var reportPath = Path.Combine(rootDir, "docs", "research", "VERIFY_REPORT.md");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);

        var distDir = Path.Combine(rootDir, "dist");
        var targetScreenshotsDir = Path.Combine(rootDir, "docs", "design-references");
        var localScreenshotsDir = Path.Combine(rootDir, "docs", "research", "local-screenshots");
        var screenshotComparisons = CompareScreenshotFiles(targetScreenshotsDir, localScreenshotsDir).ToList();
        var missingScreenshots = FindMissingScreenshotPairs(targetScreenshotsDir, localScreenshotsDir).ToList();
        var failedComparisons = screenshotComparisons.Count(c => c.DiffRatio > visualThreshold);
        var affectedSections = FindAffectedSections(screenshotComparisons, sections, visualThreshold).ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Clone Verify Report");
        sb.AppendLine();
        sb.AppendLine($"- Config: `{configPath}`");
        sb.AppendLine($"- Build: `{(buildPassed ? "passed" : "failed")}`");
        sb.AppendLine($"- Dist: `{distDir}`");
        sb.AppendLine($"- Content files: `{CountFiles(Path.Combine(rootDir, "content"), "*.md")}`");
        sb.AppendLine($"- Data modules: `{CountFiles(Path.Combine(rootDir, "data"), "*.md")}`");
        sb.AppendLine($"- Theme asset files: `{CountThemeAssetFiles(rootDir)}`");
        sb.AppendLine($"- Visual threshold: `{visualThreshold:P2}`");
        sb.AppendLine($"- Target screenshots: `{targetScreenshotsDir}`");
        sb.AppendLine($"- Local screenshots: `{localScreenshotsDir}`");
        sb.AppendLine("- Local screenshot capture: `external` - handled by `bukit-clone` skill/browser automation, not by Bukit CLI.");
        sb.AppendLine();
        sb.AppendLine("## Screenshot Comparisons");
        if (screenshotComparisons.Count == 0)
        {
            sb.AppendLine("- No paired screenshots found. Add matching files such as `target-1440.png` and `local-1440.png`; Bukit CLI only diffs screenshots that already exist.");
        }
        else
        {
            foreach (var comparison in screenshotComparisons)
            {
                var pass = comparison.DiffRatio <= visualThreshold ? "pass" : "fail";
                var bbox = comparison.HasMismatchBounds
                    ? $" bbox={comparison.MismatchMinX},{comparison.MismatchMinY}-{comparison.MismatchMaxX},{comparison.MismatchMaxY}"
                    : "";
                sb.AppendLine($"- {comparison.Name}: `{pass}` `{comparison.Status}` pixels={comparison.MismatchedPixels}/{comparison.ComparedPixels} diff={comparison.DiffRatio:P2} threshold={visualThreshold:P2} dimensions={comparison.TargetWidth}x{comparison.TargetHeight} vs {comparison.LocalWidth}x{comparison.LocalHeight}{bbox}");
            }
        }
        AppendAffectedSections(sb, affectedSections, sections.Count > 0);
        sb.AppendLine();
        sb.AppendLine("## Missing Screenshots");
        if (missingScreenshots.Count == 0)
        {
            sb.AppendLine("- none");
        }
        else
        {
            foreach (var missing in missingScreenshots)
                sb.AppendLine($"- {missing.Viewport}: target=`{(missing.TargetExists ? "present" : "missing")}` local=`{(missing.LocalExists ? "present" : "missing")}` expected `{missing.TargetPath}` and `{missing.LocalPath}`");
        }
        sb.AppendLine();
        sb.AppendLine("## Interactive Behavior Checks");
        sb.AppendLine("- Run `docs/research/BEHAVIORS_VERIFY.js` in the browser console on the built site or inject it via automation to validate:");
        sb.AppendLine("  - `HeaderSticky`: checks `.site-header` has `position: sticky` or `position: fixed`");
        sb.AppendLine("  - `HeaderShrink`: checks nav visibility toggles on scroll (requires scroll interaction)");
        sb.AppendLine("  - `DarkModeToggle`: checks `.dark-mode-toggle` exists and toggles `body.dark`");
        sb.AppendLine("  - `Modal`: checks `.modal-overlay` exists and has open/close/Escape behavior");
        sb.AppendLine("  - `Hamburger`: checks `.hamburger` button exists and toggles `.nav-links.open`");
        sb.AppendLine("  - `Tabs`: checks `.tab-nav` or `.state-tabs` exists and switches panels on click");
        sb.AppendLine("  - `Lenis`: checks if `window.lenis` is defined (Lenis smooth scroll active)");
        sb.AppendLine("  - `BackToTop`: checks `.back-to-top` button exists");
        sb.AppendLine();
        sb.AppendLine("## Next Visual QA Step");
        if (missingScreenshots.Any(m => !m.TargetExists))
            sb.AppendLine("- Missing target screenshots: rerun source-site screenshot capture in the `bukit-clone` skill.");
        if (missingScreenshots.Any(m => !m.LocalExists))
            sb.AppendLine("- Missing local screenshots: build the site and let the `bukit-clone` skill capture local screenshots into `docs/research/local-screenshots/local-*.png`.");
        if (failedComparisons > 0)
            sb.AppendLine("- Visual threshold exceeded: inspect section specs, assets, CSS, and generated partials for the failing viewport(s).");
        if (missingScreenshots.Count == 0 && failedComparisons == 0)
            sb.AppendLine("- Visual screenshot pairs are within the configured threshold.");
        File.WriteAllText(reportPath, sb.ToString());
        WriteVerifyJsonReport(rootDir, configPath, buildPassed, visualThreshold, screenshotComparisons, missingScreenshots, affectedSections);
        Console.WriteLine($"  Verify report: {reportPath}");
        return new VisualVerifyResult(screenshotComparisons.Count, failedComparisons, missingScreenshots.Count);
    }

    private static void WriteBehaviorVerifyScript(string rootDir)
    {
        var path = Path.Combine(rootDir, "docs", "research", "BEHAVIORS_VERIFY.js");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        File.WriteAllText(path, """
(function(){'use strict';
var results=[];
function pass(name,detail){results.push({name:name,status:'pass',detail:detail||''});console.log('%c PASS %c '+name,'color:green','color:inherit',detail||'');}
function fail(name,detail){results.push({name:name,status:'fail',detail:detail||''});console.log('%c FAIL %c '+name,'color:red','color:inherit',detail||'');}
function warn(name,detail){results.push({name:name,status:'warn',detail:detail||''});console.log('%c WARN %c '+name,'color:orange','color:inherit',detail||'');}

// Header sticky
var header=document.querySelector('.site-header');
if(header){var pos=getComputedStyle(header).position;if(pos==='sticky'||pos==='fixed')pass('HeaderSticky','position: '+pos);else warn('HeaderSticky','position: '+pos+' (not sticky)');}else{fail('HeaderSticky','.site-header not found');}

// Header shrink (nav-hidden class toggle check)
if(header&&document.querySelector('.nav-hidden')!==null)pass('HeaderShrink','.nav-hidden class present');else if(header)warn('HeaderShrink','.nav-hidden not present (may need scroll)');else fail('HeaderShrink','header not found');

// Dark mode toggle
var dt=document.querySelector('.dark-mode-toggle');
if(dt){pass('DarkModeToggle:exists','found');var wasDark=document.body.classList.contains('dark');dt.click();var nowDark=document.body.classList.contains('dark');if(wasDark!==nowDark)pass('DarkModeToggle:toggles','body.dark toggled');else fail('DarkModeToggle:toggles','body.dark did not change');dt.click();}else{warn('DarkModeToggle','.dark-mode-toggle not found (dark mode may not be configured)');}

// Modal
var mo=document.getElementById('site-modal')||document.querySelector('.modal-overlay');
if(mo){pass('Modal:exists','found');var wasVis=!mo.classList.contains('hidden')&&mo.classList.contains('visible');if(!wasVis){mo.classList.remove('hidden');mo.classList.add('visible');mo.setAttribute('aria-hidden','false');var nowVis=!mo.classList.contains('hidden')&&mo.classList.contains('visible');mo.classList.add('hidden');mo.classList.remove('visible');mo.setAttribute('aria-hidden','true');if(nowVis)pass('Modal:opens','modal became visible');else fail('Modal:opens','modal did not become visible');}else{pass('Modal:visible','already visible');}}else{warn('Modal','.modal-overlay not found');}

// Hamburger
var ham=document.querySelector('.hamburger');
if(ham){pass('Hamburger:exists','found');var nav=document.querySelector('.nav-links');var wasOpen=nav&&nav.classList.contains('open');ham.click();var nowOpen=nav&&nav.classList.contains('open');if(wasOpen!==nowOpen)pass('Hamburger:toggles','.nav-links.open toggled');else fail('Hamburger:toggles','did not toggle');ham.click();}else{warn('Hamburger','.hamburger not found');}

// Tabs (tab-nav or state-tabs)
var tabs=document.querySelector('.tab-nav')||document.querySelector('.state-tabs');
if(tabs){var firstBtn=tabs.querySelector('[role="tab"]');if(firstBtn){pass('Tabs:exists','found');var wasSel=firstBtn.getAttribute('aria-selected')==='true';firstBtn.click();setTimeout(function(){var nowSel=firstBtn.getAttribute('aria-selected')==='true';if(nowSel)pass('Tabs:switches','tab selected');else fail('Tabs:switches','tab did not become selected');},50);}else{fail('Tabs','no tab button found');}}else{warn('Tabs','no .tab-nav or .state-tabs found');}

// Lenis
if(typeof lenis!=='undefined'){pass('Lenis','window.lenis defined');}else{warn('Lenis','window.lenis not defined (Lenis may not be configured)');}

// Back to top
var btt=document.querySelector('.back-to-top');
if(btt){pass('BackToTop:exists','found');var bttOp=getComputedStyle(btt).opacity;if(parseFloat(bttOp)>0)pass('BackToTop:visible','opacity: '+bttOp);else warn('BackToTop:hidden','opacity: '+bttOp+' (may need scroll)');}else{warn('BackToTop','.back-to-top not found');}

// Animate on scroll
var anim=document.querySelector('.animate-in');
if(anim){pass('AnimateOnScroll','.animate-in element found');}else{warn('AnimateOnScroll','no .animate-in elements found');}

// Summary
console.log('\n=== BEHAVIOR VERIFY SUMMARY ===');
var passed=results.filter(function(r){return r.status==='pass';}).length;
var failed=results.filter(function(r){return r.status==='fail';}).length;
var warnings=results.filter(function(r){return r.status==='warn';}).length;
console.log('Passed: '+passed+' Failed: '+failed+' Warnings: '+warnings+' Total: '+results.length);
if(failed>0){console.log('%c FAILURES DETECTED','color:red;font-weight:bold');}else if(warnings===0){console.log('%c ALL CHECKS PASSED','color:green;font-weight:bold');}else{console.log('%c ALL CRITICAL CHECKS PASSED (with warnings)','color:orange;font-weight:bold');}
window.__bukitBehaviorResults=results;

// Export as JSON
var json=JSON.stringify({timestamp:new Date().toISOString(),summary:{passed:passed,failed:failed,warnings:warnings,total:results.length},results:results},null,2);
console.log('\n=== RESULTS JSON ===');
console.log(json);

})();
""");
    }

    private static void AppendAffectedSections(System.Text.StringBuilder sb, IReadOnlyList<AffectedSection> affectedSections, bool hasSections)
    {
        sb.AppendLine();
        sb.AppendLine("## Likely Affected Sections");
        if (!hasSections)
        {
            sb.AppendLine("- No sections metadata available. Pass `--sections sections.json` to map visual diffs back to extracted sections.");
            return;
        }

        if (affectedSections.Count == 0)
        {
            sb.AppendLine("- none inferred");
            return;
        }

        foreach (var group in affectedSections.GroupBy(a => a.Screenshot))
        {
            sb.AppendLine($"- {group.Key}: overlaps:");
            foreach (var item in group)
            {
                sb.AppendLine($"  - section {item.SectionIndex}: `{item.SectionLabel}` id=`{item.SectionId ?? ""}` type=`{item.SectionType ?? ""}` order=`{item.SectionOrder?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? ""}` y={item.SectionY:0} height={item.SectionHeight:0}");
                sb.AppendLine($"    data: `{item.DataPath}`");
                sb.AppendLine($"    spec: `{item.SpecPath}`");
            }
        }
    }

    private static IEnumerable<AffectedSection> FindAffectedSections(IReadOnlyList<ScreenshotComparison> comparisons, IReadOnlyList<CloneSectionInfo> sections, double visualThreshold)
    {
        foreach (var comparison in comparisons.Where(c => c.DiffRatio > visualThreshold && c.HasMismatchBounds))
        {
            var viewport = ExtractViewportName(comparison.Name);
            foreach (var item in sections.Select((section, index) => new { Section = section, Index = index, Bounds = ResolveSectionBounds(section, viewport) }))
            {
                var bounds = item.Bounds;
                if (bounds is null)
                    continue;
                var y = bounds.Y ?? 0;
                var height = bounds.Height ?? 0;
                if (!RangesOverlap(y, y + height, comparison.MismatchMinY!.Value, comparison.MismatchMaxY!.Value))
                    continue;

                yield return new AffectedSection(
                    Screenshot: comparison.Name,
                    Viewport: viewport,
                    SectionIndex: item.Index + 1,
                    SectionKey: CloneContentWriter.SectionDataKey(item.Section, item.Index),
                    SectionId: item.Section.Id,
                    SectionType: item.Section.Type ?? item.Section.Semantic,
                    SectionOrder: item.Section.Order,
                    SectionLabel: SectionLabel(item.Section),
                    DataPath: $"data/{CloneContentWriter.SectionDataKey(item.Section, item.Index)}.md",
                    SpecPath: $"docs/research/components/{CloneContentWriter.SectionSpecFileName(item.Section, item.Index)}",
                    SectionY: y,
                    SectionHeight: height,
                    MismatchMinY: comparison.MismatchMinY.Value,
                    MismatchMaxY: comparison.MismatchMaxY.Value);
            }
        }
    }

    private static void WriteVerifyJsonReport(
        string rootDir,
        string configPath,
        bool buildPassed,
        double visualThreshold,
        IReadOnlyList<ScreenshotComparison> comparisons,
        IReadOnlyList<MissingScreenshotPair> missingScreenshots,
        IReadOnlyList<AffectedSection> affectedSections)
    {
        var path = Path.Combine(rootDir, "docs", "research", "VERIFY_REPORT.json");
        var payload = new CloneVerifyReportJson(
            buildPassed,
            configPath,
            visualThreshold,
            buildPassed && comparisons.All(c => c.DiffRatio <= visualThreshold),
            new CloneVerifyReportSummary(
                comparisons.Count,
                comparisons.Count(c => c.DiffRatio > visualThreshold),
                missingScreenshots.Count,
                affectedSections.Count),
            comparisons.Select(c => new CloneVerifyScreenshotComparison(
                c.Name,
                c.DiffRatio <= visualThreshold,
                c.Status,
                c.ComparedPixels,
                c.MismatchedPixels,
                c.DiffRatio,
                c.TargetWidth,
                c.TargetHeight,
                c.LocalWidth,
                c.LocalHeight,
                c.HasMismatchBounds
                    ? new CloneVerifyMismatchBounds(c.MismatchMinX, c.MismatchMinY, c.MismatchMaxX, c.MismatchMaxY)
                    : null)).ToList(),
            missingScreenshots.Select(m => new CloneVerifyMissingScreenshot(
                m.Viewport,
                m.TargetPath,
                m.LocalPath,
                m.TargetExists,
                m.LocalExists)).ToList(),
            affectedSections.Select(a => new CloneVerifyAffectedSection(
                a.Screenshot,
                a.Viewport,
                a.SectionIndex,
                a.SectionKey,
                a.SectionId,
                a.SectionType,
                a.SectionOrder,
                a.SectionLabel,
                a.DataPath,
                a.SpecPath,
                a.SectionY,
                a.SectionHeight,
                a.MismatchMinY,
                a.MismatchMaxY)).ToList());
        File.WriteAllText(path, CloneJson.SerializeIndented(payload));
    }

    private static CloneBox? ResolveSectionBounds(CloneSectionInfo section, string viewport)
    {
        if (section.Responsive?.Viewports is { Count: > 0 })
        {
            if (section.Responsive.Viewports.TryGetValue(viewport, out var exact) && exact.Bounds is not null)
                return exact.Bounds;
            var alias = viewport switch
            {
                "1440" => "desktop",
                "768" => "tablet",
                "390" => "mobile",
                _ => null
            };
            if (alias is not null && section.Responsive.Viewports.TryGetValue(alias, out var named) && named.Bounds is not null)
                return named.Bounds;
        }

        return section.Bounds;
    }

    private static string ExtractViewportName(string screenshotName)
    {
        var file = Path.GetFileNameWithoutExtension(screenshotName);
        if (file.StartsWith("target-", StringComparison.OrdinalIgnoreCase))
            return file["target-".Length..];
        if (file.StartsWith("local-", StringComparison.OrdinalIgnoreCase))
            return file["local-".Length..];
        return file;
    }

    private static bool RangesOverlap(double aStart, double aEnd, double bStart, double bEnd)
        => aEnd >= bStart && bEnd >= aStart;

    private static string SectionLabel(CloneSectionInfo section)
        => section.Id ?? section.Heading ?? section.Title ?? section.Type ?? section.Semantic ?? "section";

    private static int CountFiles(string dir, string pattern)
        => Directory.Exists(dir) ? Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories).Count() : 0;

    private static int CountThemeAssetFiles(string rootDir)
    {
        var themesDir = Path.Combine(rootDir, "themes");
        if (!Directory.Exists(themesDir))
            return 0;

        return Directory.EnumerateDirectories(themesDir)
            .Select(theme => Path.Combine(theme, "assets"))
            .Where(Directory.Exists)
            .Sum(assets => Directory.EnumerateFiles(assets, "*.*", SearchOption.AllDirectories).Count());
    }

    private static IEnumerable<ScreenshotComparison> CompareScreenshotFiles(string targetDir, string localDir)
    {
        if (!Directory.Exists(targetDir) || !Directory.Exists(localDir))
            yield break;

        foreach (var target in Directory.EnumerateFiles(targetDir, "*.png"))
        {
            var targetName = Path.GetFileName(target);
            var localName = targetName.StartsWith("target-", StringComparison.OrdinalIgnoreCase)
                ? "local-" + targetName["target-".Length..]
                : targetName;
            var local = Path.Combine(localDir, localName);
            if (!File.Exists(local))
                continue;

            yield return ComparePngScreenshots(targetName, target, local);
        }
    }

    private static IEnumerable<MissingScreenshotPair> FindMissingScreenshotPairs(string targetDir, string localDir)
    {
        foreach (var viewport in new[] { "1440", "768", "390" })
        {
            var target = Path.Combine(targetDir, $"target-{viewport}.png");
            var local = Path.Combine(localDir, $"local-{viewport}.png");
            var targetExists = File.Exists(target);
            var localExists = File.Exists(local);
            if (!targetExists || !localExists)
                yield return new MissingScreenshotPair(viewport, target, local, targetExists, localExists);
        }
    }

    internal static ScreenshotComparison ComparePngScreenshots(string name, string targetPath, string localPath)
    {
        try
        {
            var target = PngImage.Read(targetPath);
            var local = PngImage.Read(localPath);
            var width = Math.Min(target.Width, local.Width);
            var height = Math.Min(target.Height, local.Height);
            var compared = width * height;
            var mismatched = 0;
            int? minX = null;
            int? minY = null;
            int? maxX = null;
            int? maxY = null;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var ti = ((y * target.Width) + x) * 4;
                    var li = ((y * local.Width) + x) * 4;
                    if (target.Pixels[ti] != local.Pixels[li] ||
                        target.Pixels[ti + 1] != local.Pixels[li + 1] ||
                        target.Pixels[ti + 2] != local.Pixels[li + 2] ||
                        target.Pixels[ti + 3] != local.Pixels[li + 3])
                    {
                        mismatched++;
                        minX = minX is null ? x : Math.Min(minX.Value, x);
                        minY = minY is null ? y : Math.Min(minY.Value, y);
                        maxX = maxX is null ? x : Math.Max(maxX.Value, x);
                        maxY = maxY is null ? y : Math.Max(maxY.Value, y);
                    }
                }
            }

            if (target.Width != local.Width || target.Height != local.Height)
            {
                mismatched += Math.Abs((target.Width * target.Height) - (local.Width * local.Height));
                minX ??= 0;
                minY ??= 0;
                maxX = Math.Max(target.Width, local.Width) - 1;
                maxY = Math.Max(target.Height, local.Height) - 1;
            }

            var total = Math.Max(target.Width * target.Height, local.Width * local.Height);
            var ratio = total == 0 ? 0 : (double)mismatched / total;
            var status = mismatched == 0 ? "identical" : "pixel-different";
            return new ScreenshotComparison(name, status, compared, mismatched, ratio, target.Width, target.Height, local.Width, local.Height, minX, minY, maxX, maxY);
        }
        catch (Exception ex)
        {
            var targetBytes = new FileInfo(targetPath).Length;
            var localBytes = new FileInfo(localPath).Length;
            var same = File.ReadAllBytes(targetPath).SequenceEqual(File.ReadAllBytes(localPath));
            return new ScreenshotComparison(name, same ? "identical-bytes" : $"unsupported-png: {ex.Message}", 0, same ? 0 : 1, same ? 0 : 1, (int)targetBytes, 0, (int)localBytes, 0, null, null, null, null);
        }
    }

    private static string ResolveConfigPathForCommand(CliBoundCommand command, string rootDir)
    {
        var configPath = command.GetString("--config");
        if (!string.IsNullOrWhiteSpace(configPath))
            return Path.GetFullPath(configPath);

        var site = command.GetString("--site");
        if (!string.IsNullOrWhiteSpace(site))
        {
            var fileName = site.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || site.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
                ? site
                : site + ".yaml";
            return Path.GetFullPath(Path.Combine(rootDir, "sites", fileName));
        }

        return Path.GetFullPath(Path.Combine(rootDir, "site.yaml"));
    }

    internal sealed record ScreenshotComparison(string Name, string Status, int ComparedPixels, int MismatchedPixels, double DiffRatio, int TargetWidth, int TargetHeight, int LocalWidth, int LocalHeight, int? MismatchMinX, int? MismatchMinY, int? MismatchMaxX, int? MismatchMaxY)
    {
        public bool HasMismatchBounds => MismatchMinX is not null && MismatchMinY is not null && MismatchMaxX is not null && MismatchMaxY is not null;
    }
    private sealed record MissingScreenshotPair(string Viewport, string TargetPath, string LocalPath, bool TargetExists, bool LocalExists);
    private sealed record AffectedSection(
        string Screenshot,
        string Viewport,
        int SectionIndex,
        string SectionKey,
        string? SectionId,
        string? SectionType,
        int? SectionOrder,
        string SectionLabel,
        string DataPath,
        string SpecPath,
        double SectionY,
        double SectionHeight,
        int MismatchMinY,
        int MismatchMaxY);
    private sealed record VisualVerifyResult(int Comparisons, int FailedComparisons, int MissingScreenshots)
    {
        public bool HasFailures => FailedComparisons > 0;
    }

    private sealed record PngImage(int Width, int Height, byte[] Pixels)
    {
        public static PngImage Read(string path)
        {
            using var stream = File.OpenRead(path);
            Span<byte> signature = stackalloc byte[8];
            ReadOnlySpan<byte> expectedSignature = [137, 80, 78, 71, 13, 10, 26, 10];
            if (stream.Read(signature) != 8 || !signature.SequenceEqual(expectedSignature))
                throw new InvalidOperationException("not a PNG file");

            var width = 0;
            var height = 0;
            var bitDepth = 0;
            var colorType = 0;
            using var idat = new MemoryStream();

            var header = new byte[8];
            while (stream.Position < stream.Length)
            {
                if (stream.Read(header) != 8)
                    break;
                var length = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(0, 4));
                var type = Encoding.ASCII.GetString(header.AsSpan(4, 4));
                var data = new byte[length];
                if (stream.Read(data) != length)
                    throw new InvalidOperationException("truncated PNG chunk");
                stream.Position += 4; // CRC

                if (type == "IHDR")
                {
                    width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(0, 4));
                    height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4, 4));
                    bitDepth = data[8];
                    colorType = data[9];
                }
                else if (type == "IDAT")
                {
                    idat.Write(data, 0, data.Length);
                }
                else if (type == "IEND")
                {
                    break;
                }
            }

            if (width <= 0 || height <= 0)
                throw new InvalidOperationException("missing IHDR");
            if (bitDepth != 8 || colorType is not (2 or 6))
                throw new InvalidOperationException($"unsupported PNG format bitDepth={bitDepth} colorType={colorType}");

            idat.Position = 0;
            using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
            using var raw = new MemoryStream();
            zlib.CopyTo(raw);

            var bytesPerPixel = colorType == 6 ? 4 : 3;
            var stride = width * bytesPerPixel;
            var filtered = raw.ToArray();
            var pixels = new byte[width * height * 4];
            var previous = new byte[stride];
            var current = new byte[stride];
            var offset = 0;
            for (var y = 0; y < height; y++)
            {
                var filter = filtered[offset++];
                Array.Copy(filtered, offset, current, 0, stride);
                offset += stride;
                Unfilter(current, previous, bytesPerPixel, filter);
                for (var x = 0; x < width; x++)
                {
                    var src = x * bytesPerPixel;
                    var dst = ((y * width) + x) * 4;
                    pixels[dst] = current[src];
                    pixels[dst + 1] = current[src + 1];
                    pixels[dst + 2] = current[src + 2];
                    pixels[dst + 3] = bytesPerPixel == 4 ? current[src + 3] : (byte)255;
                }
                (previous, current) = (current, previous);
                Array.Clear(current);
            }

            return new PngImage(width, height, pixels);
        }

        private static void Unfilter(byte[] row, byte[] previous, int bytesPerPixel, int filter)
        {
            for (var i = 0; i < row.Length; i++)
            {
                var left = i >= bytesPerPixel ? row[i - bytesPerPixel] : 0;
                var up = previous[i];
                var upperLeft = i >= bytesPerPixel ? previous[i - bytesPerPixel] : 0;
                row[i] = filter switch
                {
                    0 => row[i],
                    1 => unchecked((byte)(row[i] + left)),
                    2 => unchecked((byte)(row[i] + up)),
                    3 => unchecked((byte)(row[i] + ((left + up) / 2))),
                    4 => unchecked((byte)(row[i] + Paeth(left, up, upperLeft))),
                    _ => throw new InvalidOperationException($"unsupported PNG filter {filter}")
                };
            }
        }

        private static int Paeth(int a, int b, int c)
        {
            var p = a + b - c;
            var pa = Math.Abs(p - a);
            var pb = Math.Abs(p - b);
            var pc = Math.Abs(p - c);
            if (pa <= pb && pa <= pc) return a;
            return pb <= pc ? b : c;
        }
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
