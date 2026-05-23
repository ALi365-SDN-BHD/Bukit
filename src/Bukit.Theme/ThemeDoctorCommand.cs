namespace Bukit.Theme;

public static class ThemeDoctorCommand
{
    public sealed record DoctorResult(bool HasErrors, bool HasWarnings, List<string> Issues);

    public static DoctorResult Diagnose(string themeRoot, ThemeManifestV2 manifest, ThemeComponentRegistry registry)
    {
        var issues = new List<string>();

        CheckThemeYaml(themeRoot, manifest, issues);
        CheckPageTemplates(themeRoot, manifest, registry, issues);
        CheckSections(themeRoot, manifest, registry, issues);
        CheckComponents(manifest, registry, issues);
        CheckVariants(manifest, registry, issues);
        CheckAssets(themeRoot, manifest, issues);
        CheckExtends(themeRoot, manifest, issues);
        CheckTokens(themeRoot, manifest, issues);
        CheckUnusedComponents(manifest, issues);

        var errors = issues.Any(i => i.StartsWith("✗") || i.StartsWith("✘"));
        var warnings = issues.Any(i => i.StartsWith("⚠") || i.StartsWith("◌"));

        return new DoctorResult(errors, warnings, issues);
    }

    private static void CheckThemeYaml(string themeRoot, ThemeManifestV2 manifest, List<string> issues)
    {
        var themeYamlPath = Path.Combine(themeRoot, "theme.yaml");
        if (!File.Exists(themeYamlPath))
        {
            issues.Add("✗ theme.yaml not found");
            return;
        }
        issues.Add("✓ theme.yaml exists");

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            issues.Add("⚠ theme.yaml: name field is empty");
        }
        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            issues.Add("◌ theme.yaml: version field is empty (recommended)");
        }
    }

    private static void CheckPageTemplates(string themeRoot, ThemeManifestV2 manifest, ThemeComponentRegistry registry, List<string> issues)
    {
        if (manifest.PageTemplates is null || manifest.PageTemplates.Count == 0)
        {
            issues.Add("◌ No pageTemplates defined in theme.yaml");
            return;
        }

        foreach (var (name, ptDef) in manifest.PageTemplates)
        {
            var resolved = registry.ResolvePageTemplate(name);
            if (resolved is null)
            {
                var templatePath = Path.Combine(themeRoot, ptDef.Template);
                if (!File.Exists(templatePath))
                {
                    issues.Add($"✗ pageTemplate '{name}': template file not found '{ptDef.Template}'");
                }
                else
                {
                    issues.Add($"✓ pageTemplate '{name}' OK");
                }
            }
            else
            {
                issues.Add($"✓ pageTemplate '{name}' OK");
            }
        }
    }

    private static void CheckSections(string themeRoot, ThemeManifestV2 manifest, ThemeComponentRegistry registry, List<string> issues)
    {
        if (manifest.Sections is null || manifest.Sections.Count == 0)
        {
            issues.Add("◌ No sections defined in theme.yaml");
            return;
        }

        foreach (var (name, sDef) in manifest.Sections)
        {
            var resolved = registry.ResolveSectionTemplate(name);
            if (resolved is null)
            {
                var templatePath = Path.Combine(themeRoot, sDef.Template);
                if (!File.Exists(templatePath))
                {
                    issues.Add($"✗ section '{name}': template file not found '{sDef.Template}'");
                }
            }

            if (!string.IsNullOrEmpty(sDef.Schema))
            {
                var schemaPath = Path.Combine(themeRoot, sDef.Schema);
                if (!File.Exists(schemaPath))
                {
                    issues.Add($"⚠ section '{name}': schema file not found '{sDef.Schema}'");
                }
            }
            else
            {
                issues.Add($"◌ section '{name}': no schema defined (recommended)");
            }
        }
    }

    private static void CheckComponents(ThemeManifestV2 manifest, ThemeComponentRegistry registry, List<string> issues)
    {
        if (manifest.Components is null || manifest.Components.Count == 0)
        {
            return;
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in manifest.Components.Keys)
        {
            if (!names.Add(name))
            {
                issues.Add($"✗ component '{name}': duplicate name");
            }
        }
    }

    private static void CheckVariants(ThemeManifestV2 manifest, ThemeComponentRegistry registry, List<string> issues)
    {
        if (manifest.Sections is null) return;

        foreach (var (sectionName, sDef) in manifest.Sections)
        {
            if (sDef.Variants is null) continue;

            foreach (var (variantName, vDef) in sDef.Variants)
            {
                var resolved = registry.ResolveSectionTemplate(sectionName, variantName);
                if (resolved is null || !File.Exists(resolved))
                {
                    issues.Add($"✗ section '{sectionName}' variant '{variantName}': template not found '{vDef.Template}'");
                }
            }
        }
    }

    private static void CheckAssets(string themeRoot, ThemeManifestV2 manifest, List<string> issues)
    {
        if (manifest.Assets.Css is not null)
        {
            foreach (var css in manifest.Assets.Css)
            {
                var path = Path.Combine(themeRoot, css);
                if (!File.Exists(path))
                {
                    issues.Add($"⚠ asset CSS not found: {css}");
                }
            }
        }

        if (manifest.Assets.Js is not null)
        {
            foreach (var js in manifest.Assets.Js)
            {
                var path = Path.Combine(themeRoot, js);
                if (!File.Exists(path))
                {
                    issues.Add($"⚠ asset JS not found: {js}");
                }
            }
        }
    }

    private static void CheckExtends(string themeRoot, ThemeManifestV2 manifest, List<string> issues)
    {
        if (string.IsNullOrWhiteSpace(manifest.Extends)) return;

        var themesBase = Path.GetDirectoryName(themeRoot);
        if (themesBase is null) return;

        var parentThemeRoot = Path.Combine(themesBase, manifest.Extends);
        if (!Directory.Exists(parentThemeRoot))
        {
            issues.Add($"✗ extends: parent theme '{manifest.Extends}' not found at '{parentThemeRoot}'");
        }
        else
        {
            issues.Add($"✓ extends: parent theme '{manifest.Extends}' found");
        }
    }

    private static void CheckTokens(string themeRoot, ThemeManifestV2 manifest, List<string> issues)
    {
        var tokensPath = manifest.Tokens ?? "tokens.yaml";
        if (!Path.IsPathRooted(tokensPath))
        {
            tokensPath = Path.Combine(themeRoot, tokensPath);
        }

        if (!File.Exists(tokensPath))
        {
            if (manifest.Tokens is not null)
            {
                issues.Add($"⚠ tokens file not found: {tokensPath}");
            }
            return;
        }

        issues.Add("✓ tokens.yaml exists");

        try
        {
            var loader = new ThemeTokensLoader();
            var tokens = loader.Load(themeRoot, tokensPath);
            if (tokens is null)
            {
                issues.Add("⚠ tokens.yaml could not be parsed");
            }
        }
        catch
        {
            issues.Add("⚠ tokens.yaml could not be parsed");
        }
    }

    private static void CheckUnusedComponents(ThemeManifestV2 manifest, List<string> issues)
    {
        if (manifest.Components is null || manifest.Components.Count == 0) return;

        issues.Add("◌ Unused component detection: not yet implemented");
    }

    public static void PrintReport(DoctorResult result)
    {
        Console.WriteLine();
        Console.WriteLine("═══ Theme Doctor Report ═══");
        Console.WriteLine();

        foreach (var issue in result.Issues)
        {
            var color = issue.StartsWith("✗") || issue.StartsWith("✘") ? ConsoleColor.Red
                : issue.StartsWith("⚠") ? ConsoleColor.Yellow
                : issue.StartsWith("◌") ? ConsoleColor.DarkGray
                : ConsoleColor.Green;

            Console.ForegroundColor = color;
            Console.WriteLine($"  {issue}");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.Write("Summary: ");
        if (result.HasErrors)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERRORS FOUND");
        }
        else if (result.HasWarnings)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("WARNINGS FOUND");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("ALL CLEAN");
        }
        Console.ResetColor();
    }
}
