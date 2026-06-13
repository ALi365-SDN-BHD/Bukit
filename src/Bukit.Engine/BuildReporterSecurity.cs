using System.Text.Json;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class BuildReporterSecurity
{
    internal static void EnforceSecurityGate(AppConfig config, SecurityReportData? securityData, bool isCi)
    {
        if (securityData is null)
        {
            return;
        }

        var mode = ResolveSecurityFailMode(config.Build.Report, isCi);
        if (mode == "off")
        {
            return;
        }

        var status = ResolveSecurityStatus(securityData);
        if (!string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var message = $"BKT-BUILD-SECURITY-0001: security-report.json contains failed checks (mode={mode}).";
        if (mode == "warn")
        {
            Console.Error.WriteLine(message);
            return;
        }

        throw new InvalidOperationException(message);
    }

    internal static void WriteSecurityReport(string path, AppConfig config, SecurityReportData? data)
    {
        data ??= new SecurityReportData(
            RouteTraversal: "not_checked",
            UnsafeSlug: "not_checked",
            PluginOutputPath: "not_checked",
            RemoteThemeLock: "not_checked",
            Warnings: new[] { "Security checks were not executed for this report." },
            Errors: Array.Empty<string>());

        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        BuildReporter.WriteArtifactContract(writer, BuildReporter.SecurityReportSchema);
        writer.WriteString("status", ResolveSecurityStatus(data));
        writer.WritePropertyName("warnings");
        writer.WriteStartArray();
        foreach (var w in data.Warnings)
        {
            writer.WriteStringValue(w);
        }
        writer.WriteEndArray();
        writer.WritePropertyName("errors");
        writer.WriteStartArray();
        foreach (var e in data.Errors)
        {
            writer.WriteStringValue(e);
        }
        writer.WriteEndArray();
        writer.WritePropertyName("checks");
        writer.WriteStartObject();
        WriteSecurityCheck(writer, "routeTraversal", data.RouteTraversal, "error");
        WriteSecurityCheck(writer, "unsafeSlug", data.UnsafeSlug, "error");
        WriteSecurityCheck(writer, "pluginOutputPath", data.PluginOutputPath, "error");
        WriteSecurityCheck(writer, "remoteThemeLock", data.RemoteThemeLock, "warning");
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteSecurityCheck(Utf8JsonWriter writer, string name, string status, string severity)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        writer.WriteString("status", status);
        writer.WriteString("severity", severity);
        writer.WriteEndObject();
    }

    internal static SecurityReportData CreateSecurityReportData(
        AppConfig config,
        string rootDir,
        string outputDir,
        IReadOnlyList<BuildVariantResult> variants)
    {
        var warnings = new List<string>();
        var errors = new List<string>();

        var routeTraversal = CheckRoutes(variants, errors);
        var unsafeSlug = CheckSlugs(variants, errors);
        var pluginOutputPath = CheckPluginOutputs(outputDir, variants, errors);
        var remoteThemeLock = CheckRemoteThemeLock(config, rootDir, warnings, errors);

        return new SecurityReportData(
            routeTraversal,
            unsafeSlug,
            pluginOutputPath,
            remoteThemeLock,
            warnings,
            errors);
    }

    private static string ResolveSecurityStatus(SecurityReportData data)
    {
        if (data.Errors.Count > 0 ||
            IsFailed(data.RouteTraversal) ||
            IsFailed(data.UnsafeSlug) ||
            IsFailed(data.PluginOutputPath) ||
            IsFailed(data.RemoteThemeLock))
        {
            return "failed";
        }

        if (data.Warnings.Count > 0 ||
            IsWarning(data.RouteTraversal) ||
            IsWarning(data.UnsafeSlug) ||
            IsWarning(data.PluginOutputPath) ||
            IsWarning(data.RemoteThemeLock))
        {
            return "warning";
        }

        return "passed";
    }

    private static bool IsFailed(string status)
        => string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase);

    private static bool IsWarning(string status)
        => string.Equals(status, "warning", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "not_checked", StringComparison.OrdinalIgnoreCase);

    private static string ResolveSecurityFailMode(BuildReportConfig report, bool isCi)
    {
        var mode = (report.SecurityFailMode ?? "auto").Trim().ToLowerInvariant();
        if (mode == "auto")
        {
            return IsStrictSecurityContext(isCi) ? "strict" : "warn";
        }

        return mode;
    }

    private static bool IsStrictSecurityContext(bool isCi)
    {
        return isCi || IsReleaseProfileContext();
    }

    private static bool IsReleaseProfileContext()
    {
        var buildMode = Environment.GetEnvironmentVariable("BUKIT_BUILD_MODE")?.Trim().ToLowerInvariant();
        if (buildMode is "release" or "profile")
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BUKIT_PROFILE")))
        {
            return true;
        }

        var releaseFlag = Environment.GetEnvironmentVariable("BUKIT_RELEASE")?.Trim();
        if (string.IsNullOrWhiteSpace(releaseFlag))
        {
            return false;
        }

        return !string.Equals(releaseFlag, "0", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(releaseFlag, "false", StringComparison.OrdinalIgnoreCase);
    }

    private static string CheckRoutes(IReadOnlyList<BuildVariantResult> variants, List<string> errors)
    {
        try
        {
            foreach (var route in BuildReporter.EnumerateRoutes(variants))
            {
                RouteSecurityValidator.ValidateInternalUrl(route.Url, "security-report route.url");
                RouteSecurityValidator.ValidateOutputPath(route.OutputPath, "security-report route.outputPath");
            }

            return "passed";
        }
        catch (ConfigException ex)
        {
            errors.Add(ex.Message);
            return "failed";
        }
    }

    private static string CheckSlugs(IReadOnlyList<BuildVariantResult> variants, List<string> errors)
    {
        try
        {
            foreach (var document in variants
                .SelectMany(v => v.RoutedDocuments.Concat(v.DerivedDocuments))
                .Select(x => x.Document))
            {
                RouteSecurityValidator.ValidateSlugSegment(document.Slug, $"security-report slug for {document.Id}");
            }

            return "passed";
        }
        catch (ConfigException ex)
        {
            errors.Add(ex.Message);
            return "failed";
        }
    }

    private static string CheckPluginOutputs(string outputDir, IReadOnlyList<BuildVariantResult> variants, List<string> errors)
    {
        var pluginOutputs = variants.SelectMany(v => v.PluginOutputs).ToArray();
        if (pluginOutputs.Length == 0)
        {
            return "not_applicable";
        }

        try
        {
            var safeRoot = Path.GetFullPath(outputDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (var pluginOutput in pluginOutputs)
            {
                RouteSecurityValidator.ValidateOutputPath(pluginOutput.Path, $"security-report plugin output for {pluginOutput.Plugin}");
                var fullPath = Path.GetFullPath(Path.Combine(outputDir, pluginOutput.Path.Replace('/', Path.DirectorySeparatorChar)));
                if (!fullPath.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ConfigException($"Plugin output escapes outputDir: {pluginOutput.Path}", DiagnosticCode.PluginOutputTraversal);
                }
            }

            return "passed";
        }
        catch (ConfigException ex)
        {
            errors.Add(ex.Message);
            return "failed";
        }
    }

    private static string CheckRemoteThemeLock(AppConfig config, string rootDir, List<string> warnings, List<string> errors)
    {
        var source = config.Theme.Source;
        if (string.IsNullOrWhiteSpace(source))
        {
            return "not_applicable";
        }

        var trimmed = source.Trim();
        if (!LooksRemoteThemeSource(trimmed))
        {
            return "not_applicable";
        }

        if (!trimmed.Contains('@', StringComparison.Ordinal))
        {
            warnings.Add($"Remote theme source '{trimmed}' is not pinned with an explicit ref.");
            return "warning";
        }

        var lockPath = Path.Combine(rootDir, ".bukit-cache", "themes", "bukit-theme.lock.json");
        if (!File.Exists(lockPath))
        {
            warnings.Add($"Remote theme source '{trimmed}' has no theme lock file at {BuildReporter.NormalizePath(lockPath)}.");
            return "warning";
        }

        return "passed";
    }

    private static bool LooksRemoteThemeSource(string source)
        => source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
           source.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
           source.StartsWith("git@", StringComparison.OrdinalIgnoreCase);
}
