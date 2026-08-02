using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed class AssetSourceWorkspace : IDisposable
{
    private readonly string? _workspaceRoot;
    private readonly ILogger _logger;

    private AssetSourceWorkspace(
        string assetsDir,
        string? scssOutputDir,
        string? workspaceRoot,
        ILogger logger)
    {
        AssetsDir = assetsDir;
        ScssOutputDir = scssOutputDir;
        _workspaceRoot = workspaceRoot;
        _logger = logger;
    }

    internal string AssetsDir { get; }
    internal string? ScssOutputDir { get; }

    internal static async Task<AssetSourceWorkspace> PrepareAsync(
        string sourceAssetsDir,
        ScssConfig? scssConfig,
        ImageOptimizationConfig? imageConfig,
        ILogger logger,
        bool publishDotFiles,
        bool followSymlinks,
        CancellationToken cancellationToken = default,
        Func<DirectoryCopyItem, string, CancellationToken, Task>? copyFileAsync = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var requiresWorkspace = scssConfig is { Enabled: true } || imageConfig is { Enabled: true };
        if (scssConfig is { Enabled: true, EntryPoint: not null } && !Directory.Exists(sourceAssetsDir))
        {
            throw new ConfigException(
                $"theme.scss.entryPoint '{scssConfig.EntryPoint}' does not exist in the theme assets directory.",
                DiagnosticCode.ConfigInvalidValue);
        }

        if (!requiresWorkspace || !Directory.Exists(sourceAssetsDir))
        {
            return new AssetSourceWorkspace(
                sourceAssetsDir,
                scssOutputDir: null,
                workspaceRoot: null,
                logger);
        }

        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "bukit-asset-workspaces",
            Guid.NewGuid().ToString("N"));
        var workspaceAssetsDir = Path.Combine(workspaceRoot, "assets");
        var scssOutputDir = scssConfig is { Enabled: true }
            ? Path.Combine(workspaceRoot, "scss-output")
            : null;

        try
        {
            var copyOptions = new DirectoryCopyOptions
            {
                IgnoreDotPrefixedFiles = !publishDotFiles,
                FollowSymlinks = followSymlinks
            };
            foreach (var item in DirectoryCopy.EnumerateFilesForSync(
                         sourceAssetsDir,
                         copyOptions,
                         cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var destinationFile = Path.Combine(workspaceAssetsDir, item.RelativePath);
                if (copyFileAsync is not null)
                {
                    await copyFileAsync(item, destinationFile, cancellationToken);
                }
                else
                {
                    await DirectoryCopy.CopyPlannedFileAsync(
                        item.SourcePath,
                        destinationFile,
                        item.PhysicalSourceRoot,
                        copyOptions,
                        cancellationToken);
                }
            }

            await ScssCompiler.CompileIfEnabled(
                workspaceAssetsDir,
                scssConfig,
                logger,
                cancellationToken,
                scssOutputDir);
            await ImageOptimizer.OptimizeIfEnabled(
                workspaceAssetsDir,
                imageConfig,
                logger,
                cancellationToken);

            return new AssetSourceWorkspace(workspaceAssetsDir, scssOutputDir, workspaceRoot, logger);
        }
        catch
        {
            TryDeleteWorkspace(workspaceRoot, logger);
            throw;
        }
    }

    public void Dispose()
    {
        if (_workspaceRoot is not null)
        {
            TryDeleteWorkspace(_workspaceRoot, _logger);
        }
    }

    private static void TryDeleteWorkspace(string workspaceRoot, ILogger logger)
    {
        try
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException ex)
        {
            logger.Warn($"event=asset.workspace.cleanup_failed reason={ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.Warn($"event=asset.workspace.cleanup_failed reason={ex.Message}");
        }
    }
}
