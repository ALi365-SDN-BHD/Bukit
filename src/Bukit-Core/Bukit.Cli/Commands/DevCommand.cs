using Bukit.Cli.Shared;
using System.Diagnostics;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Cli.Commands.Dev;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

public static class DevCommand
{
    internal static (string? configPath, string? site, string host, int port, bool noWatch, string? outputOverride, bool allowLan) ExtractOptions(CliBoundCommand command)
    {
        return (
            command.GetString("--config"),
            command.GetString("--site"),
            command.GetString("--host") ?? "localhost",
            command.GetInt("--port") ?? 35729,
            command.GetBool("--no-watch"),
            command.GetString("--output"),
            command.GetBool("--allow-lan") || command.GetBool("--public")
        );
    }

    public static async Task<int> RunAsync(CliBoundCommand command)
    {
        var (configPath, site, host, port, noWatch, outputOverride, allowLan) = ExtractOptions(command);
        return await RunCoreAsync(configPath, site, host, port, noWatch, outputOverride, allowLan);
    }

    private static async Task<int> RunCoreAsync(
        string? configPath, string? site, string host, int port, bool noWatch, string? outputOverride, bool allowLan)
    {
        var logger = new ConsoleLogger(LogLevel.Info);
        if (IsLanExposureHost(host))
        {
            if (ShouldRefuseLanExposure(host, allowLan))
            {
                logger.Error("bukit dev refused to bind a non-loopback host. Use --allow-lan to expose the development server to your LAN.");
                return 2;
            }

            logger.Warn($"bukit dev is listening on non-loopback host '{host}'. Only use --allow-lan on trusted networks.");
        }

        var resolved = ConfigPathResolver.Resolve(configPath, site);
        var config = ConfigLoader.Load(resolved.FullConfigPath);
        var rootDir = resolved.RootDir;
        var outputDir = BuildPathUtils.MakeAbsolute(rootDir, outputOverride ?? config.Build.Output);

        var cacheDir = Path.GetFullPath(Path.Combine(rootDir, ".cache"));

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        Console.WriteLine("bukit dev — LiveReload development server");
        Console.WriteLine($"  root: {rootDir}");

        var engine = new SiteEngine(logger);

        Console.WriteLine("\n[build] initial full build...");
        var sw = Stopwatch.StartNew();
        await engine.BuildAsync(
            config, rootDir,
            CreateBuildOverrides(clean: true, outputOverride, cacheDir),
            cts.Token);
        sw.Stop();
        Console.WriteLine($"[build] done in {sw.ElapsedMilliseconds}ms, serving {outputDir}\n");

        using var serverHost = DevServerHost.Start(host, port, logger);
        using var hub = new DevWebSocketHub(logger, new DevWebSocketAccessPolicy(host, serverHost.Port, allowLan));
        var handler = new DevRequestHandler(
            outputDir,
            PreviewCommand.ShouldRemoveManagedAnalytics(config.Site),
            logger);

        var watchedDirs = ResolveWatchDirs(rootDir, config);
        var excludedDirs = ResolveExcludedWatchDirs(rootDir, outputDir, cacheDir);
        DevFileWatcher? watcher = null;
        if (ShouldStartWatcher(noWatch, watchedDirs.Count))
        {
            watcher = new DevFileWatcher(watchedDirs, rootDir, logger,
                async (_, rebuildCt) =>
                {
                    await engine.BuildAsync(config, rootDir,
                        CreateBuildOverrides(clean: false, outputOverride, cacheDir),
                        rebuildCt);
                    await hub.BroadcastReloadAsync();
                },
                excludedDirs);
            watcher.Start(cts.Token);
        }

        Console.WriteLine($"  dev server: {serverHost.Prefix}");
        if (ShouldPrintWatchStatus(noWatch))
        {
            Console.WriteLine($"  live reload: ws://{host}:{serverHost.Port}/__ws__");
            Console.WriteLine($"  watching: {watchedDirs.Count} directorie(s)");
        }
        Console.WriteLine("  Press Ctrl+C to stop.\n");

        var acceptLoopTask = serverHost.RunAcceptLoopAsync(async ctx =>
        {
            if (ctx.Request.Url?.AbsolutePath == "/__ws__")
                await hub.HandleUpgradeAsync(ctx, cts.Token);
            else
                await handler.HandleAsync(ctx, cts.Token);
        }, cts.Token);

        try
        {
            await WaitForShutdownOrAcceptLoopAsync(acceptLoopTask, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        finally
        {
            cts.Cancel();
            if (watcher is not null)
            {
                await watcher.DisposeAsync();
            }

            try
            {
                await acceptLoopTask;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
            }
        }

        Console.WriteLine("\ndev server stopped.");
        return 0;
    }

    internal static async Task WaitForShutdownOrAcceptLoopAsync(
        Task acceptLoopTask,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(acceptLoopTask);
        await acceptLoopTask.WaitAsync(cancellationToken);
    }

    internal static ConfigOverrides CreateBuildOverrides(bool clean, string? outputOverride, string cacheDir)
    {
        return new ConfigOverrides
        {
            ExecutionMode = BuildExecutionMode.Development,
            Clean = clean,
            Output = outputOverride,
            Incremental = true,
            CacheDir = cacheDir
        };
    }

    internal static List<string> ResolveWatchDirs(string rootDir, AppConfig config)
    {
        var dirs = new List<string>();

        var contentDir = Path.GetFullPath(Path.Combine(rootDir, "content"));
        if (Directory.Exists(contentDir)) dirs.Add(contentDir);

        if (!string.IsNullOrWhiteSpace(config.Theme.Name))
        {
            var themeDir = Path.GetFullPath(Path.Combine(rootDir, "themes", config.Theme.Name));
            if (Directory.Exists(themeDir)) dirs.Add(themeDir);
        }

        void AddIfNotUnderTheme(string relPath)
        {
            var full = Path.GetFullPath(Path.Combine(rootDir, relPath));
            if (Directory.Exists(full) && !dirs.Any(d => PathUtils.IsSameOrSubPathOf(full, d)))
                dirs.Add(full);
        }

        AddIfNotUnderTheme(config.Theme.Layouts);
        AddIfNotUnderTheme(config.Theme.Assets);
        AddIfNotUnderTheme(config.Theme.Static);

        return dirs;
    }

    internal static IReadOnlyList<string> ResolveExcludedWatchDirs(string rootDir, string outputDir, string cacheDir)
        => new[]
        {
            outputDir,
            cacheDir,
            Path.Combine(rootDir, ".git"),
            Path.Combine(rootDir, "node_modules"),
            Path.Combine(rootDir, ".bukit"),
            Path.Combine(rootDir, "bin"),
            Path.Combine(rootDir, "obj")
        }
        .Select(Path.GetFullPath)
        .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
        .ToArray();

    internal static bool IsLanExposureHost(string host)
        => !DevWebSocketAccessPolicy.IsLoopbackHost(host);

    internal static bool ShouldRefuseLanExposure(string host, bool allowLan)
        => IsLanExposureHost(host) && !allowLan;

    internal static bool ShouldStartWatcher(bool noWatch, int watchedDirsCount)
        => !noWatch && watchedDirsCount > 0;

    internal static bool ShouldPrintWatchStatus(bool noWatch)
        => !noWatch;
}
