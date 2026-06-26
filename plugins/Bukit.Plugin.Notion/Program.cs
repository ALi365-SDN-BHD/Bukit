using System.Text.Json;
using Bukit.Plugin.Notion;
using Bukit.Plugin.Abstractions;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;

string input = await Console.In.ReadToEndAsync();
if (string.IsNullOrWhiteSpace(input))
{
    Console.Error.WriteLine("bukit-plugin-notion received an empty request");
    Console.Out.Write(SerializeError("unknown", "plugin.notion.missingRequest", "Request JSON is required."));
    return 1;
}

try
{
    Console.Error.WriteLine("bukit-plugin-notion invoked");
    Console.Out.Write(NotionPluginApp.Handle(input));
    return 0;
}
catch (JsonException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Out.Write(SerializeError("unknown", "plugin.notion.invalidJson", "Request JSON is invalid."));
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Out.Write(SerializeError(TryReadRequestId(input), "plugin.notion.executionFailed", "Notion plugin execution failed."));
    return 1;
}

static string SerializeError(string requestId, string code, string message)
{
    var response = new PluginResponseEnvelope(
        Type: "errorResponse",
        Protocol: PluginProtocolConstants.ProtocolVersion,
        RequestId: requestId,
        Success: false,
        Error: new PluginError(code, message));

    return JsonSerializer.Serialize(response, PluginJsonSerializerContext.Default.PluginResponseEnvelope);
}

static string TryReadRequestId(string input)
{
    try
    {
        using JsonDocument document = JsonDocument.Parse(input);
        return document.RootElement.TryGetProperty("requestId", out JsonElement requestIdElement)
            ? requestIdElement.GetString() ?? "unknown"
            : "unknown";
    }
    catch (JsonException)
    {
        return "unknown";
    }
}
