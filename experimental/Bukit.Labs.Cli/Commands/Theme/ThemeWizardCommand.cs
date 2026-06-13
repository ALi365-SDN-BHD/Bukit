using Bukit.Cli.Cli.Binding;

namespace Bukit.Cli.Commands;

public static class ThemeWizardCommand
{
    public static async Task<int> RunAsync(CliBoundCommand command)
    {
        var raw = command.GetArgument(1);

        var presetName = command.GetString("--preset");
        var hasPreset = !string.IsNullOrWhiteSpace(presetName);

        if (!hasPreset && (string.IsNullOrWhiteSpace(raw) || raw.StartsWith('-') || !CloneModels.IsSafeThemeName(raw)))
        {
            Console.Error.WriteLine("Missing or invalid theme name. Usage: bukit theme wizard <name> [--preset blog|docs|landing|minimal|portfolio]");
            return 2;
        }

        var name = hasPreset ? (raw ?? presetName) : raw;
        if (!CloneModels.IsSafeThemeName(name))
        {
            Console.Error.WriteLine("Missing or invalid theme name.");
            return 2;
        }

        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
        var rootDir = resolved.RootDir;
        var themesDir = Path.Combine(rootDir, "themes");
        var themeRoot = Path.Combine(themesDir, name!);

        var force = command.GetBool("--force");
        var templateScope = TemplateScopeExtensions.Parse(command.GetString("--template"));
        if (Directory.Exists(themeRoot))
        {
            if (!force)
            {
                Console.Error.WriteLine($"Theme already exists: {name}. Use --force to overwrite.");
                return 2;
            }

            Directory.Delete(themeRoot, recursive: true);
        }

        WizardPreset? preset = null;
        if (hasPreset)
        {
            preset = WizardPreset.All.FirstOrDefault(p =>
                p.Name.Equals(presetName, StringComparison.OrdinalIgnoreCase));

            if (preset is null)
            {
                Console.Error.WriteLine($"Unknown preset: {presetName}. Available: {string.Join(", ", WizardPreset.All.Select(p => p.Name))}");
                return 2;
            }

            Console.WriteLine();
            Console.WriteLine($"=== Bukit Theme Wizard: {name} ===");
            Console.WriteLine($"Preset: {preset.Name} — {preset.Description}");
            Console.WriteLine();
        }

        try
        {
            if (hasPreset && preset is not null)
            {
                return await RunPresetFlowAsync(preset, name!, command, rootDir, force, templateScope);
            }

            Console.WriteLine();
            Console.WriteLine($"=== Bukit Theme Wizard: {name} ===");
            Console.WriteLine();

            return await RunInteractiveFlowAsync(name!, command, rootDir, templateScope);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine();
            Console.WriteLine("Wizard cancelled.");
            return 2;
        }
    }

    private static Task<int> RunPresetFlowAsync(WizardPreset preset, string name, CliBoundCommand command, string rootDir, bool force, TemplateScope templateScope)
    {
        Console.WriteLine();
        Console.WriteLine("Presets provide sensible defaults. You can override them below.");
        Console.WriteLine();

        var finalThemeName = Ask("Theme Name", name);
        var description = Ask("Description", preset.Description);
        var author = Ask("Author", "");
        var brand = Ask("Brand display name", finalThemeName);

        Console.WriteLine();
        Console.WriteLine("--- Override Design Tokens (press Enter to keep preset) ---");
        var primaryColor = AskHexPreset("Primary color", preset.Tokens.Primary ?? "#0b5fff");
        var accentColor = AskHexPreset("Accent color", preset.Tokens.Accent ?? "#0f7b6c");

        Console.WriteLine();
        Console.WriteLine("--- Override Behaviors ---");
        var hasDarkMode = AskBoolPreset("Dark mode toggle", preset.Behaviors.DarkModeToggle);

        var tokens = preset.Tokens with
        {
            Primary = primaryColor,
            PrimaryStrong = ColorDarken(primaryColor),
            Accent = accentColor,
        };

        var behaviors = preset.Behaviors with
        {
            DarkModeToggle = hasDarkMode,
        };

        Console.WriteLine();
        Console.WriteLine("Generating theme...");

        CloneThemeGenerator.WriteTo(rootDir, finalThemeName, tokens, preset.Layout, brand, behaviors, templateScope: templateScope);

        Console.WriteLine($"Theme created: themes/{finalThemeName}/");
        Console.WriteLine($"Use it:   bukit theme use {finalThemeName}");
        Console.WriteLine($"Preview:  bukit preview");
        Console.WriteLine($"Info:     bukit theme info {finalThemeName}");

        if (command.GetBool("--use"))
        {
            var resolvedForSet = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
            return ThemeCommand.SetThemeAsync(finalThemeName, resolvedForSet.FullConfigPath, resolvedForSet.RootDir, brand, primaryColor, accentColor);
        }

        return Task.FromResult(0);
    }

