using System.Text.Json;
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
        var arguments = ReadArguments(bound);
        var options = ReadOptions(bound, command);
        var request = new PluginInvokeRequest(
            Type: string.Empty,
            Protocol: string.Empty,
            RequestId: string.Empty,
            Host: plugin.Host,
            Command: new PluginInvokeCommand(command.Name, Arguments: arguments, Options: options),
            Context: new PluginInvokeContext(
                RootDir: Directory.GetCurrentDirectory(),
                WorkingDir: Directory.GetCurrentDirectory()),
            Permissions: new PluginPermissionSet());

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

    private static IReadOnlyDictionary<string, JsonElement> ReadOptions(CliBoundCommand bound, PluginCommandSpec command)
    {
        var options = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (PluginOptionSpec option in command.Options)
        {
            string? value = bound.GetString(option.Name);
            if (value is null)
            {
                continue;
            }

            options[option.Name] = CreateStringElement(value);
        }

        return options;
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
}
