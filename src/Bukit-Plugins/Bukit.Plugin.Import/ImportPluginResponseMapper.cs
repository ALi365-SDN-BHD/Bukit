using Bukit.Importing;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;

namespace Bukit.Plugin.Import;

public static class ImportPluginResponseMapper
{
    public static PluginInvokeResponse FromResult(
        PluginInvokeRequest request,
        ImportCommandResult result,
        ImportPluginConsoleCaptureResult<ImportCommandResult> capture)
    {
        var messages = new List<PluginMessage>();
        var diagnostics = new List<PluginDiagnostic>();

        foreach (var message in result.Messages)
        {
            if (message.Level.Equals("error", StringComparison.OrdinalIgnoreCase))
                diagnostics.Add(new PluginDiagnostic("plugin.import.error", "error", message.Message));
            else
                messages.Add(new PluginMessage(NormalizeMessageLevel(message.Level), message.Message));
        }

        messages.AddRange(capture.StdOutLines.Select(line => new PluginMessage("info", line)));
        diagnostics.AddRange(capture.StdErrLines.Select(line => new PluginDiagnostic("plugin.import.stderr", "error", line)));

        diagnostics.AddRange(result.Diagnostics.Select(diagnostic =>
            new PluginDiagnostic(
                string.IsNullOrWhiteSpace(diagnostic.Code) ? "plugin.import.diagnostic" : diagnostic.Code,
                diagnostic.Severity,
                diagnostic.Message,
                ToProjectRelativePath(request.Context.RootDir, diagnostic.Path))));

        return new PluginInvokeResponse(
            Type: "invokeResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: request.RequestId,
            Success: result.Success,
            ExitCode: result.ExitCode,
            Messages: messages,
            Diagnostics: diagnostics,
            Artifacts: result.Artifacts
                .Select(artifact => new PluginArtifact(
                    artifact.Type,
                    ToProjectRelativePath(request.Context.RootDir, artifact.Path) ?? NormalizeRelativePath(artifact.Path),
                    artifact.Description))
                .ToArray());
    }

    public static PluginInvokeResponse FromOptionsException(PluginInvokeRequest request, ImportPluginOptionsException exception)
        => new(
            Type: "invokeResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: request.RequestId,
            Success: false,
            ExitCode: exception.ExitCode,
            Diagnostics:
            [
                new PluginDiagnostic(exception.Code, "error", exception.Message)
            ]);

    public static PluginInvokeResponse FromException(PluginInvokeRequest request, Exception exception)
        => new(
            Type: "invokeResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: request.RequestId,
            Success: false,
            ExitCode: 1,
            Diagnostics:
            [
                new PluginDiagnostic("plugin.import.failed", "error", exception.Message)
            ]);

    private static string NormalizeMessageLevel(string level)
        => level.Equals("warning", StringComparison.OrdinalIgnoreCase) ||
           level.Equals("warn", StringComparison.OrdinalIgnoreCase)
            ? "warning"
            : level.Equals("error", StringComparison.OrdinalIgnoreCase) ? "error" : "info";

    private static string? ToProjectRelativePath(string rootDir, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!Path.IsPathRooted(path))
            return NormalizeRelativePath(path);

        var root = Path.GetFullPath(rootDir);
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(root, fullPath);
        var normalized = NormalizeRelativePath(relative);
        if (Path.IsPathFullyQualified(relative) ||
            normalized.Equals("..", StringComparison.Ordinal) ||
            normalized.StartsWith("../", StringComparison.Ordinal))
        {
            return Path.GetFileName(fullPath);
        }

        return normalized;
    }

    private static string NormalizeRelativePath(string path)
        => path.Replace('\\', '/');
}
