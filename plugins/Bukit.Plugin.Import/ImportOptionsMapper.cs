using System.Text.Json;
using Bukit.Importing;
using Bukit.Importing.HtmlDemo;
using Bukit.Importing.Seed;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;
using Bukit.Shared;

namespace Bukit.Plugin.Import;

public sealed record ImportOptionsMapperResult(
    bool Success,
    ImportSeedOptions? Options,
    IReadOnlyList<PluginDiagnostic>? Diagnostics = null)
{
    public IReadOnlyList<PluginDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
}

public sealed record ImportHtmlDemoDryRunMapperResult(
    bool Success,
    HtmlDemoDryRunOptions? Options,
    string? ThemeName,
    IReadOnlyList<PluginDiagnostic>? Diagnostics = null)
{
    public IReadOnlyList<PluginDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
}

public sealed record ImportHtmlDemoMapperResult(
    bool Success,
    bool DryRun,
    HtmlDemoDryRunOptions? DryRunOptions,
    HtmlDemoImportOptions? ImportOptions,
    IReadOnlyList<PluginDiagnostic>? Diagnostics = null)
{
    public IReadOnlyList<PluginDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
}

public static class ImportOptionsMapper
{
    public static ImportOptionsMapperResult MapSeedOptions(PluginInvokeRequest request)
    {
        var diagnostics = new List<PluginDiagnostic>();
        if (!request.Command.Path.SequenceEqual(["import", "seed"], StringComparer.Ordinal))
        {
            diagnostics.Add(Error(
                "plugin.import.unsupportedCommand",
                "Import plugin only supports the import seed command in this phase."));
        }

        string? seedDirectory = request.Command.Arguments.Count > 0
            ? request.Command.Arguments[0]
            : null;
        if (string.IsNullOrWhiteSpace(seedDirectory))
        {
            diagnostics.Add(Error("import.missingSeedDir", "Missing required argument: <seed-dir>."));
        }

        string? outputDirectory = null;
        if (!request.Command.Options.TryGetValue("--output", out JsonElement outputElement))
        {
            diagnostics.Add(Error("import.missingOutput", "Missing required option: --output."));
        }
        else if (outputElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(outputElement.GetString()))
        {
            diagnostics.Add(Error("import.invalidOutput", "--output must be a non-empty JSON string."));
        }
        else
        {
            outputDirectory = outputElement.GetString();
        }

        bool force = false;
        if (request.Command.Options.TryGetValue("--force", out JsonElement forceElement))
        {
            if (forceElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                force = forceElement.GetBoolean();
            }
            else
            {
                diagnostics.Add(Error("import.invalidForce", "--force must be a JSON boolean."));
            }
        }

        if (diagnostics.Count > 0)
        {
            return new ImportOptionsMapperResult(false, null, diagnostics);
        }

        string root = request.Context.RootDir;
        return new ImportOptionsMapperResult(
            true,
            new ImportSeedOptions(
                ProjectRoot: root,
                SeedDirectory: ResolvePath(root, seedDirectory!),
                OutputDirectory: ResolvePath(root, outputDirectory!),
                Force: force),
            []);
    }

    public static ImportHtmlDemoDryRunMapperResult MapHtmlDemoDryRunOptions(PluginInvokeRequest request)
    {
        ImportHtmlDemoMapperResult mapped = MapHtmlDemoOptions(request);
        return new ImportHtmlDemoDryRunMapperResult(
            mapped.Success && mapped.DryRun,
            mapped.DryRunOptions,
            mapped.ImportOptions?.ThemeName,
            mapped.Success && !mapped.DryRun
                ? [Error("import.htmlDemoDryRunRequired", "Only --dry-run is supported for this mapper.")]
                : mapped.Diagnostics);
    }

