using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class BuildReporter
{
    internal const string ReportDirectoryName = ".bukit";
    internal const string ArtifactSchemaVersion = "1.0";
    internal const string BuildReportSchema = "https://bukit.dev/schemas/build-report.v1.json";
    internal const string RoutesReportSchema = "https://bukit.dev/schemas/routes.v1.json";
    internal const string AssetsReportSchema = "https://bukit.dev/schemas/assets.v1.json";
    internal const string IncrementalManifestSchema = "https://bukit.dev/schemas/incremental-manifest.v1.json";
    internal const string SecurityReportSchema = "https://bukit.dev/schemas/security-report.v1.json";
    internal const string ArtifactManifestSchema = "https://bukit.dev/schemas/artifact-manifest.v1.json";
    internal const string ReleaseBundleChecksumsSchema = "https://bukit.dev/schemas/release-bundle-checksums.v1.json";
    internal const string BuildManifestDigestSchema = "https://bukit.dev/schemas/build-manifest-digest.v1.json";

    internal static void WriteIfEnabled(
        AppConfig config,
        string rootDir,
        string outputDir,
        BuildResult result,
        IReadOnlyList<BuildVariantResult> variants,
        ILogger logger,
        SecurityReportData? securityData = null)
    {
        var reportDir = Path.Combine(outputDir, ReportDirectoryName);
        Directory.CreateDirectory(reportDir);
        WriteSecurityReport(Path.Combine(reportDir, "security-report.json"), config, securityData);

        if (!config.Build.Report.Enabled)
        {
            WriteReleaseBundleChecksums(reportDir, outputDir);
            WriteArtifactManifest(reportDir);
            WriteBuildManifestDigest(reportDir);
            logger.Debug($"event=build.security_report.write dir={reportDir} root={rootDir}");
            return;
        }

        WriteBuildReport(Path.Combine(reportDir, "build-report.json"), result);
        WriteRoutes(Path.Combine(reportDir, "routes.json"), variants);
        WriteAssets(Path.Combine(reportDir, "assets.json"), outputDir);
        WriteIncrementalManifest(Path.Combine(reportDir, "incremental-manifest.json"), result, variants);
        WriteReleaseBundleChecksums(reportDir, outputDir);
        WriteArtifactManifest(reportDir);
        WriteBuildManifestDigest(reportDir);
        logger.Debug($"event=build.report.write dir={reportDir} root={rootDir}");
    }

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

    private static void WriteBuildReport(string path, BuildResult result)
    {
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        WriteArtifactContract(writer, BuildReportSchema);
        writer.WriteString("version", result.Version);
        writer.WriteString("startedAt", result.StartedAt);
        writer.WriteString("endedAt", result.EndedAt);
        writer.WriteNumber("durationMs", result.DurationMs);
        writer.WritePropertyName("environment");
        writer.WriteStartObject();
        writer.WriteString("os", result.Environment.OS);
        writer.WriteString("runtime", result.Environment.Runtime);
        writer.WriteBoolean("aot", result.Environment.Aot);
        writer.WriteEndObject();
        writer.WritePropertyName("project");
        writer.WriteStartObject();
        writer.WriteString("root", result.Project.Root);
        writer.WriteString("output", result.Project.Output);
        writer.WriteString("contentSource", result.Project.ContentSource);
        writer.WriteString("themeName", result.Project.ThemeName);
        writer.WriteString("themeSource", result.Project.ThemeSource);
        writer.WriteEndObject();
        writer.WritePropertyName("summary");
        writer.WriteStartObject();
        writer.WriteNumber("pageCount", result.Summary.PageCount);
        writer.WriteNumber("routeCount", result.Summary.RouteCount);
        writer.WriteNumber("assetCount", result.Summary.AssetCount);
        writer.WriteNumber("mediaCount", result.Summary.MediaCount);
        writer.WriteNumber("pluginCount", result.Summary.PluginCount);
        writer.WriteNumber("warningCount", result.Summary.WarningCount);
        writer.WriteNumber("errorCount", result.Summary.ErrorCount);
        writer.WriteNumber("schemaErrorCount", result.Summary.SchemaErrorCount);
        writer.WriteEndObject();
        writer.WritePropertyName("incremental");
        writer.WriteStartObject();
        writer.WriteBoolean("enabled", result.Incremental.Enabled);
        writer.WriteNumber("cacheHitCount", result.Incremental.CacheHitCount);
        writer.WriteNumber("cacheMissCount", result.Incremental.CacheMissCount);
        writer.WriteEndObject();
        writer.WritePropertyName("generatedFiles");
        writer.WriteStartArray();
        foreach (var file in result.GeneratedFiles.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteStringValue(file);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteRoutes(string path, IReadOnlyList<BuildVariantResult> variants)
    {
        var entries = BuildRouteEntries(variants);
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        WriteArtifactContract(writer, RoutesReportSchema);
        writer.WritePropertyName("routes");
        writer.WriteStartArray();
        foreach (var entry in entries)
        {
            writer.WriteStartObject();
            writer.WriteString("url", entry.Url);
            writer.WriteString("outputPath", NormalizePath(entry.OutputPath));
            writer.WriteString("template", NormalizePath(entry.Template));
            writer.WriteString("source", entry.Source);
            writer.WriteString("kind", entry.Kind);
            writer.WriteString("language", entry.Language);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteAssets(string path, string outputDir)
    {
        var assets = EnumerateAssets(outputDir).ToList();
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        WriteArtifactContract(writer, AssetsReportSchema);
        writer.WritePropertyName("assets");
        writer.WriteStartArray();
        foreach (var asset in assets)
        {
            writer.WriteStartObject();
            writer.WriteString("path", asset.Path);
            writer.WriteString("source", asset.Source);
            writer.WriteString("hash", asset.Hash);
            writer.WriteNumber("size", asset.Size);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteIncrementalManifest(string path, BuildResult result, IReadOnlyList<BuildVariantResult> variants)
    {
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        WriteArtifactContract(writer, IncrementalManifestSchema);
        writer.WriteBoolean("enabled", result.Incremental.Enabled);
        writer.WriteNumber("cacheHitCount", result.Incremental.CacheHitCount);
        writer.WriteNumber("cacheMissCount", result.Incremental.CacheMissCount);
        writer.WritePropertyName("renderReasons");
        writer.WriteStartObject();
        foreach (var reason in variants.SelectMany(v => v.RenderReasons).GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteNumber(reason.Key, reason.Sum(x => x.Value));
        }

        writer.WriteEndObject();
        writer.WritePropertyName("variants");
        writer.WriteStartArray();
        foreach (var variant in variants.OrderBy(v => v.Language, StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteStartObject();
            writer.WriteString("language", variant.Language);
            writer.WriteString("outputDir", variant.OutputDir);
            writer.WriteNumber("renderedCount", variant.RenderedCount);
            writer.WriteNumber("skippedCount", variant.SkippedCount);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteArtifactManifest(string reportDir)
    {
        var manifestPath = Path.Combine(reportDir, "artifact-manifest.json");
        var artifactFiles = Directory.EnumerateFiles(reportDir, "*.json", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(Path.GetFileName(path), "artifact-manifest.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        using var stream = File.Create(manifestPath);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        WriteArtifactContract(writer, ArtifactManifestSchema);
        writer.WriteString("generatedAt", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        var artifactEntries = artifactFiles
            .Select(path => new ReleaseBundleFileEntry(
                NormalizePath(Path.GetRelativePath(reportDir, path)),
                ComputeSha256(path),
                new FileInfo(path).Length))
            .ToList();
        writer.WriteNumber("artifactCount", artifactEntries.Count);
        writer.WriteString("artifactSetHash", ComputeBundleHash(artifactEntries));
        writer.WritePropertyName("artifacts");
        writer.WriteStartArray();
        foreach (var artifact in artifactEntries)
        {
            writer.WriteStartObject();
            writer.WriteString("path", artifact.Path);
            writer.WriteString("hash", artifact.Hash);
            writer.WriteNumber("size", artifact.Size);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteReleaseBundleChecksums(string reportDir, string outputDir)
    {
        var bundlePath = Path.Combine(reportDir, "release-bundle-checksums.json");
        var files = Directory.Exists(outputDir)
            ? Directory.EnumerateFiles(outputDir, "*", SearchOption.AllDirectories)
                .Where(path => !IsUnderReportDirectory(path, reportDir))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => new ReleaseBundleFileEntry(
                    NormalizePath(Path.GetRelativePath(outputDir, path)),
                    ComputeSha256(path),
                    new FileInfo(path).Length))
                .ToList()
            : new List<ReleaseBundleFileEntry>();

        using var stream = File.Create(bundlePath);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        WriteArtifactContract(writer, ReleaseBundleChecksumsSchema);
        writer.WriteNumber("fileCount", files.Count);
        writer.WriteString("bundleHash", ComputeBundleHash(files));
        writer.WritePropertyName("files");
        writer.WriteStartArray();
        foreach (var file in files)
        {
            writer.WriteStartObject();
            writer.WriteString("path", file.Path);
            writer.WriteString("hash", file.Hash);
            writer.WriteNumber("size", file.Size);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteBuildManifestDigest(string reportDir)
    {
        var digestPath = Path.Combine(reportDir, "build-manifest-digest.json");
        var reportFiles = Directory.EnumerateFiles(reportDir, "*.json", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(Path.GetFileName(path), "build-manifest-digest.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new ReleaseBundleFileEntry(
                NormalizePath(Path.GetRelativePath(reportDir, path)),
                ComputeSha256(path),
                new FileInfo(path).Length))
            .ToList();

        var releaseBundle = reportFiles.FirstOrDefault(x =>
            string.Equals(x.Path, "release-bundle-checksums.json", StringComparison.OrdinalIgnoreCase));
        var artifactManifest = reportFiles.FirstOrDefault(x =>
            string.Equals(x.Path, "artifact-manifest.json", StringComparison.OrdinalIgnoreCase));
        var securityReport = reportFiles.FirstOrDefault(x =>
            string.Equals(x.Path, "security-report.json", StringComparison.OrdinalIgnoreCase));

        using var stream = File.Create(digestPath);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        WriteArtifactContract(writer, BuildManifestDigestSchema);
        writer.WriteString("generatedAt", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteNumber("reportCount", reportFiles.Count);
        writer.WriteString("reportSetHash", ComputeBundleHash(reportFiles));
        writer.WriteString("artifactManifestHash", artifactManifest?.Hash);
        writer.WriteString("releaseBundleHash", releaseBundle?.Hash);
        writer.WriteString("securityReportHash", securityReport?.Hash);
        writer.WritePropertyName("reports");
        writer.WriteStartArray();
        foreach (var report in reportFiles)
        {
            writer.WriteStartObject();
            writer.WriteString("path", report.Path);
            writer.WriteString("hash", report.Hash);
            writer.WriteNumber("size", report.Size);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteSecurityReport(string path, AppConfig config, SecurityReportData? data)
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
        WriteArtifactContract(writer, SecurityReportSchema);
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
        WriteExternalPluginGovernance(writer, config);
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

    private static void WriteExternalPluginGovernance(Utf8JsonWriter writer, AppConfig config)
    {
        writer.WritePropertyName("externalPlugins");
        writer.WriteStartObject();
        writer.WriteString("policy", config.Site.ExternalPluginPolicy.ToString().ToLowerInvariant());

        var plugins = config.Site.ExternalPlugins?
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? Array.Empty<KeyValuePair<string, ExternalPluginConfig>>();

        writer.WriteNumber("pluginCount", plugins.Length);
        writer.WritePropertyName("plugins");
        writer.WriteStartArray();
        foreach (var (name, plugin) in plugins)
        {
            writer.WriteStartObject();
            writer.WriteString("name", name);
            writer.WriteBoolean("enabled", plugin.Enabled);
            writer.WriteString("runtime", plugin.Runtime);
            writer.WritePropertyName("hooks");
            WriteStringArray(writer, plugin.Hooks);
            writer.WritePropertyName("capabilities");
            WriteStringArray(writer, plugin.Capabilities);
            writer.WritePropertyName("templateRequirements");
            WriteStringArray(writer, plugin.TemplateRequirements);
            writer.WritePropertyName("allowEnvironment");
            WriteStringArray(writer, plugin.AllowEnvironment);
            writer.WriteBoolean("allowAbsoluteEntry", plugin.AllowAbsoluteEntry);
            writer.WriteBoolean("sha256Pinned", !string.IsNullOrWhiteSpace(plugin.Sha256));
            writer.WritePropertyName("declaredContract");
            writer.WriteStartObject();
            writer.WriteBoolean("spawnsProcess", string.Equals(plugin.Runtime, "process", StringComparison.OrdinalIgnoreCase));
            writer.WriteBoolean("absoluteEntryOptIn", plugin.AllowAbsoluteEntry);
            writer.WriteBoolean("declaresExtraEnvironment", plugin.AllowEnvironment is { Count: > 0 });
            writer.WriteBoolean("declaresTemplateRequirements", plugin.TemplateRequirements is { Count: > 0 });
            writer.WriteBoolean("declaresIntegrityPin", !string.IsNullOrWhiteSpace(plugin.Sha256));
            writer.WriteEndObject();
            writer.WritePropertyName("enforcedBoundaries");
            writer.WriteStartObject();
            writer.WriteString("entryScope", plugin.AllowAbsoluteEntry ? "absolute-opt-in-or-project-relative" : "project-relative-only");
            writer.WriteString("outputScope", "outputDir-only");
            writer.WriteString("networkAccess", "not-declared-by-contract");
            writer.WriteNumber("timeoutMs", plugin.TimeoutMs);
            writer.WriteNumber("maxStdoutBytes", plugin.MaxStdoutBytes);
            writer.WriteNumber("maxStderrBytes", plugin.MaxStderrBytes);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteStringArray(Utf8JsonWriter writer, IReadOnlyList<string>? values)
    {
        writer.WriteStartArray();
        foreach (var value in values ?? Array.Empty<string>())
        {
            writer.WriteStringValue(value);
        }
        writer.WriteEndArray();
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
            foreach (var route in EnumerateRoutes(variants))
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

    private static bool IsUnderReportDirectory(string path, string reportDir)
    {
        var reportRoot = Path.GetFullPath(reportDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(reportRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeBundleHash(IReadOnlyList<ReleaseBundleFileEntry> files)
    {
        using var sha = SHA256.Create();
        foreach (var file in files)
        {
            var line = $"{file.Path}|{file.Hash}|{file.Size}\n";
            var bytes = System.Text.Encoding.UTF8.GetBytes(line);
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return $"sha256:{Convert.ToHexStringLower(sha.Hash!)}";
    }

    private sealed record ReleaseBundleFileEntry(string Path, string Hash, long Size);

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
            warnings.Add($"Remote theme source '{trimmed}' has no theme lock file at {NormalizePath(lockPath)}.");
            return "warning";
        }

        return "passed";
    }

    private static bool LooksRemoteThemeSource(string source)
        => source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
           source.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
           source.StartsWith("git@", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<RouteInfo> EnumerateRoutes(IReadOnlyList<BuildVariantResult> variants)
    {
        foreach (var variant in variants)
        {
            foreach (var route in variant.RoutedDocuments.Select(x => x.Route))
            {
                yield return route;
            }

            foreach (var route in variant.DerivedDocuments.Select(x => x.Route))
            {
                yield return route;
            }

            foreach (var route in variant.StaticRoutes)
            {
                yield return route;
            }
        }
    }

    private static void WriteArtifactContract(Utf8JsonWriter writer, string schema)
    {
        writer.WriteString("schema", schema);
        writer.WriteString("schemaVersion", ArtifactSchemaVersion);
    }

    private static IReadOnlyList<RouteReportEntry> BuildRouteEntries(IReadOnlyList<BuildVariantResult> variants)
    {
        return variants
            .SelectMany(variant => variant.RoutedDocuments.Concat(variant.DerivedDocuments).Select(route => new RouteReportEntry(
                route.Route.Url,
                route.Route.OutputPath,
                route.Route.Template,
                GetSource(route.Document),
                GetKind(route.Document),
                variant.Language)))
            .Concat(variants.SelectMany(variant => variant.StaticRoutes.Select(route => new RouteReportEntry(
                route.Url,
                route.OutputPath,
                route.Template,
                null,
                "static",
                variant.Language))))
            .Concat(variants.SelectMany(variant => variant.PluginOutputs.Select(plugin => new RouteReportEntry(
                BuildPluginRouteUrl(plugin.Path),
                NormalizePath(plugin.Path),
                string.Empty,
                plugin.Plugin,
                "plugin",
                variant.Language))))
            .OrderBy(entry => entry.Url, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Language, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildPluginRouteUrl(string outputPath)
    {
        var normalizedPath = NormalizePath(outputPath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return string.Empty;
        }

        return "/" + normalizedPath.TrimStart('/');
    }

    private static IEnumerable<AssetReportEntry> EnumerateAssets(string outputDir)
    {
        var excludedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ReportDirectoryName
        };

        foreach (var file in Directory.EnumerateFiles(outputDir, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var relative = NormalizePath(Path.GetRelativePath(outputDir, file));
            if (relative is ".bukit-build-state.json" or ".bukit-output-marker")
            {
                continue;
            }

            var dirName = relative.Split('/')[0];
            if (excludedDirs.Contains(dirName))
            {
                continue;
            }

            var info = new FileInfo(file);
            yield return new AssetReportEntry(relative, relative, ComputeSha256(file), info.Length);
        }
    }

    private static string? GetSource(ContentDocument document)
    {
        foreach (var key in new[] { "source", "sourcePath", "path", "file" })
        {
            var value = ContentFieldReader.GetText(document.CustomFields, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string GetKind(ContentDocument document)
    {
        return ContentFieldReader.GetCollection(document);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string NormalizePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private sealed record RouteReportEntry(
        string Url,
        string OutputPath,
        string Template,
        string? Source,
        string Kind,
        string Language);

    private sealed record AssetReportEntry(
        string Path,
        string? Source,
        string Hash,
        long Size);
}

internal sealed record SecurityReportData(
    string RouteTraversal,
    string UnsafeSlug,
    string PluginOutputPath,
    string RemoteThemeLock,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors);
