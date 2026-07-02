using Bukit.Plugin.Abstractions.Manifest;
using Bukit.Shared;

namespace Bukit.PluginHost;

public sealed class PluginCommandManifestValidator
{
    public void ValidateRuntimeCommands(
        string pluginId,
        IReadOnlyList<PluginCommandSpec> staticCommands,
        IReadOnlyList<PluginCommandSpec> runtimeCommands)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(staticCommands);
        ArgumentNullException.ThrowIfNull(runtimeCommands);

        if (staticCommands.Count == 0)
        {
            return;
        }

        ValidateRuntimeCommands(pluginId, staticCommands, runtimeCommands, parentPath: null);
    }

    private static void ValidateRuntimeCommands(
        string pluginId,
        IReadOnlyList<PluginCommandSpec> staticCommands,
        IReadOnlyList<PluginCommandSpec> runtimeCommands,
        string? parentPath)
    {
        IReadOnlyDictionary<string, PluginCommandSpec> staticByName = ToCommandMap(pluginId, staticCommands, parentPath);
        foreach (PluginCommandSpec runtimeCommand in runtimeCommands)
        {
            string commandPath = BuildPath(parentPath, runtimeCommand.Name);
            if (!staticByName.TryGetValue(runtimeCommand.Name, out PluginCommandSpec? staticCommand))
            {
                throw new ConfigException(
                    $"Plugin {pluginId} runtime manifest command is not declared in plugin.yaml: {commandPath}.",
                    DiagnosticCode.PluginCapabilityMissing);
            }

            ValidateStringSubset(pluginId, commandPath, "alias", staticCommand.Aliases, runtimeCommand.Aliases);
            ValidateArgumentSubset(pluginId, commandPath, staticCommand.Arguments, runtimeCommand.Arguments);
            ValidateOptionSubset(pluginId, commandPath, staticCommand.Options, runtimeCommand.Options);
            ValidateRuntimeCommands(pluginId, staticCommand.Subcommands, runtimeCommand.Subcommands, commandPath);
        }
    }

    private static IReadOnlyDictionary<string, PluginCommandSpec> ToCommandMap(
        string pluginId,
        IReadOnlyList<PluginCommandSpec> commands,
        string? parentPath)
    {
        var result = new Dictionary<string, PluginCommandSpec>(StringComparer.Ordinal);
        foreach (PluginCommandSpec command in commands)
        {
            if (!result.TryAdd(command.Name, command))
            {
                throw new ConfigException(
                    $"Plugin {pluginId} plugin.yaml declares duplicate command: {BuildPath(parentPath, command.Name)}.",
                    DiagnosticCode.ConfigInvalidValue);
            }
        }

        return result;
    }

    private static void ValidateStringSubset(
        string pluginId,
        string commandPath,
        string label,
        IReadOnlyList<string> staticValues,
        IReadOnlyList<string> runtimeValues)
    {
        var declared = new HashSet<string>(staticValues, StringComparer.Ordinal);
        foreach (string runtimeValue in runtimeValues)
        {
            if (!declared.Contains(runtimeValue))
            {
                throw new ConfigException(
                    $"Plugin {pluginId} runtime manifest {label} is not declared in plugin.yaml for command {commandPath}: {runtimeValue}.",
                    DiagnosticCode.PluginCapabilityMissing);
            }
        }
    }

    private static void ValidateArgumentSubset(
        string pluginId,
        string commandPath,
        IReadOnlyList<PluginArgumentSpec> staticArguments,
        IReadOnlyList<PluginArgumentSpec> runtimeArguments)
    {
        var declared = staticArguments.ToDictionary(argument => argument.Name, StringComparer.Ordinal);
        foreach (PluginArgumentSpec runtimeArgument in runtimeArguments)
        {
            if (!declared.TryGetValue(runtimeArgument.Name, out PluginArgumentSpec? staticArgument))
            {
                throw new ConfigException(
                    $"Plugin {pluginId} runtime manifest argument is not declared in plugin.yaml for command {commandPath}: {runtimeArgument.Name}.",
                    DiagnosticCode.PluginCapabilityMissing);
            }

            if (runtimeArgument.Required && !staticArgument.Required)
            {
                throw new ConfigException(
                    $"Plugin {pluginId} runtime manifest argument required=true conflicts with plugin.yaml for command {commandPath}: {runtimeArgument.Name}.",
                    DiagnosticCode.ConfigInvalidValue);
            }
        }
    }

    private static void ValidateOptionSubset(
        string pluginId,
        string commandPath,
        IReadOnlyList<PluginOptionSpec> staticOptions,
        IReadOnlyList<PluginOptionSpec> runtimeOptions)
    {
        var declared = staticOptions.ToDictionary(option => option.Name, StringComparer.Ordinal);
        foreach (PluginOptionSpec runtimeOption in runtimeOptions)
        {
            if (!declared.TryGetValue(runtimeOption.Name, out PluginOptionSpec? staticOption))
            {
                throw new ConfigException(
                    $"Plugin {pluginId} runtime manifest option is not declared in plugin.yaml for command {commandPath}: {runtimeOption.Name}.",
                    DiagnosticCode.PluginCapabilityMissing);
            }

            if (!StringComparer.Ordinal.Equals(runtimeOption.Type, staticOption.Type))
            {
                throw new ConfigException(
                    $"Plugin {pluginId} runtime manifest option type conflicts with plugin.yaml for command {commandPath}: {runtimeOption.Name}.",
                    DiagnosticCode.ConfigInvalidValue);
            }

            if (runtimeOption.Required && !staticOption.Required)
            {
                throw new ConfigException(
                    $"Plugin {pluginId} runtime manifest option required=true conflicts with plugin.yaml for command {commandPath}: {runtimeOption.Name}.",
                    DiagnosticCode.ConfigInvalidValue);
            }
        }
    }

    private static string BuildPath(string? parentPath, string name)
        => string.IsNullOrWhiteSpace(parentPath) ? name : $"{parentPath} {name}";
}