    public static ImportHtmlDemoMapperResult MapHtmlDemoOptions(PluginInvokeRequest request)
    {
        var diagnostics = new List<PluginDiagnostic>();
        if (!request.Command.Path.SequenceEqual(["import", "html-demo"], StringComparer.Ordinal))
        {
            diagnostics.Add(Error(
                "plugin.import.unsupportedCommand",
                "Import plugin only supports the import html-demo command for this path."));
        }

        string? demoDirectory = request.Command.Arguments.Count > 0
            ? request.Command.Arguments[0]
            : null;
        if (string.IsNullOrWhiteSpace(demoDirectory))
        {
            diagnostics.Add(Error("import.htmlDemoMissingDemoDir", "Missing required argument: <demo-dir>."));
        }

        string? themeName = null;
        if (!request.Command.Options.TryGetValue("--theme", out JsonElement themeElement))
        {
            diagnostics.Add(Error("import.htmlDemoMissingTheme", "Missing required option: --theme."));
        }
        else if (themeElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(themeElement.GetString()))
        {
            diagnostics.Add(Error("import.htmlDemoInvalidTheme", "--theme must be a non-empty JSON string."));
        }
        else
        {
            themeName = themeElement.GetString();
        }

        bool dryRun = false;
        if (request.Command.Options.TryGetValue("--dry-run", out JsonElement dryRunElement))
        {
            if (dryRunElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                dryRun = dryRunElement.GetBoolean();
            }
            else
            {
                diagnostics.Add(Error("import.htmlDemoInvalidDryRun", "--dry-run must be a JSON boolean."));
            }
        }

        bool use = false;
        if (request.Command.Options.TryGetValue("--use", out JsonElement useElement))
        {
            if (useElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                use = useElement.GetBoolean();
            }
            else
            {
                diagnostics.Add(Error("import.htmlDemoInvalidUse", "--use must be a JSON boolean."));
            }
        }

        bool verify = false;
        if (request.Command.Options.TryGetValue("--verify", out JsonElement verifyElement))
        {
            if (verifyElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                verify = verifyElement.GetBoolean();
            }
            else
            {
                diagnostics.Add(Error("import.htmlDemoInvalidVerify", "--verify must be a JSON boolean."));
            }
        }

        bool force = false;
        if (request.Command.Options.TryGetValue("--force", out JsonElement forceElement))
        {
            if (forceElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                force = forceElement.GetBoolean();
            }
            else
            {
                diagnostics.Add(Error("import.htmlDemoInvalidForce", "--force must be a JSON boolean."));
            }
        }

        string? strictMode = null;
        if (request.Command.Options.TryGetValue("--strict", out JsonElement strictElement))
        {
            if (strictElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(strictElement.GetString()))
            {
                diagnostics.Add(Error("import.htmlDemoInvalidStrict", "--strict must be a non-empty JSON string."));
            }
            else
            {
                strictMode = strictElement.GetString();
                if (strictMode is not ("fail" or "warn"))
                {
                    diagnostics.Add(Error("import.htmlDemoInvalidStrict", "--strict must be either 'fail' or 'warn'."));
                }
            }
        }

        string? routeMapPath = null;
        if (request.Command.Options.TryGetValue("--route-map", out JsonElement routeMapElement))
        {
            if (routeMapElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(routeMapElement.GetString()))
            {
                diagnostics.Add(Error("import.htmlDemoInvalidRouteMap", "--route-map must be a non-empty JSON string."));
            }
            else
            {
                routeMapPath = routeMapElement.GetString();
            }
        }

        bool extractContent = true;
        if (request.Command.Options.TryGetValue("--no-extract-content", out JsonElement noExtractContentElement))
        {
            if (noExtractContentElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                extractContent = !noExtractContentElement.GetBoolean();
            }
            else
            {
                diagnostics.Add(Error("import.htmlDemoInvalidNoExtractContent", "--no-extract-content must be a JSON boolean."));
            }
        }

        bool generateReport = true;
        if (request.Command.Options.TryGetValue("--no-report", out JsonElement noReportElement))
        {
            if (noReportElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                generateReport = !noReportElement.GetBoolean();
            }
            else
            {
                diagnostics.Add(Error("import.htmlDemoInvalidNoReport", "--no-report must be a JSON boolean."));
            }
        }

        string? sitePath = null;
        if (request.Command.Options.TryGetValue("--site-path", out JsonElement sitePathElement))
        {
            if (sitePathElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(sitePathElement.GetString()))
            {
                diagnostics.Add(Error("import.htmlDemoInvalidSitePath", "--site-path must be a non-empty JSON string."));
            }
            else
            {
                sitePath = sitePathElement.GetString();
            }
        }

        string language = "zh";
        if (request.Command.Options.TryGetValue("--language", out JsonElement languageElement))
        {
            if (languageElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(languageElement.GetString()))
            {
                diagnostics.Add(Error("import.htmlDemoInvalidLanguage", "--language must be a non-empty JSON string."));
            }
            else
            {
                language = languageElement.GetString()!;
            }
        }

        string contentSource = "json";
        bool contentSourceSpecified = false;
        if (request.Command.Options.TryGetValue("--content-source", out JsonElement contentSourceElement))
        {
            if (contentSourceElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(contentSourceElement.GetString()))
            {
                diagnostics.Add(Error("import.htmlDemoInvalidContentSource", "--content-source must be a non-empty JSON string."));
            }
            else
            {
                contentSourceSpecified = true;
                contentSource = contentSourceElement.GetString()!;
                if (contentSource is not ("markdown" or "json" or "yaml" or "notion"))
                {
                    diagnostics.Add(Error("import.htmlDemoInvalidContentSource", "--content-source must be markdown, json, yaml, or notion."));
                }
            }
        }

        string buildSource = "markdown";
        if (request.Command.Options.TryGetValue("--build-source", out JsonElement buildSourceElement))
        {
            if (buildSourceElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(buildSourceElement.GetString()))
            {
                diagnostics.Add(Error("import.htmlDemoInvalidBuildSource", "--build-source must be a non-empty JSON string."));
            }
            else
            {
                buildSource = buildSourceElement.GetString()!;
                if (buildSource is not ("markdown" or "notion"))
                {
                    diagnostics.Add(Error("import.htmlDemoInvalidBuildSource", "--build-source must be markdown or notion."));
                }
            }
        }

        bool generateSeed = contentSourceSpecified && contentSource is "json" or "yaml" or "notion";
        if (request.Command.Options.TryGetValue("--no-seed", out JsonElement noSeedElement))
        {
            if (noSeedElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                generateSeed = generateSeed && !noSeedElement.GetBoolean();
            }
            else
            {
                diagnostics.Add(Error("import.htmlDemoInvalidNoSeed", "--no-seed must be a JSON boolean."));
            }
        }

        if (buildSource == "notion" && contentSource != "notion")
        {
            diagnostics.Add(Error("import.htmlDemoInvalidBuildSource", "--build-source notion requires --content-source notion."));
        }

        if (diagnostics.Count > 0)
        {
            return new ImportHtmlDemoMapperResult(false, dryRun, null, null, diagnostics);
        }

        string root = request.Context.RootDir;
        string resolvedDemoDirectory = ResolvePath(root, demoDirectory!);
        if (!IsInsideDirectory(resolvedDemoDirectory, root))
        {
            return new ImportHtmlDemoMapperResult(
                false,
                dryRun,
                null,
                null,
                [Error("import.htmlDemoDirInvalid", "Demo directory must stay inside the project root.")]);
        }

        string? resolvedRouteMapPath = null;
        if (!string.IsNullOrWhiteSpace(routeMapPath))
        {
            resolvedRouteMapPath = ResolvePath(root, routeMapPath);
            if (!IsInsideDirectory(resolvedRouteMapPath, root))
            {
                return new ImportHtmlDemoMapperResult(
                    false,
                    dryRun,
                    null,
                    null,
                    [Error("import.routeMapPathInvalid", "--route-map must stay inside the project root.")]);
            }

            if (!File.Exists(resolvedRouteMapPath))
            {
                return new ImportHtmlDemoMapperResult(
                    false,
                    dryRun,
                    null,
                    null,
                    [Error("import.routeMapNotFound", "Route map file was not found.")]);
            }
        }

        string? resolvedSitePath = null;
        if (!string.IsNullOrWhiteSpace(sitePath))
        {
            resolvedSitePath = Path.GetFullPath(ResolvePath(root, sitePath));
            string sitesRoot = Path.GetFullPath(Path.Combine(root, "sites"));
            if (!IsInsideDirectory(resolvedSitePath, sitesRoot))
            {
                return new ImportHtmlDemoMapperResult(
                    false,
                    dryRun,
                    null,
                    null,
                    [Error("import.sitePathInvalid", "--site-path must stay inside ./sites.")]);
            }
        }

        if (dryRun)
        {
            return new ImportHtmlDemoMapperResult(
                true,
                true,
                new HtmlDemoDryRunOptions(
                    ProjectRoot: root,
                    DemoDirectory: resolvedDemoDirectory,
                    RouteMapPath: resolvedRouteMapPath),
                null,
                []);
        }

        return new ImportHtmlDemoMapperResult(
            true,
            false,
            null,
            new HtmlDemoImportOptions
            {
                InputPath = resolvedDemoDirectory,
                ThemeName = themeName!,
                RootDir = root,
                Force = force,
                Use = use,
                Verify = verify,
                ContentSource = contentSource,
                BuildSource = buildSource,
                GenerateSeed = generateSeed,
                GenerateReport = generateReport,
                PreserveHtml = false,
                RouteMapPath = resolvedRouteMapPath,
                StrictMode = strictMode,
                SitePath = resolvedSitePath,
                Language = language,
                ExtractContent = extractContent
            },
            []);
    }

    private static string ResolvePath(string root, string path)
        => Path.IsPathRooted(path) ? path : Path.Combine(root, path);

    private static bool IsInsideDirectory(string path, string directory)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        try
        {
            return PathUtils.IsSameOrSubPathOf(path, directory);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static PluginDiagnostic Error(string code, string message)
        => new(code, "error", message);
}
