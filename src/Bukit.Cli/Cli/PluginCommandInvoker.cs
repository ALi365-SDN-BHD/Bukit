using System.Text.Json;
using System.Globalization;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Plugin.Abstractions.Manifest;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Runtime;
using Bukit.Plugin.Abstractions.Security;
using Bukit.PluginHost;

namespace Bukit.Cli;

public static class PluginCommandInvoker
{
    public static async Task<int> InvokeAsync(
        CliBoundCommand bound,
        ResolvedPlugin plugin,
        PluginCommandSpec command,
        IPluginProtocolClient client)
    {
        var invocation = ResolveInvocation(bound, command);
        var options = ReadOptions(bound, invocation.CommandSpecs);
        var request = new PluginInvokeRequest(
            Type: string.Empty,
            Protocol: string.Empty,
            RequestId: string.Empty,
            Host: plugin.Host,
            Command: new PluginInvokeCommand(
                invocation.LeafCommand.Name,
                Path: invocation.Path,
                Arguments: invocation.Arguments,
                Options: options),
            Context: new PluginInvokeContext(
                RootDir: Directory.GetCurrentDirectory(),
                WorkingDir: Directory.GetCurrentDirectory()),
            Permissions: plugin.GrantedPermissions);

        PluginInvokeResponse response = await client.InvokeAsync(plugin, request, CancellationToken.None);
        foreach (var message in response.Messages)
        {
            if (message.Level.Equals("error", StringComparison.OrdinalIgnoreCase)
                || message.Level.Equals("warn", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine(message.Message);
            }
            else
            {
                Console.WriteLine(message.Message);
            }
        }

        foreach (var diagnostic in response.Diagnostics)
        {
            Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
        }

        return response.ExitCode;
    }

    private static PluginInvocation ResolveInvocation(CliBoundCommand bound, PluginCommandSpec command)
    {
        IReadOnlyList<string> rawArguments = ReadArguments(bound);
        var path = new List<string> { command.Name };
        var commandSpecs = new List<PluginCommandSpec> { command };
        PluginCommandSpec leafCommand = command;
        var consumed = 0;

        while (consumed < rawArguments.Count)
        {
            PluginCommandSpec? subcommand = FindSubcommand(leafCommand, rawArguments[consumed]);
            if (subcommand is null)
            {
                break;
            }

            leafCommand = subcommand;
            path.Add(subcommand.Name);
            commandSpecs.Add(subcommand);
            consumed++;
        }

        return new PluginInvocation(
            leafCommand,
            path,
            rawArguments.Skip(consumed).ToArray(),
            commandSpecs);
    }

    private static PluginCommandSpec? FindSubcommand(PluginCommandSpec command, string name)
    {
        foreach (PluginCommandSpec subcommand in command.Subcommands)
        {
            if (string.Equals(subcommand.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return subcommand;
            }

            if (subcommand.Aliases.Any(alias => string.Equals(alias, name, StringComparison.OrdinalIgnoreCase)))
            {
                return subcommand;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ReadArguments(CliBoundCommand bound)
    {
        var arguments = new List<string>();
        for (int i = 0; ; i++)
        {
            string? value = bound.GetArgument(i);
            if (value is null)
            {
                break;
            }

            arguments.Add(value);
        }

        return arguments;
    }

    private static IReadOnlyDictionary<string, JsonElement> ReadOptions(
        CliBoundCommand bound,
        IReadOnlyList<PluginCommandSpec> commandSpecs)
    {
        var options = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (PluginCommandSpec command in commandSpecs)
        {
            foreach (PluginOptionSpec option in command.Options)
            {
                string? value = bound.GetString(option.Name);
                if (value is null)
                {
                    continue;
                }

                options[option.Name] = CreateOptionElement(option, value);
            }
        }

        return options;
    }

    private static JsonElement CreateOptionElement(PluginOptionSpec option, string value)
        => option.Type.ToLowerInvariant() switch
        {
            "flag" or "bool" or "boolean" => CreateBooleanElement(true),
            "int" or "integer" => CreateNumberElement(int.Parse(value, CultureInfo.InvariantCulture)),
            "number" or "float" or "double" => CreateNumberElement(double.Parse(value, CultureInfo.InvariantCulture)),
            _ => CreateStringElement(value)
        };

    private static JsonElement CreateBooleanElement(bool value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteBooleanValue(value);
        }

        using JsonDocument document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    private static JsonElement CreateNumberElement(int value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteNumberValue(value);
        }

        using JsonDocument document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    private static JsonElement CreateNumberElement(double value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteNumberValue(value);
        }

        using JsonDocument document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    private static JsonElement CreateStringElement(string value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStringValue(value);
        }

        using JsonDocument document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    private sealed record PluginInvocation(
        PluginCommandSpec LeafCommand,
        IReadOnlyList<string> Path,
        IReadOnlyList<string> Arguments,
        IReadOnlyList<PluginCommandSpec> CommandSpecs);
}