    private static Task<int> RunInteractiveFlowAsync(string name, CliBoundCommand command, string rootDir, TemplateScope templateScope)
    {
        Console.WriteLine();

        var finalThemeName = Ask("Theme Name", name);
        var description = Ask("Description", "");
        var author = Ask("Author", "");
        var brand = Ask("Brand display name", finalThemeName);

        Console.WriteLine();
        Console.WriteLine("--- Preset ---");
        Console.WriteLine("Choose a starting preset (or skip for full customization):");
        for (var i = 0; i < WizardPreset.All.Count; i++)
        {
            var p = WizardPreset.All[i];
            Console.WriteLine($"  {i + 1}. {p.Name,-12} {p.Description}");
        }
        Console.WriteLine($"  {WizardPreset.All.Count + 1}. None — full manual customization");
        var presetChoice = AskChoice("Choose", ["1", "2", "3", "4", "5", "6"], "6");

        WizardPreset? selectedPreset = null;
        if (int.TryParse(presetChoice, out var idx) && idx >= 1 && idx <= WizardPreset.All.Count)
        {
            selectedPreset = WizardPreset.All[idx - 1];
            Console.WriteLine($"  Using preset: {selectedPreset.Name}");
        }

        var defaults = selectedPreset ?? new WizardPreset { Name = "custom", Tokens = new CloneTokens(), Layout = new CloneLayoutInfo(), Behaviors = new CloneBehaviors() };

        Console.WriteLine();
        Console.WriteLine("--- Design Tokens ---");
        var primaryColor = AskHex("Primary color", defaults.Tokens.Primary ?? "#0b5fff");
        var accentColor = AskHex("Accent color", defaults.Tokens.Accent ?? "#0f7b6c");
        var bgColor = AskHex("Background color", defaults.Tokens.Bg ?? "#fbfaf8");
        var textColor = AskHex("Text color", defaults.Tokens.Text ?? "#202124");
        var mutedColor = AskHex("Muted/secondary text color", defaults.Tokens.Muted ?? "#66615b");
        var fontFamily = Ask("Font family", defaults.Tokens.FontFamily ?? "system-ui");
        var radius = Ask("Border radius", defaults.Tokens.Radius ?? "8px");

        Console.WriteLine();
        Console.WriteLine("--- Layout ---");
        var hasHero = AskBool("Include hero section", defaults.Layout.HasFeaturesSection);
        var hasSidebar = AskBool("Include sidebar", false);
        var hasDarkMode = AskBool("Include dark mode toggle", defaults.Behaviors.DarkModeToggle);
        var stickyHeader = AskBool("Sticky header", defaults.Behaviors.StickyHeader);
        var mobileHamburger = AskBool("Mobile hamburger menu", defaults.Behaviors.MobileHamburger);

        Console.WriteLine();
        Console.WriteLine("--- Features ---");
        var hasSearch = AskBool("Include search page", true);
        var hasTaxonomy = AskBool("Include taxonomy pages", true);
        var hasPagination = AskBool("Include pagination", true);

        Console.WriteLine();
        Console.WriteLine("--- Template Style ---");
        Console.WriteLine("  1. Standard (header + content + footer)");
        Console.WriteLine("  2. Sidebar layout");
        Console.WriteLine("  3. Minimal (no header)");
        var styleChoice = AskChoice("Choose", ["1", "2", "3"], defaults.TemplateStyle);

        var tokens = new CloneTokens
        {
            Bg = bgColor,
            Primary = primaryColor,
            PrimaryStrong = ColorDarken(primaryColor),
            Accent = accentColor,
            Text = textColor,
            Muted = mutedColor,
            Radius = radius,
            FontFamily = fontFamily
        };

        var layout = new CloneLayoutInfo
        {
            HasFeaturesSection = hasHero,
            HasCTASection = hasHero
        };

        if (hasHero)
        {
            layout = layout with
            {
                HeroHeading = finalThemeName,
                HeroSubtext = description
            };
        }

        var behaviors = new CloneBehaviors
        {
            StickyHeader = stickyHeader,
            MobileHamburger = mobileHamburger,
            DarkModeToggle = hasDarkMode
        };

        Console.WriteLine();
        Console.WriteLine("Generating theme...");

        CloneThemeGenerator.WriteTo(rootDir, finalThemeName, tokens, layout, brand, behaviors, templateScope: templateScope);

        Console.WriteLine($"Theme created: themes/{finalThemeName}/");
        Console.WriteLine($"Use it:   bukit theme use {finalThemeName}");
        Console.WriteLine($"Preview:  bukit preview");
        Console.WriteLine($"Info:     bukit theme info {finalThemeName}");

        if (command.GetBool("--use"))
        {
            var resolvedForSet = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
            return ThemeCommand.SetThemeAsync(finalThemeName, resolvedForSet.FullConfigPath, resolvedForSet.RootDir, brand, primaryColor, accentColor);
        }

        return Task.FromResult(0);
    }

