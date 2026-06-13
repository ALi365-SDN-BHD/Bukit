using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins.Protocol;
using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Shared;
using System.Security.Cryptography;

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

        var policy = _context.Config.Site.ExternalPluginPolicy;
        if (policy == ExternalPluginPolicy.Deny)
        {
            _context.Logger.Warn("External plugins are disabled (externalPluginPolicy: deny).");
            yield break;
        }

        if (policy == ExternalPluginPolicy.Warn)
        {
            _context.Logger.Warn(
                "External plugins execute local processes and should only be enabled for trusted projects. " +
                "Set site.externalPluginPolicy: deny to disable external plugins.");
        }

        foreach (var (name, config) in _context.Config.Site.ExternalPlugins)
        {
            if (!config.Enabled)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(config.Sha256))
            {
                var resolvedEntry = config.Entry;
                if (!Path.IsPathRooted(resolvedEntry))
                {
                    resolvedEntry = Path.Combine(_context.RootDir, resolvedEntry.Replace('/', Path.DirectorySeparatorChar));
                }

                if (!ValidateSha256(resolvedEntry, config.Sha256))
                {
                    throw new ConfigException(
                        $"site.externalPlugins.{name}: sha256 mismatch for entry '{config.Entry}'. " +
                        "The plugin binary has been modified. Update the sha256 hash or remove the plugin.",
                        DiagnosticCode.PluginExecutionFailed);
                }
            }

            yield return new ExternalProtocolPlugin(name, config, _context);
        }
    }

    private static bool ValidateSha256(string filePath, string expectedHash)
    {
        if (!File.Exists(filePath)) return false;
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        var hex = Convert.ToHexStringLower(hash);
        return string.Equals(hex, expectedHash.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ExternalProtocolPlugin : IBukitPlugin, IAfterBuildAsyncPlugin, IDerivePagesAsyncPlugin, IHookFilterPlugin, ITemplateRequirementPlugin
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

        public string Version => "protocol-v2";

        public bool SupportsHook(string hook)
            => HasHook(_config, hook);

        public IReadOnlyList<string> GetTemplateRequirementKinds(BuildContext context)
            => _config.TemplateRequirements ?? Array.Empty<string>();

        public Task<IReadOnlyList<RoutedContentDocument>> DerivePagesAsync(
            BuildContext context,
            CancellationToken cancellationToken = default)
        {
            if (!HasHook(_config, "derive-pages"))
            {
                return Task.FromResult<IReadOnlyList<RoutedContentDocument>>(Array.Empty<RoutedContentDocument>());
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

            throw new NotSupportedException($"Protocol plugin runtime '{runtime}' is not supported. Use 'process'.");
        }

        private static ExternalPluginConfig ResolveEntryPath(ExternalPluginConfig config, string rootDir)
        {
            if (Path.IsPathRooted(config.Entry))
            {
                return config;
            }

            var rootFullPath = Path.GetFullPath(rootDir);
            var combined = Path.GetFullPath(Path.Combine(rootDir, config.Entry.Replace('/', Path.DirectorySeparatorChar)));
            if (!combined.StartsWith(rootFullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(combined, rootFullPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ConfigException(
                    $"External plugin entry resolves outside the project root: '{config.Entry}'.",
                    DiagnosticCode.ConfigPathTraversal);
            }

            if (File.Exists(combined))
            {
                return config with { Entry = combined };
            }

            return config;
        }
    }
}
