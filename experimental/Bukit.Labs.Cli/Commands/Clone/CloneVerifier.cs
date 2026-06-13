using System.Text;
using Bukit.Cli;
using Bukit.Cli.Cli.Binding;
using Bukit.Config;
using Bukit.Engine;
using Scriban;

namespace Bukit.Labs.Cli.Commands;

internal static class CloneVerifier
{
    public static async Task<int> VerifyCloneAsync(CliBoundCommand command, string rootDir, bool failOnVisualDiff, double visualThreshold)
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
        var buildResult = await Bukit.Cli.Commands.BuildCommand.RunAsync(new CliBoundCommand(buildOptions, Array.Empty<string>()));
        Console.WriteLine(buildResult == 0 ? "  Verify build: passed" : "  Verify build: failed");
        var sections = command.GetString("--sections") is { } sectionsPath
            ? await CloneInputLoader.LoadSectionsAsync(sectionsPath)
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
        var screenshotComparisons = CloneScreenshotComparer.CompareScreenshotFiles(targetScreenshotsDir, localScreenshotsDir).ToList();
        var missingScreenshots = CloneScreenshotComparer.FindMissingScreenshotPairs(targetScreenshotsDir, localScreenshotsDir).ToList();
        var failedComparisons = screenshotComparisons.Count(c => c.DiffRatio > visualThreshold);
        var affectedSections = CloneScreenshotComparer.FindAffectedSections(screenshotComparisons, sections, visualThreshold).ToList();

        var sb = new StringBuilder();
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
        CloneScreenshotComparer.AppendAffectedSections(sb, affectedSections, sections.Count > 0);
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

    internal static ScreenshotComparison ComparePngScreenshots(string name, string targetPath, string localPath)
        => CloneScreenshotComparer.ComparePngScreenshots(name, targetPath, localPath);

    private static IEnumerable<ScreenshotComparison> CompareScreenshotFiles(string targetDir, string localDir)
        => CloneScreenshotComparer.CompareScreenshotFiles(targetDir, localDir);

    private static IEnumerable<MissingScreenshotPair> FindMissingScreenshotPairs(string targetDir, string localDir)
        => CloneScreenshotComparer.FindMissingScreenshotPairs(targetDir, localDir);

    private static string ExtractViewportName(string screenshotName)
        => CloneScreenshotComparer.ExtractViewportName(screenshotName);

    private static string SectionLabel(CloneSectionInfo section)
        => CloneScreenshotComparer.SectionLabel(section);

    internal static void WriteBehaviorVerifyScript(string rootDir)
    {
        var path = Path.Combine(rootDir, "docs", "research", "BEHAVIORS_VERIFY.js");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, CloneBehaviorVerifyScript.Script);
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
}
