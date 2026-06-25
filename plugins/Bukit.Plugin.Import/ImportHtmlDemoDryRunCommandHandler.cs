using Bukit.Importing;
using Bukit.Importing.HtmlDemo;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;

namespace Bukit.Plugin.Import;

public static class ImportHtmlDemoDryRunCommandHandler
{
    public static PluginInvokeResponse Handle(string requestId, PluginInvokeRequest request)
    {
        ImportHtmlDemoMapperResult mapped = ImportOptionsMapper.MapHtmlDemoOptions(request);
        if (!mapped.Success)
        {
            return CreateResponse(requestId, success: false, exitCode: 2, mapped.Diagnostics);
        }

        try
        {
            if (mapped.DryRun && mapped.DryRunOptions is not null)
            {
                HtmlDemoDryRunScanResult scanResult = HtmlDemoDryRunScanner.Scan(mapped.DryRunOptions);
                return new PluginInvokeResponse(
                    Type: "invokeResponse",
                    Protocol: PluginProtocolConstants.ProtocolVersion,
                    RequestId: requestId,
                    Success: scanResult.Success,
                    ExitCode: scanResult.ExitCode,
                    Diagnostics: scanResult.Diagnostics.Select(ToPluginDiagnostic).ToArray(),
                    Artifacts: scanResult.Artifacts.Select(ToPluginArtifact).ToArray());
            }

            if (mapped.ImportOptions is null)
            {
                return CreateResponse(
                    requestId,
                    success: false,
                    exitCode: 2,
                    [new PluginDiagnostic("import.htmlDemoInvalidOptions", "error", "Invalid html-demo import options.")]);
            }

            ImportResult result = InvokeWithStdoutRedirectedToStderr(() => HtmlDemoImporter.Import(mapped.ImportOptions));
            return new PluginInvokeResponse(
                Type: "invokeResponse",
                Protocol: PluginProtocolConstants.ProtocolVersion,
                RequestId: requestId,
                Success: true,
                ExitCode: 0,
                Diagnostics: result.Diagnostics.Select(ToPluginDiagnostic).ToArray(),
                Artifacts: ToPluginArtifacts(mapped.ImportOptions, result));
        }
        catch (ImportException ex) when (ex.Kind == ImportErrorKind.UserInput)
        {
            return CreateResponse(
                requestId,
                success: false,
                exitCode: 2,
                [
                    new PluginDiagnostic(
                        Code: "import.htmlDemoImportInvalid",
                        Severity: "error",
                        Message: ex.Message)
                ]);
        }
        catch (Exception ex)
        {
            return CreateResponse(
                requestId,
                success: false,
                exitCode: 1,
                [
                    new PluginDiagnostic(
                        Code: "import.htmlDemoDryRunFailed",
                        Severity: "error",
                        Message: ex.Message)
                ]);
        }
    }

    private static PluginInvokeResponse CreateResponse(
        string requestId,
        bool success,
        int exitCode,
        IReadOnlyList<PluginDiagnostic> diagnostics)
        => new(
            Type: "invokeResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: requestId,
            Success: success,
            ExitCode: exitCode,
            Diagnostics: diagnostics);

    private static PluginDiagnostic ToPluginDiagnostic(HtmlDemoDryRunDiagnostic diagnostic)
        => new(diagnostic.Code, diagnostic.Severity, diagnostic.Message, diagnostic.Path);

    private static PluginDiagnostic ToPluginDiagnostic(ImportDiagnostic diagnostic)
        => new(
            diagnostic.Code,
            diagnostic.Severity.ToString().ToLowerInvariant(),
            diagnostic.Message,
            diagnostic.FilePath);

    private static PluginArtifact ToPluginArtifact(HtmlDemoDryRunArtifact artifact)
        => new(artifact.Type, artifact.Path, artifact.Description);

    private static IReadOnlyList<PluginArtifact> ToPluginArtifacts(HtmlDemoImportOptions options, ImportResult result)
    {
        var artifacts = new List<PluginArtifact>
        {
            new("theme", ToRelativePath(options.RootDir, result.ThemePath), "Generated Bukit theme."),
        };

        if (!string.IsNullOrWhiteSpace(result.SitePath))
        {
            artifacts.Add(new("site", ToRelativePath(options.RootDir, result.SitePath), "Generated Bukit site."));
            string siteConfigPath = Path.Combine(result.SitePath, "site.yaml");
            if (options.Use && File.Exists(siteConfigPath))
            {
                artifacts.Add(new("site-config", ToRelativePath(options.RootDir, siteConfigPath), "Updated Bukit site config."));
            }

            string contentPath = Path.Combine(result.SitePath, "content");
            if (options.ExtractContent && Directory.Exists(contentPath))
            {
                artifacts.Add(new("content", ToRelativePath(options.RootDir, contentPath), "Generated local Markdown content."));
            }

            string notionSeedPath = Path.Combine(result.SitePath, "notion-seed");
            if (options.GenerateSeed &&
                options.ContentSource.Equals("notion", StringComparison.OrdinalIgnoreCase) &&
                Directory.Exists(notionSeedPath))
            {
                artifacts.Add(new("notion-seed", ToRelativePath(options.RootDir, notionSeedPath), "Generated Notion seed handoff files."));
                string databaseMapPath = Path.Combine(notionSeedPath, "notion-database-map.yaml");
                if (File.Exists(databaseMapPath))
                {
                    artifacts.Add(new("notion-database-map", ToRelativePath(options.RootDir, databaseMapPath), "Generated Notion database map candidate."));
                }
            }

            string markdownReportPath = Path.Combine(result.SitePath, "import-report.md");
            if (options.GenerateReport && File.Exists(markdownReportPath))
            {
                artifacts.Add(new("report", ToRelativePath(options.RootDir, markdownReportPath), "Generated import report."));
            }

            if (options.Verify && File.Exists(siteConfigPath))
            {
                artifacts.Add(new("verification", ToRelativePath(options.RootDir, siteConfigPath), "Light import verification."));
            }
        }

        string jsonReportPath = Path.Combine(
            options.RootDir,
            ".bukit",
            "reports",
            "plugin-output",
            "import",
            "html-demo-report.json");
        if (options.GenerateReport && File.Exists(jsonReportPath))
        {
            artifacts.Add(new("report-json", ToRelativePath(options.RootDir, jsonReportPath), "Generated import report JSON."));
        }

        return artifacts;
    }

    private static T InvokeWithStdoutRedirectedToStderr<T>(Func<T> action)
    {
        TextWriter originalOut = Console.Out;
        try
        {
            Console.SetOut(Console.Error);
            return action();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private static string ToRelativePath(string root, string path)
    {
        string relative = Path.GetRelativePath(root, path);
        return relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }
}
