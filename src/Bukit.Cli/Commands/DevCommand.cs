using System.Diagnostics;
using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Commands.Dev;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

public static class DevCommand
{
    internal static (string? configPath, string? site, string host, int port, bool noWatch, string? outputOverride) ExtractOptions(CliBoundCommand command)
    {
        return (
            command.GetString("--config"),
            command.GetString("--site"),
            command.GetString("--host") ?? "localhost",
            command.GetInt("--port") ?? 35729,
            command.GetBool("--no-watch"),
            command.GetString("--output")
        );
    }

    public static async Task<int> RunAsync(CliBoundCommand command)
    {
        var (configPath, site, host, port, noWatch, outputOverride) = ExtractOptions(command);
        return await RunCoreAsync(configPath, site, host, port, noWatch, outputOverride);
    }

    public static async Task<int> RunAsync(string[] args)
    {
        var configPath = (string?)null;
        var site = (string?)null;
        var host = "localhost";
        var port = 35729;
        var noWatch = false;
        var outputOverride = (string?)null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--config" when i + 1 < args.Length: configPath = args[++i]; break;
                case "--site" when i + 1 < args.Length: site = args[++i]; break;
                case "--host" when i + 1 < args.Length: host = args[++i]; break;
                case "--port" when i + 1 < args.Length: port = int.Parse(args[++i]); break;
                case "--output" or "--dir" when i + 1 < args.Length: outputOverride = args[++i]; break;
                case "--no-watch": noWatch = true; break;
            }
        }

        return await RunCoreAsync(configPath, site, host, port, noWatch, outputOverride);
    }

    private static async Task<int> RunCoreAsync(
        string? configPath, string? site, string host, int port, bool noWatch, string? outputOverride)
    {
        var resolved = ConfigPathResolver.Resolve(configPath, site);
        var config = ConfigLoader.Load(resolved.FullConfigPath);
        var rootDir = resolved.RootDir;
        var outputDir = outputOverride is not null
            ? Path.GetFullPath(outputOverride)
            : Path.GetFullPath(Path.Combine(rootDir, config.Build.Output));

        var cacheDir = Path.Combine(rootDir, ".cache");

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        Console.WriteLine($"bukit dev \u2014 HMR development server");
        Console.WriteLine($"  root: {rootDir}");

        var logger = new ConsoleLogger(LogLevel.Info);
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
        var hub = new DevWebSocketHub(logger);
        var handler = new DevRequestHandler(outputDir, serverHost.Port,
            ResolveDisableAnalytics(rootDir), logger);

        var watchedDirs = ResolveWatchDirs(rootDir, config);
        DevFileWatcher? watcher = null;
        if (!noWatch && watchedDirs.Count > 0)
        {
            watcher = new DevFileWatcher(watchedDirs, rootDir, logger,
                async (_, rebuildCt) =>
                {
                    await engine.BuildAsync(config, rootDir,
                        CreateBuildOverrides(clean: false, outputOverride, cacheDir),
                        rebuildCt);
                    await hub.BroadcastReloadAsync();
                });
            watcher.Start(cts.Token);
        }

        Console.WriteLine($"  dev server: {serverHost.Prefix}");
        if (!noWatch)
        {
            Console.WriteLine($"  live reload: ws://{host}:{serverHost.Port}/__ws__");
            Console.WriteLine($"  watching: {watchedDirs.Count} directorie(s)");
        }
        Console.WriteLine("  Press Ctrl+C to stop.\n");

        _ = serverHost.RunAcceptLoopAsync(async ctx =>
        {
            if (ctx.Request.Url?.AbsolutePath == "/__ws__")
                await hub.HandleUpgradeAsync(ctx, cts.Token);
            else
                await handler.HandleAsync(ctx, cts.Token);
        }, cts.Token);

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }

        Console.WriteLine("\ndev server stopped.");
        watcher?.Dispose();
        return 0;
    }

    internal static ConfigOverrides CreateBuildOverrides(bool clean, string? outputOverride, string cacheDir)
    {
        return new ConfigOverrides
        {
            Clean = clean,
            Output = outputOverride,
            Incremental = true,
            CacheDir = cacheDir
        };
    }

    private static List<string> ResolveWatchDirs(string rootDir, AppConfig config)
    {
        var dirs = new List<string>();

        var contentDir = Path.Combine(rootDir, "content");
        if (Directory.Exists(contentDir)) dirs.Add(contentDir);

        if (!string.IsNullOrWhiteSpace(config.Theme.Name))
        {
            var themeDir = Path.Combine(rootDir, "themes", config.Theme.Name);
            if (Directory.Exists(themeDir)) dirs.Add(themeDir);

            if (!string.IsNullOrWhiteSpace(config.Theme.Extends))
            {
                var parentDir = Path.Combine(rootDir, "themes", config.Theme.Extends);
                if (Directory.Exists(parentDir)) dirs.Add(parentDir);
            }
        }

        void AddIfNotUnderTheme(string relPath)
        {
            var full = Path.Combine(rootDir, relPath);
            if (Directory.Exists(full) && !dirs.Any(d => full.StartsWith(d, StringComparison.OrdinalIgnoreCase)))
                dirs.Add(full);
        }

        AddIfNotUnderTheme(config.Theme.Layouts);
        AddIfNotUnderTheme(config.Theme.Assets);
        AddIfNotUnderTheme(config.Theme.Static);

        return dirs;
    }

    private static bool ResolveDisableAnalytics(string dir)
    {
        var current = new DirectoryInfo(Path.GetFullPath(dir));
        while (current is not null)
        {
            var configPath = Path.Combine(current.FullName, "site.yaml");
            if (File.Exists(configPath))
            {
                try
                {
                    var c = ConfigLoader.Load(configPath);
                    return c.Site.Analytics.DisableInPreview &&
                           !string.IsNullOrWhiteSpace(c.Site.Analytics.GoogleAnalyticsId);
                }
                catch
                {
                    return false;
                }
            }

            current = current.Parent;
        }

        return false;
    }
}
