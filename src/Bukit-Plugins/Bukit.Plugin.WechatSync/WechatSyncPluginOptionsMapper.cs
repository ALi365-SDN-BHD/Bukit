using System.Text.Json;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.WechatSyncing;

namespace Bukit.Plugin.WechatSync;

public static class WechatSyncPluginOptionsMapper
{
    public static WechatSyncPluginMappedInvocation Map(PluginInvokeRequest request)
    {
        var path = request.Command.Path.Count > 0 ? request.Command.Path : [request.Command.Name];
        if (path.Count != 2 ||
            !path[0].Equals("wechat-sync", StringComparison.OrdinalIgnoreCase) ||
            !path[1].Equals("sync", StringComparison.OrdinalIgnoreCase))
        {
            throw new WechatSyncPluginOptionsException(
                "plugin.wechat-sync.unknownCommand",
                $"Unsupported command path: {string.Join(" ", path)}");
        }

        var rootDir = NormalizeRequiredPath(request.Context.RootDir, "rootDir");
        var workingDir = NormalizeRequiredPath(request.Context.WorkingDir, "workingDir");
        EnsureUnderRoot(rootDir, workingDir, "workingDir");

        var output = ReadString(request, "--output");
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new WechatSyncPluginOptionsException("plugin.wechat-sync.missingOutput", "--output is required.");
        }

        var dryRun = ReadBool(request, "--dry-run");
        var appIdEnv = ReadString(request, "--app-id-env") ?? "WECHAT_APP_ID";
        var appSecretEnv = ReadString(request, "--app-secret-env") ?? "WECHAT_APP_SECRET";
        var forceEnv = ReadString(request, "--force-retry-ignore-cache-env") ?? "BUKIT_WECHAT_FORCE_RETRY";

        if (!dryRun)
        {
            EnsureNetworkGranted(request);
            EnsureEnvironmentGranted(request, appIdEnv);
            EnsureEnvironmentGranted(request, appSecretEnv);
        }

        if (!string.IsNullOrWhiteSpace(forceEnv) &&
            request.Permissions.Environment.Read.Count > 0 &&
            !request.Permissions.Environment.Read.Contains(forceEnv, StringComparer.Ordinal))
        {
            throw new WechatSyncPluginOptionsException(
                "plugin.wechat-sync.envDenied",
                $"Environment variable '{forceEnv}' is not granted. Add it to permissions.environment.read.");
        }

        var outputDir = ResolveUnderRoot(rootDir, workingDir, output, "--output");
        var manifest = ReadString(request, "--manifest");
        var manifestPath = string.IsNullOrWhiteSpace(manifest)
            ? null
            : ResolveUnderRoot(rootDir, workingDir, manifest, "--manifest");
        var mediaDownloadDir = ReadString(request, "--media-download-dir");
        if (!string.IsNullOrWhiteSpace(mediaDownloadDir))
        {
            mediaDownloadDir = ResolveUnderRoot(rootDir, workingDir, mediaDownloadDir, "--media-download-dir");
        }

        var target = ReadString(request, "--target") ?? "draft";
        if (!target.Equals("draft", StringComparison.OrdinalIgnoreCase) &&
            !target.Equals("publish", StringComparison.OrdinalIgnoreCase))
        {
            throw new WechatSyncPluginOptionsException("plugin.wechat-sync.invalidTarget", "--target must be draft or publish.");
        }

        var contentTypes = ParseSet(ReadString(request, "--content-types"));
        if (contentTypes.Count == 0)
        {
            contentTypes = new HashSet<string>(["post", "app"], StringComparer.OrdinalIgnoreCase);
        }

        var defaultTypes = ParseSet(ReadString(request, "--default-types-when-missing"));
        if (defaultTypes.Count == 0)
        {
            defaultTypes = new HashSet<string>(contentTypes, StringComparer.OrdinalIgnoreCase);
        }

        var options = new WechatSyncOptions(
            SourceNames: ParseSet(ReadString(request, "--source-names")),
            ContentTypes: contentTypes,
            DefaultTypesWhenMissing: defaultTypes,
            CacheFile: ReadString(request, "--cache-file") ?? ".cache/wechat-sync/sync-cache.json",
            MaxAttempts: ReadPositiveInt(request, "--max-attempts", 3),
            BaseDelayMs: ReadPositiveInt(request, "--base-delay-ms", 1000),
            BackoffFactor: ReadPositiveInt(request, "--backoff-factor", 2),
            AppIdEnv: appIdEnv,
            AppSecretEnv: appSecretEnv,
            ForceRetryIgnoreCacheEnv: forceEnv,
            Author: ReadString(request, "--author"),
            DefaultThumbMediaId: ReadString(request, "--default-thumb-media-id"),
            NeedOpenComment: ReadBool(request, "--need-open-comment"),
            OnlyFansCanComment: ReadBool(request, "--only-fans-can-comment"),
            SiteName: ReadString(request, "--site-name") ?? "Bukit",
            SiteUrl: ReadString(request, "--site-url"),
            BaseUrl: NormalizeBaseUrl(ReadString(request, "--base-url") ?? "/"),
            ProcessImages: ReadBool(request, "--process-images"),
            Passthrough: ReadBool(request, "--passthrough"),
            Target: target.ToLowerInvariant(),
            PublishPollMaxAttempts: ReadPositiveInt(request, "--poll-max-attempts", 10),
            PublishPollIntervalSeconds: ReadPositiveInt(request, "--poll-interval-seconds", 5),
            Force: ReadBool(request, "--force"),
            DefaultImageUrl: ReadString(request, "--default-image-url"));

