using Bukit.Cli.Shared.Cli.Metadata;
using Bukit.PluginHost;
using Bukit.Shared;

namespace Bukit.Cli;

public static class PluginListCommand
{
    public static CommandDescriptor Create(
        IReadOnlyList<PluginListRecord> plugins,
        IPluginConfigLoader configLoader,
        IPluginManifestLoader manifestLoader,
        string projectRoot)
    {
        var listSpec = new CliCommandSpec("list", "List enabled and configured plugins");
        var validateConfigSpec = new CliCommandSpec(
            "validate-config",
            "Validate .bukit/plugins.yaml",
            Arguments: [new CliArgumentSpec("path", "Project root or .bukit/plugins.yaml path")]);
        var validateManifestSpec = new CliCommandSpec(
            "validate-manifest",
            "Validate plugins/<id>/plugin.yaml",
            Arguments: [new CliArgumentSpec("path", "Plugin root or plugin.yaml path", Required: true)]);

        var list = new CommandDescriptor(
            listSpec,
            _ =>
            {
                Console.WriteLine("Plugins:");
                if (plugins.Count == 0)
                {
                    Console.WriteLine("  (none)");
                    return Task.FromResult(0);
                }

                foreach (PluginListRecord plugin in plugins)
                {
                    string commands = plugin.Commands.Count == 0 ? "-" : string.Join(",", plugin.Commands);
                    string error = string.IsNullOrWhiteSpace(plugin.Error) ? string.Empty : $" error={Normalize(plugin.Error)}";
                    Console.WriteLine($"  {plugin.Id}@{plugin.Version} enabled={plugin.Enabled.ToString().ToLowerInvariant()} status={plugin.Status} platform={plugin.Platform} commands={commands}{error}");
                }

                return Task.FromResult(0);
            });
        var validateConfig = new CommandDescriptor(
            validateConfigSpec,
            async command => await ValidateConfigAsync(command.GetArgument(1), configLoader, projectRoot));
        var validateManifest = new CommandDescriptor(
            validateManifestSpec,
            async command => await ValidateManifestAsync(command.GetArgument(1), manifestLoader, projectRoot));

        return new CommandDescriptor(
            new CliCommandSpec(
                Name: "plugin",
                Description: "Manage configured extensions",
                Subcommands: [listSpec, validateConfigSpec, validateManifestSpec]),
            Children: [list, validateConfig, validateManifest]);
    }

    private static async Task<int> ValidateConfigAsync(
        string? path,
        IPluginConfigLoader configLoader,
        string projectRoot)
    {
        string root = ResolveConfigProjectRoot(path, projectRoot);
        try
        {
            await configLoader.LoadAsync(root, CancellationToken.None);
            Console.WriteLine($"Plugin config OK: {Path.Combine(root, ".bukit", "plugins.yaml")}");
            return 0;
        }
        catch (ConfigException ex)
        {
            Console.Error.WriteLine($"Invalid plugin config: {ex.Message}");
            return 2;
        }
    }

    private static async Task<int> ValidateManifestAsync(
        string? path,
        IPluginManifestLoader manifestLoader,
        string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.Error.WriteLine("Invalid plugin manifest: path is required.");
            return 2;
        }

        string pluginRoot = ResolveManifestPluginRoot(path, projectRoot);
        try
        {
            await manifestLoader.LoadAsync(pluginRoot, CancellationToken.None);
            Console.WriteLine($"Plugin manifest OK: {Path.Combine(pluginRoot, "plugin.yaml")}");
            return 0;
        }
        catch (ConfigException ex)
        {
            Console.Error.WriteLine($"Invalid plugin manifest: {ex.Message}");
            return 2;
        }
    }

    private static string ResolveConfigProjectRoot(string? path, string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Path.GetFullPath(projectRoot);
        }

        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, path));
        if (StringComparer.Ordinal.Equals(Path.GetFileName(fullPath), "plugins.yaml")
            && StringComparer.Ordinal.Equals(Path.GetFileName(Path.GetDirectoryName(fullPath) ?? string.Empty), ".bukit"))
        {
            string? bukitRoot = Path.GetDirectoryName(fullPath);
            string? resolvedProjectRoot = bukitRoot is null ? null : Path.GetDirectoryName(bukitRoot);
            if (!string.IsNullOrWhiteSpace(resolvedProjectRoot))
            {
                return resolvedProjectRoot;
            }
        }

        return fullPath;
    }

    private static string ResolveManifestPluginRoot(string path, string projectRoot)
    {
        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, path));
        if (StringComparer.Ordinal.Equals(Path.GetFileName(fullPath), "plugin.yaml"))
        {
            return Path.GetDirectoryName(fullPath) ?? fullPath;
        }

        return fullPath;
    }

    private static string Normalize(string value)
        => value.Replace('\r', ' ').Replace('\n', ' ');
}
