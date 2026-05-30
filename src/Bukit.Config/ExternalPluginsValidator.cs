using Bukit.Shared;
using System.Text.RegularExpressions;

namespace Bukit.Config;

internal static class ExternalPluginsValidator
{
    internal static void ValidateExternalPlugins(IReadOnlyDictionary<string, ExternalPluginConfig> plugins)
    {
        foreach (var (name, plugin) in plugins)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ConfigException("site.externalPlugins keys must be non-empty strings.");
            }

            if (string.IsNullOrWhiteSpace(plugin.Runtime))
            {
                throw new ConfigException($"site.externalPlugins.{name}.runtime is required.");
            }

            var runtime = plugin.Runtime.Trim().ToLowerInvariant();
            if (runtime != "process")
            {
                throw new ConfigException($"site.externalPlugins.{name}.runtime must be process.");
            }

            if (string.IsNullOrWhiteSpace(plugin.Entry))
            {
                throw new ConfigException($"site.externalPlugins.{name}.entry is required.");
            }

            if (Path.IsPathRooted(plugin.Entry) && !plugin.AllowAbsoluteEntry)
            {
                throw new ConfigException($"site.externalPlugins.{name}: plugin entry must be within project directory. Set allowAbsoluteEntry: true to allow absolute paths.");
            }

            if (plugin.Hooks is null || plugin.Hooks.Count == 0)
            {
                throw new ConfigException($"site.externalPlugins.{name}.hooks must contain at least one hook.");
            }

            for (var i = 0; i < plugin.Hooks.Count; i++)
            {
                var hook = plugin.Hooks[i]?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(hook))
                {
                    throw new ConfigException($"site.externalPlugins.{name}.hooks[{i}] must be a non-empty string.");
                }

                if (hook != "after-build" && hook != "derive-pages")
                {
                    throw new ConfigException($"site.externalPlugins.{name}.hooks[{i}] must be after-build or derive-pages.");
                }
            }

            if (plugin.TimeoutMs <= 0)
            {
                throw new ConfigException($"site.externalPlugins.{name}.timeoutMs must be a positive integer.");
            }

            if (plugin.MaxStdoutBytes <= 0)
            {
                throw new ConfigException($"site.externalPlugins.{name}.maxStdoutBytes must be a positive integer.");
            }

            if (plugin.MaxStderrBytes <= 0)
            {
                throw new ConfigException($"site.externalPlugins.{name}.maxStderrBytes must be a positive integer.");
            }

            if (plugin.Capabilities is { Count: > 0 })
            {
                for (var i = 0; i < plugin.Capabilities.Count; i++)
                {
                    var cap = plugin.Capabilities[i]?.Trim().ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(cap))
                    {
                        throw new ConfigException($"site.externalPlugins.{name}.capabilities[{i}] must be a non-empty string.");
                    }

                    if (cap is not ("emit-outputs" or "derive-pages"))
                    {
                        throw new ConfigException($"site.externalPlugins.{name}.capabilities[{i}] must be emit-outputs or derive-pages.");
                    }
                }
            }

            if (runtime == "process")
            {
                ValidateProcessPluginOptions(name, plugin.Options);
            }

#if false
            if (runtime == "wasm")
            {
                if (!string.Equals(plugin.WasmProfile?.Trim(), "wasi-preview1", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ConfigException($"site.externalPlugins.{name}.wasmProfile must be wasi-preview1.");
                }

                if (plugin.MaxMemoryMb <= 0)
                {
                    throw new ConfigException($"site.externalPlugins.{name}.maxMemoryMb must be a positive integer.");
                }

                if (plugin.MaxMemoryMb > 512)
                {
                    throw new ConfigException($"site.externalPlugins.{name}.maxMemoryMb must be <= 512.");
                }

                var wasmFsMode = (plugin.WasmFsMode ?? "output-only").Trim().ToLowerInvariant();
                if (wasmFsMode is not ("none" or "output-only"))
                {
                    throw new ConfigException($"site.externalPlugins.{name}.wasmFsMode must be none|output-only.");
                }

                if (plugin.WasmAllowNetwork)
                {
                    throw new ConfigException($"site.externalPlugins.{name}.wasmAllowNetwork must be false in current sandbox policy.");
                }

                if (plugin.Capabilities is not null)
                {
                    for (var i = 0; i < plugin.Capabilities.Count; i++)
                    {
                        var capability = plugin.Capabilities[i]?.Trim().ToLowerInvariant();
                        if (string.IsNullOrWhiteSpace(capability))
                        {
                            throw new ConfigException($"site.externalPlugins.{name}.capabilities[{i}] must be a non-empty string.");
                        }

                        if (capability != "emit-outputs")
                        {
                            throw new ConfigException($"site.externalPlugins.{name}.capabilities[{i}] must be emit-outputs.");
                        }
                    }
                }
        }
#endif
        }
    }

    internal static void ValidateProcessPluginOptions(string pluginName, IReadOnlyDictionary<string, object>? options)
    {
        if (options is null)
        {
            return;
        }

        if (options.ContainsKey("arguments"))
        {
            throw new ConfigException($"site.externalPlugins.{pluginName}.options.arguments is not allowed. Use options.processArgs.");
        }

        if (!options.TryGetValue("processArgs", out var processArgsObj) || processArgsObj is null)
        {
            return;
        }

        var processArgs = ProviderValidators.AsObjectMap(processArgsObj);
        if (processArgs is null)
        {
            throw new ConfigException($"site.externalPlugins.{pluginName}.options.processArgs must be a mapping.");
        }

        if (processArgs.TryGetValue("positionals", out var positionalsObj) && positionalsObj is not null)
        {
            if (positionalsObj is string || positionalsObj is not IEnumerable<object> positionals)
            {
                throw new ConfigException($"site.externalPlugins.{pluginName}.options.processArgs.positionals must be a sequence.");
            }

            var index = 0;
            foreach (var positional in positionals)
            {
                if (positional is null)
                {
                    throw new ConfigException($"site.externalPlugins.{pluginName}.options.processArgs.positionals[{index}] must be non-null.");
                }

                index++;
            }
        }

        if (processArgs.TryGetValue("named", out var namedObj) && namedObj is not null)
        {
            var named = ProviderValidators.AsObjectMap(namedObj);
            if (named is null)
            {
                throw new ConfigException($"site.externalPlugins.{pluginName}.options.processArgs.named must be a mapping.");
            }

            foreach (var key in named.Keys)
            {
                if (string.IsNullOrWhiteSpace(key) || !Regex.IsMatch(key, "^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$"))
                {
                    throw new ConfigException($"site.externalPlugins.{pluginName}.options.processArgs.named contains illegal key: {key}");
                }
            }
        }
    }
}
