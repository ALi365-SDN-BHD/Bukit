using Bukit.Cli.Shared.Cli.Metadata;
using Bukit.Plugin.Abstractions.Manifest;
using Bukit.PluginHost;

namespace Bukit.Cli;

public static class PluginCommandDescriptorFactory
{
    public static CommandDescriptor Create(
        ResolvedPlugin plugin,
        PluginCommandSpec command,
        IPluginProtocolClient client)
    {
        var spec = ToCliCommandSpec(command);
        return new CommandDescriptor(spec, bound => PluginCommandInvoker.InvokeAsync(bound, plugin, command, client));
    }

    public static CommandDescriptor CreateDisabled(string commandName, string pluginId)
    {
        var spec = new CliCommandSpec(commandName, $"Disabled plugin command from {pluginId}");
        return new CommandDescriptor(
            spec,
            _ =>
            {
                Console.Error.WriteLine($"Command disabled by plugin config: {commandName}");
                return Task.FromResult(2);
            });
    }

    private static CliCommandSpec ToCliCommandSpec(PluginCommandSpec command)
        => new(
            Name: command.Name,
            Description: command.Description,
            Aliases: command.Aliases,
            Arguments: command.Arguments.Select(ToCliArgument).ToArray(),
            Options: command.Options.Select(ToCliOption).ToArray(),
            Subcommands: command.Subcommands.Select(ToCliCommandSpec).ToArray());

    private static CliArgumentSpec ToCliArgument(PluginArgumentSpec argument)
        => new(argument.Name, argument.Description, argument.Required);

    private static CliOptionSpec ToCliOption(PluginOptionSpec option)
        => new(
            Name: option.Name,
            Description: option.Description,
            Type: ToCliOptionType(option.Type),
            Required: option.Required,
            ValueName: option.ValueName,
            AllowedValues: option.AllowedValues,
            ConflictWith: option.ConflictWith);

    private static CliOptionType ToCliOptionType(string type)
        => type.ToLowerInvariant() switch
        {
            "flag" or "bool" or "boolean" => CliOptionType.Flag,
            "int" or "integer" => CliOptionType.Integer,
            "number" or "float" or "double" => CliOptionType.Number,
            _ => CliOptionType.String
        };
}
