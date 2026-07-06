using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;
using Bukit.Importing;

namespace Bukit.Plugin.Import;

public static class ImportPluginInvoker
{
    public static async Task<PluginInvokeResponse> InvokeAsync(PluginInvokeRequest request)
    {
        try
        {
            var invocation = ImportPluginOptionsMapper.MapInvocation(request);
            var capture = await ImportPluginConsoleCapture.CaptureAsync(() => RunMappedInvocationAsync(invocation));

            if (capture.Exception is not null)
                return ImportPluginResponseMapper.FromException(request, capture.Exception);

            return ImportPluginResponseMapper.FromResult(request, capture.Result!, capture);
        }
        catch (ImportPluginOptionsException ex)
        {
            return ImportPluginResponseMapper.FromOptionsException(request, ex);
        }
        catch (Exception ex)
        {
            return ImportPluginResponseMapper.FromException(request, ex);
        }
    }

    private static Task<ImportCommandResult> RunMappedInvocationAsync(ImportPluginMappedInvocation invocation)
        => invocation.Kind switch
        {
            ImportPluginInvocationKind.Import when invocation.ImportOptions is not null =>
                ImportCommandWorkflow.RunAsync(invocation.ImportOptions),
            ImportPluginInvocationKind.NotionPush when invocation.NotionPushOptions is not null =>
                RunNotionPushAsync(invocation.NotionPushOptions),
            ImportPluginInvocationKind.NotionValidateSchema when invocation.SchemaValidationOptions is not null =>
                RunNotionValidateSchemaAsync(invocation.SchemaValidationOptions),
            _ => Task.FromResult(new ImportCommandResult
            {
                ExitCode = 2,
                Messages = [new ImportCommandMessage("error", "Unsupported import plugin invocation.")]
            })
        };

    private static async Task<ImportCommandResult> RunNotionPushAsync(ImportNotionSeedPushOptions options)
    {
        var exitCode = await ImportNotionPushWorkflow.PushSeedDirectoryAsync(options);
        return new ImportCommandResult
        {
            ExitCode = exitCode,
            Artifacts = BuildNotionPushArtifacts(options)
        };
    }

    private static async Task<ImportCommandResult> RunNotionValidateSchemaAsync(ImportNotionSchemaValidationOptions options)
    {
        var exitCode = await ImportNotionPushWorkflow.ValidateSchemaAsync(options);
        return new ImportCommandResult
        {
            ExitCode = exitCode,
            Artifacts = BuildSchemaValidationArtifacts(options)
        };
    }

    private static IReadOnlyList<ImportCommandArtifact> BuildNotionPushArtifacts(ImportNotionSeedPushOptions options)
    {
        var artifacts = new List<ImportCommandArtifact>();
        AddArtifactIfExists(artifacts, "report", ResolvePushReportPath(options), "notion push report");

        if (!string.IsNullOrWhiteSpace(options.GeneratedDatabaseMapPath))
        {
            AddArtifactIfExists(
                artifacts,
                "database-map",
                Path.GetFullPath(options.GeneratedDatabaseMapPath),
                "generated notion database map");
        }

        if (options.CreateMissingDatabases && !string.IsNullOrWhiteSpace(options.InputDir))
        {
            AddArtifactIfExists(
                artifacts,
                "database-map",
                options.DatabaseMapPath,
                "generated notion database map");
            var inputDir = Path.GetFullPath(options.InputDir);
            AddArtifactIfExists(
                artifacts,
                "database-map",
                Path.Combine(inputDir, "notion-database-map.yaml"),
                "generated notion database map");
            AddArtifactIfExists(
                artifacts,
                "database-map",
                Path.Combine(inputDir, "notion-database-map.generated.yaml"),
                "generated notion database map");
        }

        return artifacts
            .DistinctBy(artifact => Path.GetFullPath(artifact.Path))
            .ToArray();
    }

    private static IReadOnlyList<ImportCommandArtifact> BuildSchemaValidationArtifacts(ImportNotionSchemaValidationOptions options)
    {
        var artifacts = new List<ImportCommandArtifact>();
        AddArtifactIfExists(artifacts, "report", options.ReportPath, "notion schema validation report");
        return artifacts;
    }

    private static string? ResolvePushReportPath(ImportNotionSeedPushOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ReportPath))
            return Path.GetFullPath(options.ReportPath);
        if (string.IsNullOrWhiteSpace(options.InputDir))
            return null;
        return Path.Combine(
            Path.GetFullPath(options.InputDir),
            options.DryRun ? "notion-push-plan.json" : "notion-push-report.json");
    }

    private static void AddArtifactIfExists(
        List<ImportCommandArtifact> artifacts,
        string type,
        string? path,
        string description)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var fullPath = Path.GetFullPath(path);
        if (File.Exists(fullPath))
            artifacts.Add(new ImportCommandArtifact(type, fullPath, description));
    }
}
