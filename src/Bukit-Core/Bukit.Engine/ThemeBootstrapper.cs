using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;

using Bukit.Shared;
using Bukit.Theme;

namespace Bukit.Engine;

public sealed record ThemeBootstrapResult(
    string? ThemeName,
    string? ThemeRoot,
    string? ParentThemeRoot,
    ThemeManifestV2? Manifest,
    ThemeComponentRegistry? Registry,
    SectionSchemaValidator? SchemaValidator)
{
    internal IReadOnlyDictionary<string, ISectionPlugin>? SectionPlugins { get; init; }
}

public static class ThemeBootstrapper
{
    public static ThemeBootstrapResult Bootstrap(AppConfig config, string rootDir, ILogger log)
    {
        var resolved = ThemePathResolver.Resolve(rootDir, config.Theme, log);
        return Bootstrap(config, rootDir, log, resolved);
    }

    public static ThemeBootstrapResult BootstrapRequired(AppConfig config, string rootDir, ILogger log)
    {
        var resolved = ThemePathResolver.Resolve(rootDir, config.Theme, log);
        return Bootstrap(config, rootDir, log, resolved, requireThemeManifest: true);
    }

    internal static ThemeBootstrapResult Bootstrap(AppConfig config, string rootDir, ILogger log, ResolvedThemePaths resolved, bool requireThemeManifest = false)
    {
        var themeName = resolved.ThemeName == "default" ? (config.Theme.Name ?? "default") : resolved.ThemeName;
        var themeRoot = resolved.ThemeRoot;
        ThemeManifestV2? themeManifest = null;
        ThemeComponentRegistry? themeRegistry = null;
        SectionSchemaValidator? schemaValidator = null;
        IReadOnlyDictionary<string, ISectionPlugin>? resolvedSectionPlugins = null;

        if (string.IsNullOrWhiteSpace(config.Theme.Name))
        {
            themeManifest = LoadThemeManifest(resolved.LayoutsDir, required: false, logPathLabel: "theme.yaml");
            if (themeManifest is null)
            {
                return new ThemeBootstrapResult(themeName, null, null, null, null, null);
            }

            themeRoot = resolved.LayoutsDir;
        }
        else
        {
            themeManifest = LoadThemeManifest(themeRoot, required: requireThemeManifest, logPathLabel: "theme.yaml");
            if (themeManifest is null)
            {
                return new ThemeBootstrapResult(themeName, themeRoot, null, null, null, null);
            }
        }

        ThemeComponentRegistry? parentRegistry = null;
        var parentThemeRoot = resolved.ParentThemeRoot;
        if (parentThemeRoot is null && !string.IsNullOrWhiteSpace(themeManifest.Extends))
        {
            if (!ThemeNameSanitizer.TrySanitize(themeManifest.Extends, out var safeExtends, out var sanitizeError))
            {
                throw new ConfigException(
                    $"theme.yaml extends '{themeManifest.Extends}' is invalid: {sanitizeError}",
                    DiagnosticCode.ConfigPathTraversal);
            }

            parentThemeRoot = Path.Combine(rootDir, "themes", safeExtends);
        }

        if (!string.IsNullOrWhiteSpace(parentThemeRoot))
        {
            var parentManifestPath = Path.Combine(parentThemeRoot, "theme.yaml");
            if (!File.Exists(parentManifestPath))
            {
                throw new ConfigException(
                    $"theme.yaml extends '{themeManifest.Extends}' but parent theme manifest was not found at '{parentManifestPath}'.",
                    DiagnosticCode.ThemeSourceUnavailable);
            }

            var parentManifest = LoadThemeManifest(parentThemeRoot, required: true, logPathLabel: "parent theme.yaml");
            parentRegistry = new ThemeComponentRegistry(parentThemeRoot, parentManifest!, null);
        }

        themeRegistry = new ThemeComponentRegistry(themeRoot, themeManifest, parentRegistry);

        var sectionPlugins = new Dictionary<string, ISectionPlugin>(StringComparer.OrdinalIgnoreCase);
        if (themeManifest.Sections is not null)
        {
            foreach (var (sectionName, sDef) in themeManifest.Sections)
            {
                if (!string.IsNullOrWhiteSpace(sDef.Plugin) &&
                    SectionPluginRegistry.TryResolve(sDef.Plugin, out var plugin))
                {
                    sectionPlugins[sDef.Plugin] = plugin!;
                    log.Info($"Section '{sectionName}' loaded plugin: {sDef.Plugin} ({plugin!.SupportedHook})");
                }
            }
        }

        resolvedSectionPlugins = sectionPlugins.Count > 0 ? sectionPlugins : null;

        var validationMode = config.Theme.ComponentValidation switch
        {
            "strict" => ValidationMode.Strict,
            "warn" => ValidationMode.Warn,
            _ => ValidationMode.Off
        };
        schemaValidator = new SectionSchemaValidator(validationMode, themeRoot, log);

        return new ThemeBootstrapResult(themeName, themeRoot, parentThemeRoot, themeManifest, themeRegistry, schemaValidator)
        {
            SectionPlugins = resolvedSectionPlugins
        };
    }

    private static ThemeManifestV2? LoadThemeManifest(string themeRoot, bool required, string logPathLabel)
    {
        try
        {
            return ThemeManifestLoader.Load(themeRoot, required);
        }
        catch (ThemeManifestException ex)
        {
            throw new ConfigException($"Failed to parse {logPathLabel}: {ex.Message}", ex, DiagnosticCode.ThemeManifestInvalid);
        }
    }
}