        return new WechatSyncPluginMappedInvocation(
            rootDir,
            workingDir,
            outputDir,
            manifestPath,
            mediaDownloadDir,
            dryRun,
            options);
    }

    private static string NormalizeRequiredPath(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new WechatSyncPluginOptionsException("plugin.wechat-sync.invalidContext", $"Plugin context {name} is required.");
        }

        return Path.GetFullPath(value);
    }

    private static string ResolveUnderRoot(string rootDir, string workingDir, string value, string name)
    {
        var combined = Path.IsPathRooted(value)
            ? value
            : Path.Combine(workingDir, value);
        var full = Path.GetFullPath(combined);
        EnsureUnderRoot(rootDir, full, name);
        return full;
    }

    private static void EnsureUnderRoot(string rootDir, string path, string name)
    {
        var root = Path.GetFullPath(rootDir);
        var full = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(root, full).Replace('\\', '/');
        if (Path.IsPathFullyQualified(relative) ||
            relative.Equals("..", StringComparison.Ordinal) ||
            relative.StartsWith("../", StringComparison.Ordinal))
        {
            throw new WechatSyncPluginOptionsException("plugin.wechat-sync.pathDenied", $"{name} must stay under the project root.");
        }
    }

    private static void EnsureNetworkGranted(PluginInvokeRequest request)
    {
        if (!request.Permissions.Network)
        {
            throw new WechatSyncPluginOptionsException("plugin.wechat-sync.networkDenied", "Network permission is required.");
        }
    }

    private static void EnsureEnvironmentGranted(PluginInvokeRequest request, string envName)
    {
        if (string.IsNullOrWhiteSpace(envName))
        {
            throw new WechatSyncPluginOptionsException("plugin.wechat-sync.envDenied", "Environment variable name must not be empty.");
        }

        if (!request.Permissions.Environment.Read.Contains(envName, StringComparer.Ordinal))
        {
            throw new WechatSyncPluginOptionsException(
                "plugin.wechat-sync.envDenied",
                $"Environment variable '{envName}' is not granted. Add it to permissions.environment.read.");
        }

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(envName)))
        {
            throw new WechatSyncPluginOptionsException(
                "plugin.wechat-sync.envMissing",
                $"Environment variable '{envName}' is not set.");
        }
    }

    private static HashSet<string> ParseSet(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static int ReadPositiveInt(PluginInvokeRequest request, string name, int fallback)
    {
        var raw = ReadString(request, name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        if (!int.TryParse(raw, out var value) || value <= 0)
        {
            throw new WechatSyncPluginOptionsException("plugin.wechat-sync.invalidOption", $"{name} must be a positive integer.");
        }

        return value;
    }

    private static string NormalizeBaseUrl(string value)
    {
        value = string.IsNullOrWhiteSpace(value) ? "/" : value.Trim();
        if (!value.StartsWith('/'))
        {
            value = "/" + value;
        }

        return value.Length > 1 ? value.TrimEnd('/') : value;
    }

    private static string? ReadString(PluginInvokeRequest request, string name)
    {
        if (!request.Command.Options.TryGetValue(name, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => value.GetRawText(),
            _ => value.GetRawText()
        };
    }

    private static bool ReadBool(PluginInvokeRequest request, string name)
        => request.Command.Options.TryGetValue(name, out var value) &&
           value.ValueKind is JsonValueKind.True;
}

public sealed record WechatSyncPluginMappedInvocation(
    string RootDir,
    string WorkingDir,
    string OutputDir,
    string? ManifestPath,
    string? MediaDownloadDir,
    bool DryRun,
    WechatSyncOptions Options);

public sealed class WechatSyncPluginOptionsException : Exception
{
    public WechatSyncPluginOptionsException(string code, string message, int exitCode = 2)
        : base(message)
    {
        Code = code;
        ExitCode = exitCode;
    }

    public string Code { get; }
    public int ExitCode { get; }
}
