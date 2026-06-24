using Bukit.Cli.Shared.Cli.Metadata;
using Bukit.Shared;

namespace Bukit.Cli;

public static class BukitCliComposer
{
    public static IReadOnlyList<CommandDescriptor> Compose(
        IReadOnlyList<CommandDescriptor> coreDescriptors,
        IReadOnlyList<CommandDescriptor> pluginDescriptors)
    {
        var occupied = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (CommandDescriptor core in coreDescriptors)
        {
            foreach (string key in CommandKeys(core.Spec))
            {
                occupied[key] = "core";
            }
        }

        var composed = new List<CommandDescriptor>(coreDescriptors);
        foreach (CommandDescriptor plugin in pluginDescriptors)
        {
            foreach (string key in CommandKeys(plugin.Spec))
            {
                if (occupied.TryGetValue(key, out string? owner))
                {
                    string message = owner == "core"
                        ? $"Plugin command conflicts with core command: {key}"
                        : $"Plugin command conflicts with another plugin command: {key}";
                    throw new ConfigException(message, DiagnosticCode.PluginCapabilityMissing);
                }
            }

            foreach (string key in CommandKeys(plugin.Spec))
            {
                occupied[key] = "plugin";
            }

            composed.Add(plugin);
        }

        return composed;
    }

    private static IEnumerable<string> CommandKeys(CliCommandSpec spec)
    {
        yield return spec.Name;
        foreach (string alias in spec.Aliases ?? [])
        {
            yield return alias;
        }
    }
}
