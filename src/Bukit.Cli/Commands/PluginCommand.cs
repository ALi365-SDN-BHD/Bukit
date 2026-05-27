using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

public static class PluginCommand
{
    public static Task<int> RunAsync(ArgReader reader)
    {
        var sub = reader.GetArg(1);
        if (string.IsNullOrWhiteSpace(sub) || sub is "help" or "--help" or "-h")
        {
            PrintHelp();
            return Task.FromResult(string.IsNullOrWhiteSpace(sub) ? 2 : 0);
        }

        return sub switch
        {
            "list" => ListAsync(reader),
            _ => Task.FromResult(Unknown(sub))
        };
    }

    private static Task<int> ListAsync(ArgReader reader)
    {
        var resolved = ConfigPathResolver.Resolve(reader);
        var config = ConfigLoader.Load(resolved.FullConfigPath);
        var context = new BuildContext
        {
            Config = config,
            RootDir = resolved.RootDir,
            OutputDir = "",
            BaseUrl = config.Site.BaseUrl,
            LayoutsDir = "",
            Routed = new List<(Bukit.Engine.Abstractions.Content.ContentItem Item, Bukit.Engine.Abstractions.Routing.RouteInfo Route)>(),
            BodyStore = Bukit.Engine.Abstractions.Content.NullContentBodyStore.Instance,
            Logger = new ConsoleLogger(LogLevel.Info)
        };

        foreach (var (plugin, source) in PluginRegistry.GetAllPlugins(context))
        {
            var hooks = new List<string>(capacity: 2);
            if (plugin is IDerivePagesPlugin or IDerivePagesAsyncPlugin)
            {
                hooks.Add("derive-pages");
            }
            if (plugin is IAfterBuildPlugin or IAfterBuildAsyncPlugin)
            {
                hooks.Add("after-build");
            }

            var hooksText = hooks.Count == 0 ? "" : $" ({string.Join(", ", hooks)})";
            var enabled = IsPluginEnabled(config, plugin.Name);
            Console.WriteLine($"{plugin.Name}@{plugin.Version} [{source}] enabled={enabled.ToString().ToLowerInvariant()}{hooksText}");
        }

        if (config.Site.ExternalPlugins is not null && config.Site.ExternalPlugins.Count > 0)
        {
            Console.WriteLine("external-config:");
            foreach (var (name, plugin) in config.Site.ExternalPlugins.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                var hooksText = plugin.Hooks.Count == 0 ? "-" : string.Join(",", plugin.Hooks);
                var negotiationText = ResolveNegotiationSummary(plugin);
                Console.WriteLine($"  {name}: enabled={plugin.Enabled.ToString().ToLowerInvariant()}, runtime={plugin.Runtime}, hooks={hooksText}, negotiation={negotiationText}");
            }
        }

        return Task.FromResult(0);
    }

    private static bool IsPluginEnabled(AppConfig config, string pluginName)
    {
        if (config.Site.Plugins is null)
        {
            return true;
        }

        if (config.Site.Plugins.TryGetValue(pluginName, out var toggle))
        {
            return toggle.Enabled;
        }

        return true;
    }

    private static string ResolveNegotiationSummary(ExternalPluginConfig plugin)
    {
        var hasAfterBuildHook = plugin.Hooks.Any(x => string.Equals(x?.Trim(), "after-build", StringComparison.OrdinalIgnoreCase));
        if (!hasAfterBuildHook)
        {
            return "n/a";
        }

        if (!string.Equals(plugin.Runtime, "process", StringComparison.OrdinalIgnoreCase))
        {
            return "n/a";
        }

        return "handshake-v2-fallback-v1";
    }

    private static int Unknown(string sub)
    {
        Console.Error.WriteLine($"Unknown plugin subcommand: {sub}");
        PrintHelp();
        return 2;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("plugin — 插件相关命令");
        Console.WriteLine();
        Console.WriteLine("Usage: bukit plugin <subcommand> [options]");
        Console.WriteLine();
        Console.WriteLine("Subcommands:");
        Console.WriteLine("  list        列出已发现和已配置的插件 (支持 --config/--site)");
    }
}
