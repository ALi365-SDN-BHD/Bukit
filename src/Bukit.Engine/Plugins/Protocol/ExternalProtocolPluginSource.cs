using Bukit.Engine.Abstractions.Plugins.Protocol;
using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Shared;

namespace Bukit.Engine.Plugins.Protocol;

internal sealed class ExternalProtocolPluginSource : IPluginSource
{
    private readonly BuildContext _context;

    public ExternalProtocolPluginSource(BuildContext context)
    {
        _context = context;
    }

    public IEnumerable<IBukitPlugin> GetPlugins()
    {
        if (_context.Config.Site.ExternalPlugins is null)
        {
            yield break;
        }

        foreach (var (name, config) in _context.Config.Site.ExternalPlugins)
        {
            if (!config.Enabled)
            {
                continue;
            }

            yield return new ExternalProtocolPlugin(name, config, _context);
        }
    }

    private sealed class ExternalProtocolPlugin : IBukitPlugin, IAfterBuildAsyncPlugin, IDerivePagesAsyncPlugin, IHookFilterPlugin
    {
        private readonly ExternalPluginConfig _config;
        private readonly BuildContext _context;

        public ExternalProtocolPlugin(string name, ExternalPluginConfig config, BuildContext context)
        {
            Name = name;
            _config = ResolveEntryPath(config, context.RootDir);
            _context = context;
        }

        public string Name { get; }

        public string Version => "protocol-v1";

        public bool SupportsHook(string hook)
            => HasHook(_config, hook);

        public Task<IReadOnlyList<(Bukit.Content.ContentItem Item, Bukit.Routing.RouteInfo Route, DateTimeOffset LastModified)>> DerivePagesAsync(
            BuildContext context,
            CancellationToken cancellationToken = default)
        {
            if (!HasHook(_config, "derive-pages"))
            {
                return Task.FromResult<IReadOnlyList<(Bukit.Content.ContentItem Item, Bukit.Routing.RouteInfo Route, DateTimeOffset LastModified)>>(
                    Array.Empty<(Bukit.Content.ContentItem Item, Bukit.Routing.RouteInfo Route, DateTimeOffset LastModified)>());
            }

            PluginCapabilityEnforcer.Enforce(_config, "derive-pages");

            var runner = new ProtocolDerivePagesRunner(CreateInvoker(_config.Runtime));
            return runner.RunAsync(_context, _config, Name, Version, cancellationToken);
        }

        public Task AfterBuildAsync(BuildContext context, CancellationToken cancellationToken = default)
        {
            if (!HasHook(_config, "after-build"))
            {
                return Task.CompletedTask;
            }

            PluginCapabilityEnforcer.Enforce(_config, "after-build");

            var runner = new ProtocolAfterBuildRunner(CreateInvoker(_config.Runtime));
            return runner.RunAsync(_context, _config, Name, Version, cancellationToken);
        }

        private static bool HasHook(ExternalPluginConfig config, string hook)
        {
            return config.Hooks.Any(x => string.Equals(x?.Trim(), hook, StringComparison.OrdinalIgnoreCase));
        }

        private static IProtocolPluginInvoker CreateInvoker(string runtime)
        {
            if (string.Equals(runtime, "process", StringComparison.OrdinalIgnoreCase))
            {
                return new ProcessPluginInvoker();
            }

            // DESKTOP-REMOVED: wasm runtime disabled (AOT-only).
#if false
            if (string.Equals(runtime, "wasm", StringComparison.OrdinalIgnoreCase))
            {
#if AOT
                throw new NotSupportedException("Protocol plugin runtime 'wasm' is not supported in AOT build. Use 'process' runtime.");
#else
                return new WasmPluginInvoker();
#endif
            }
#endif

            throw new NotSupportedException($"Protocol plugin runtime '{runtime}' is not supported. Use 'process'.");
        }

        private static ExternalPluginConfig ResolveEntryPath(ExternalPluginConfig config, string rootDir)
        {
            if (Path.IsPathRooted(config.Entry))
            {
                return config;
            }

            var combined = Path.Combine(rootDir, config.Entry.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(combined))
            {
                return config with { Entry = combined };
            }

            return config;
        }
    }
}
