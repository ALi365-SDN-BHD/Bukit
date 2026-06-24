using Bukit.Cli.Shared.Cli.Metadata;

namespace Bukit.Cli;

public static class PluginListCommand
{
    public static CommandDescriptor Create(IReadOnlyList<PluginListRecord> plugins)
    {
        var listSpec = new CliCommandSpec("list", "List enabled and configured plugins");
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

        return new CommandDescriptor(
            new CliCommandSpec(
                Name: "plugin",
                Description: "Manage configured extensions",
                Subcommands: [listSpec]),
            Children: [list]);
    }

    private static string Normalize(string value)
        => value.Replace('\r', ' ').Replace('\n', ' ');
}
