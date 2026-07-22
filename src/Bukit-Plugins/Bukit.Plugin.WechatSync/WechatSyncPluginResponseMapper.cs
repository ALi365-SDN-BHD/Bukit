using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;
using Bukit.WechatSyncing;

namespace Bukit.Plugin.WechatSync;

public static class WechatSyncPluginResponseMapper
{
    public static PluginInvokeResponse FromDryRun(
        PluginInvokeRequest request,
        WechatSyncContext context,
        WechatSyncPluginMappedInvocation invocation)
        => FromDryRun(
            request,
            WechatSyncPlanner.Create(context, invocation.Options, DateTimeOffset.UtcNow),
            invocation);

    private static PluginInvokeResponse FromDryRun(
        PluginInvokeRequest request,
        WechatSyncPlan plan,
        WechatSyncPluginMappedInvocation invocation)
        => new(
            Type: "invokeResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: request.RequestId,
            Success: !plan.HasErrors,
            ExitCode: plan.HasErrors ? 1 : 0,
            Messages:
            [
                new PluginMessage(
                    "info",
                    $"wechat-sync dry-run: candidates={plan.Candidates.Count} output={ToProjectRelativePath(request.Context.RootDir, invocation.OutputDir)} excluded={plan.Exclusions.Count}")
            ],
            Diagnostics: plan.Exclusions
                .Select(exclusion => new PluginDiagnostic(
                    exclusion.Code,
                    exclusion.Severity,
                    exclusion.Message,
                    ToProjectRelativePath(request.Context.RootDir, exclusion.Path)))
                .ToArray());

    public static PluginInvokeResponse FromResult(PluginInvokeRequest request, WechatSyncResult result)
        => new(
            Type: "invokeResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: request.RequestId,
            Success: result.Success,
            ExitCode: result.Success ? 0 : 1,
            Messages: result.Messages
                .Select(message => new PluginMessage(NormalizeMessageLevel(message.Level), message.Message))
                .ToArray(),
            Diagnostics: result.Diagnostics
                .Select(diagnostic => new PluginDiagnostic(
                    diagnostic.Code,
                    diagnostic.Severity,
                    diagnostic.Message,
                    ToProjectRelativePath(request.Context.RootDir, diagnostic.Path)))
                .ToArray(),
            Artifacts:
            [
                new PluginArtifact("cache", ToProjectRelativePath(request.Context.RootDir, result.CachePath) ?? ".cache/wechat-sync/sync-cache.json", "WeChat sync cache")
            ]);

    public static PluginInvokeResponse FromOptionsException(PluginInvokeRequest request, WechatSyncPluginOptionsException exception)
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
                new PluginDiagnostic("plugin.wechat-sync.failed", "error", exception.Message)
            ]);

    private static string NormalizeMessageLevel(string level)
        => level.Equals("warning", StringComparison.OrdinalIgnoreCase) ||
           level.Equals("warn", StringComparison.OrdinalIgnoreCase)
            ? "warning"
            : level.Equals("error", StringComparison.OrdinalIgnoreCase) ? "error" : "info";

    private static string? ToProjectRelativePath(string rootDir, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!Path.IsPathRooted(path))
        {
            return NormalizeRelativePath(path);
        }

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
