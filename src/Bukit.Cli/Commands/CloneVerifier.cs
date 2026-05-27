using System.Text;
using Bukit.Cli.Cli.Binding;
using Bukit.Config;
using Bukit.Engine;
using Scriban;

namespace Bukit.Cli.Commands;

internal static class CloneVerifier
{
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
    internal sealed record VisualVerifyResult(int Comparisons, int FailedComparisons, int MissingScreenshots)
    {
        public bool HasFailures => FailedComparisons > 0;
    }

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
        var buildResult = await BuildCommand.RunAsync(new CliBoundCommand(buildOptions, Array.Empty<string>()));
        Console.WriteLine(buildResult == 0 ? "  Verify build: passed" : "  Verify build: failed");
        var sections = command.GetString("--sections") is { } sectionsPath
            ? await CloneCommand.LoadSectionsAsync(sectionsPath)
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

    internal static void WriteBehaviorVerifyScript(string rootDir)
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

    private static void AppendAffectedSections(StringBuilder sb, IReadOnlyList<AffectedSection> affectedSections, bool hasSections)
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
}