    private static string Ask(string prompt, string defaultValue)
    {
        var display = string.IsNullOrEmpty(defaultValue)
            ? $"{prompt}: "
            : $"{prompt} [{defaultValue}]: ";
        Console.Write(display);
        var input = Console.ReadLine()?.Trim();
        return string.IsNullOrWhiteSpace(input) ? defaultValue : input;
    }

    private static string AskHex(string prompt, string defaultValue)
    {
        while (true)
        {
            var input = Ask(prompt, defaultValue);
            if (input.StartsWith('#')) return input;
            Console.WriteLine("  Please enter a hex color starting with # (e.g., #0b5fff)");
        }
    }

    private static string AskHexPreset(string prompt, string defaultValue)
    {
        while (true)
        {
            Console.Write($"{prompt} [{defaultValue}]: ");
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(input)) return defaultValue;
            if (input.StartsWith('#')) return input;
            Console.WriteLine("  Please enter a hex color starting with # (e.g., #0b5fff)");
        }
    }

    private static bool AskBool(string prompt, bool defaultValue)
    {
        var yn = defaultValue ? "[Y/n]" : "[y/N]";
        Console.Write($"{prompt}? {yn}: ");
        var input = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(input)) return defaultValue;
        return input is "y" or "yes";
    }

    private static bool AskBoolPreset(string prompt, bool defaultValue)
    {
        return AskBool(prompt, defaultValue);
    }

    private static string AskChoice(string prompt, string[] options, string defaultValue)
    {
        Console.Write($"{prompt} [{defaultValue}]: ");
        var input = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(input)) return defaultValue;
        if (options.Contains(input, StringComparer.OrdinalIgnoreCase)) return input;
        return defaultValue;
    }

    private static string ColorDarken(string hex)
    {
        if (hex.Length < 7 || !hex.StartsWith('#')) return "#0846b8";
        try
        {
            var r = Convert.ToInt32(hex[1..3], 16);
            var g = Convert.ToInt32(hex[3..5], 16);
            var b = Convert.ToInt32(hex[5..7], 16);
            r = Math.Max(0, (int)(r * 0.8));
            g = Math.Max(0, (int)(g * 0.8));
            b = Math.Max(0, (int)(b * 0.8));
            return $"#{r:x2}{g:x2}{b:x2}";
        }
        catch
        {
            return "#0846b8";
        }
    }
}
