using System.Text.Json;
using Bukit.Plugin.Abstractions;
using Bukit.Plugin.Abstractions.Manifest;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;
using Bukit.Plugin.Abstractions.Runtime;
using Bukit.Plugin.Abstractions.Security;

string input = await Console.In.ReadToEndAsync();
if (string.IsNullOrWhiteSpace(input))
{
    WriteErrorResponse("unknown", "missingRequest", "Request JSON is required.");
    return 1;
}

try
{
    using JsonDocument document = JsonDocument.Parse(input);
    string type = document.RootElement.TryGetProperty("type", out JsonElement typeElement)
        ? typeElement.GetString() ?? string.Empty
        : string.Empty;

    Console.Error.WriteLine($"bukit-plugin-echo handled {type}");

    switch (type)
    {
        case PluginProtocolConstants.Handshake:
            WriteHandshakeResponse(document.RootElement);
            return 0;

        case PluginProtocolConstants.Manifest:
            WriteManifestResponse(document.RootElement);
            return 0;

        case PluginProtocolConstants.Invoke:
            WriteInvokeResponse(input);
            return 0;

        default:
            WriteErrorResponse(ReadRequestId(document.RootElement), "unknownRequest", $"Unsupported request type: {type}");
            return 2;
    }
}
catch (JsonException ex)
{
    Console.Error.WriteLine(ex.Message);
    WriteErrorResponse("unknown", "invalidJson", "Request JSON is invalid.");
    return 1;
}

static void WriteHandshakeResponse(JsonElement request)
{
    string requestId = ReadRequestId(request);
    string hostPlatform = request.GetProperty("host").GetProperty("platform").GetString() ?? string.Empty;
    var response = new PluginHandshakeResponse(
        Type: "handshakeResponse",
        Protocol: PluginProtocolConstants.ProtocolVersion,
        RequestId: requestId,
        Success: true,
        Plugin: new PluginIdentity(
            Id: "echo",
            Name: "Bukit Echo Plugin",
            Version: "1.0.0",
            Platform: hostPlatform,
            Capabilities: ["cli-command"]));

    WriteJson(JsonSerializer.Serialize(response, PluginJsonSerializerContext.Default.PluginHandshakeResponse));
}

static void WriteManifestResponse(JsonElement request)
{
    string requestId = ReadRequestId(request);
    var response = new PluginManifestResponse(
        Type: "manifestResponse",
        Protocol: PluginProtocolConstants.ProtocolVersion,
        RequestId: requestId,
        Success: true,
        Capabilities: ["cli-command"],
        Commands:
        [
            new PluginCommandSpec(
                Name: "echo",
                Description: "Echo command arguments and context.")
        ],
        RequiredPermissions: new PluginPermissionSet());

    WriteJson(JsonSerializer.Serialize(response, PluginJsonSerializerContext.Default.PluginManifestResponse));
}

static void WriteInvokeResponse(string input)
{
    PluginInvokeRequest request = JsonSerializer.Deserialize(
        input,
        PluginJsonSerializerContext.Default.PluginInvokeRequest)
        ?? throw new JsonException("Invoke request was null.");

    string echoed = BuildEchoPayload(request.Command, request.Context);
    var response = new PluginInvokeResponse(
        Type: "invokeResponse",
        Protocol: PluginProtocolConstants.ProtocolVersion,
        RequestId: request.RequestId,
        Success: true,
        ExitCode: 0,
        Messages:
        [
            new PluginMessage("info", echoed)
        ]);

    WriteJson(JsonSerializer.Serialize(response, PluginJsonSerializerContext.Default.PluginInvokeResponse));
}

static string BuildEchoPayload(PluginInvokeCommand command, PluginInvokeContext context)
{
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream))
    {
        writer.WriteStartObject();
        writer.WritePropertyName("arguments");
        writer.WriteStartArray();
        foreach (string argument in command.Arguments)
        {
            writer.WriteStringValue(argument);
        }

        writer.WriteEndArray();
        writer.WritePropertyName("options");
        writer.WriteStartObject();
        foreach ((string key, JsonElement value) in command.Options)
        {
            writer.WritePropertyName(key);
            value.WriteTo(writer);
        }

        writer.WriteEndObject();
        writer.WritePropertyName("context");
        writer.WriteStartObject();
        writer.WriteString("rootDir", context.RootDir);
        writer.WriteString("workingDir", context.WorkingDir);
        if (context.ConfigPath is not null)
        {
            writer.WriteString("configPath", context.ConfigPath);
        }

        if (context.OutputDir is not null)
        {
            writer.WriteString("outputDir", context.OutputDir);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    return System.Text.Encoding.UTF8.GetString(stream.ToArray());
}

static void WriteErrorResponse(string requestId, string code, string message)
{
    var response = new PluginResponseEnvelope(
        Type: "errorResponse",
        Protocol: PluginProtocolConstants.ProtocolVersion,
        RequestId: requestId,
        Success: false,
        Error: new PluginError(code, message));

    WriteJson(JsonSerializer.Serialize(response, PluginJsonSerializerContext.Default.PluginResponseEnvelope));
}

static string ReadRequestId(JsonElement request)
    => request.TryGetProperty("requestId", out JsonElement requestId) ? requestId.GetString() ?? "unknown" : "unknown";

static void WriteJson(string json)
{
    Console.Out.Write(json);
}
