using System.Text.Json;
using Bukit.IndexNow;
using Bukit.Plugin.Abstractions.Protocol;

namespace Bukit.Plugin.IndexNow;

public static class IndexNowPluginOptionsMapper
{
    private static readonly HashSet<string> AllowedOptions = new(StringComparer.Ordinal)
    {
        "--change-set",
        "--snapshot",
        "--site-url",
        "--state-dir",
        "--dry-run"
    };

    public static IndexNowPluginMappedInvocation Map(PluginInvokeRequest request)
    {
        var path = request.Command.Path.Count == 0 ? [request.Command.Name] : request.Command.Path;
        if (path.Count != 2 ||
            !string.Equals(path[0], "indexnow", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(path[1], "submit", StringComparison.OrdinalIgnoreCase))
        {
            throw new IndexNowPluginOptionsException(
                "plugin.indexnow.unknownCommand",
                $"Unsupported command path: {string.Join(" ", path)}");
        }

        var unknown = request.Command.Options.Keys.FirstOrDefault(name => !AllowedOptions.Contains(name));
        if (unknown is not null)
        {
            throw new IndexNowPluginOptionsException(
                "plugin.indexnow.unknownOption",
                $"Unsupported IndexNow option: {unknown}");
        }

        var rootDir = NormalizeRoot(request.Context.RootDir);
        var workingDir = ResolveUnderRoot(rootDir, rootDir, request.Context.WorkingDir, "workingDir");
        var changeSetPath = ResolveRequiredFile(rootDir, workingDir, ReadRequired(request, "--change-set"), "--change-set");
        var snapshotPath = ResolveRequiredFile(rootDir, workingDir, ReadRequired(request, "--snapshot"), "--snapshot");
        var snapshotDirectory = Path.GetDirectoryName(snapshotPath);
        if (snapshotDirectory is null ||
            !string.Equals(Path.GetFileName(snapshotDirectory), ".bukit", StringComparison.Ordinal) ||
            !string.Equals(Path.GetFileName(snapshotPath), "publish-url-snapshot.json", StringComparison.Ordinal))
        {
            throw new IndexNowPluginOptionsException(
                "plugin.indexnow.invalidSnapshotLayout",
                "--snapshot must be <output>/.bukit/publish-url-snapshot.json.");
        }

        var outputRoot = Path.GetDirectoryName(snapshotDirectory)
                         ?? throw new IndexNowPluginOptionsException(
                             "plugin.indexnow.invalidSnapshotLayout",
                             "--snapshot has no production output root.");
        EnsureUnderRoot(rootDir, outputRoot, "production output root");

        Uri siteUrl;
        try
        {
            siteUrl = IndexNowUrlPolicy.ParseSiteUrl(ReadRequired(request, "--site-url"));
        }
        catch (InvalidOperationException)
        {
            throw new IndexNowPluginOptionsException(
                "plugin.indexnow.invalidSiteUrl",
                "--site-url must be exactly https://silushangxun.com/.");
        }

        var stateDirValue = ReadRequired(request, "--state-dir");
        string stateFile;
        try
        {
            stateFile = IndexNowStateStore.ResolveStateFile(rootDir, stateDirValue);
        }
        catch (InvalidOperationException exception)
        {
            throw new IndexNowPluginOptionsException("plugin.indexnow.pathDenied", exception.Message);
        }

        var dryRun = ReadBool(request, "--dry-run");
        string? key = null;
        if (!dryRun)
        {
            if (!request.Permissions.Network)
            {
                throw new IndexNowPluginOptionsException(
                    "plugin.indexnow.networkDenied",
                    "Network permission is required.");
            }

            if (!request.Permissions.Environment.Read.Contains("INDEXNOW_KEY", StringComparer.Ordinal))
            {
                throw new IndexNowPluginOptionsException(
                    "plugin.indexnow.envDenied",
                    "INDEXNOW_KEY must be granted in permissions.environment.read.");
            }

            if (!request.Context.Environment.TryGetValue("INDEXNOW_KEY", out key) ||
                string.IsNullOrWhiteSpace(key))
            {
                throw new IndexNowPluginOptionsException(
                    "plugin.indexnow.envMissing",
                    "INDEXNOW_KEY is not set.");
            }
        }

        return new IndexNowPluginMappedInvocation(
            rootDir,
            workingDir,
            changeSetPath,
            snapshotPath,
            siteUrl,
            Path.GetDirectoryName(stateFile)!,
            outputRoot,
            key,
            dryRun);
    }

    private static string NormalizeRoot(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new IndexNowPluginOptionsException("plugin.indexnow.invalidContext", "Plugin rootDir is required.");
        }

        return Path.GetFullPath(value);
    }

    private static string ResolveRequiredFile(string root, string working, string value, string name)
    {
        var path = ResolveUnderRoot(root, working, value, name);
        if (!File.Exists(path))
        {
            throw new IndexNowPluginOptionsException("plugin.indexnow.missingInput", $"{name} does not exist.");
        }

        EnsureNoSymbolicLinks(root, path, name);
        return path;
    }

    private static string ResolveUnderRoot(string root, string working, string value, string name)
    {
        var path = Path.IsPathRooted(value)
            ? Path.GetFullPath(value)
            : Path.GetFullPath(Path.Combine(working, value));
        EnsureUnderRoot(root, path, name);
        return path;
    }

    private static void EnsureUnderRoot(string root, string path, string name)
    {
        var relative = Path.GetRelativePath(root, path);
        if (Path.IsPathFullyQualified(relative) ||
            relative == ".." ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, PathComparison) ||
            relative.StartsWith(".." + Path.AltDirectorySeparatorChar, PathComparison))
        {
            throw new IndexNowPluginOptionsException(
                "plugin.indexnow.pathDenied",
                $"{name} must stay under the project root.");
        }
    }

    private static void EnsureNoSymbolicLinks(string root, string path, string name)
    {
        var relative = Path.GetRelativePath(root, path);
        var current = root;
        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileSystemInfo info = Directory.Exists(current)
                ? new DirectoryInfo(current)
                : new FileInfo(current);
            if (info.Exists && info.LinkTarget is not null)
            {
                throw new IndexNowPluginOptionsException(
                    "plugin.indexnow.pathDenied",
                    $"{name} must not traverse a symbolic link.");
            }
        }
    }

    private static string ReadRequired(PluginInvokeRequest request, string name)
    {
        var value = ReadString(request, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new IndexNowPluginOptionsException(
                "plugin.indexnow.missingOption",
                $"{name} is required.");
        }

        return value;
    }

    private static string? ReadString(PluginInvokeRequest request, string name)
    {
        if (!request.Command.Options.TryGetValue(name, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
    }

    private static bool ReadBool(PluginInvokeRequest request, string name)
        => request.Command.Options.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static StringComparison PathComparison
        => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}

public sealed record IndexNowPluginMappedInvocation(
    string RootDir,
    string WorkingDir,
    string ChangeSetPath,
    string SnapshotPath,
    Uri SiteUrl,
    string StateDir,
    string OutputRoot,
    string? Key,
    bool DryRun);

public sealed class IndexNowPluginOptionsException : Exception
{
    public IndexNowPluginOptionsException(string code, string message, int exitCode = 2)
        : base(message)
    {
        Code = code;
        ExitCode = exitCode;
    }

    public string Code { get; }

    public int ExitCode { get; }
}
