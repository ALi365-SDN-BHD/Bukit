using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Shared;
using Bukit.Theme;

namespace Bukit.Engine;

internal sealed record ThemeBootstrapResult(
    string? ThemeName,
    string? ThemeRoot,
    string? ParentThemeRoot,
    ThemeManifestV2? Manifest,
    ThemeComponentRegistry? Registry,
    SectionSchemaValidator? SchemaValidator,
    IReadOnlyDictionary<string, ISectionPlugin>? SectionPlugins);

internal static class ThemeBootstrapper
{
    internal static ThemeBootstrapResult Bootstrap(AppConfig config, string rootDir, ILogger log)
    {
        var themeName = config.Theme.Name;
        ThemeManifestV2? themeManifest = null;
        ThemeComponentRegistry? themeRegistry = null;
        SectionSchemaValidator? schemaValidator = null;
        IReadOnlyDictionary<string, ISectionPlugin>? resolvedSectionPlugins = null;

        if (string.IsNullOrWhiteSpace(themeName) && string.IsNullOrWhiteSpace(config.Theme.Source))
        {
            return new ThemeBootstrapResult(themeName, null, null, null, null, null, null);
        }

        var themeRoot = Path.Combine(rootDir, "themes", themeName ?? "remote");
        if (!string.IsNullOrWhiteSpace(config.Theme.Source))
        {
            var themesCacheDir = Path.Combine(rootDir, ".cache", "themes");
            Directory.CreateDirectory(themesCacheDir);
            var resolved = ThemeSourceManager.Resolve(config.Theme.Source, themesCacheDir,
                msg => log.Warn(msg));
            if (resolved is not null)
            {
                themeRoot = resolved.ThemeRoot;
                if (!string.IsNullOrWhiteSpace(themeName))
                {
                    themeRoot = Path.Combine(resolved.ThemeRoot, themeName);
                }
            }
        }

        themeManifest = ThemeManifestLoader.Load(themeRoot);
        if (themeManifest is null)
        {
            return new ThemeBootstrapResult(themeName, themeRoot, null, null, null, null, null);
        }

        ThemeComponentRegistry? parentRegistry = null;
        string? parentThemeRoot = null;
        if (!string.IsNullOrWhiteSpace(themeManifest.Extends))
        {
            parentThemeRoot = Path.Combine(rootDir, "themes", themeManifest.Extends);
            var parentManifest = ThemeManifestLoader.Load(parentThemeRoot);
            if (parentManifest is not null)
            {
                parentRegistry = new ThemeComponentRegistry(parentThemeRoot, parentManifest, null);
            }
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

        return new ThemeBootstrapResult(themeName, themeRoot, parentThemeRoot, themeManifest, themeRegistry, schemaValidator, resolvedSectionPlugins);
    }
}
