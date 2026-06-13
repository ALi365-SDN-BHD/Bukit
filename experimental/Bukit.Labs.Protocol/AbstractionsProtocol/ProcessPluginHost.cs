using System.Text.Json;

namespace Bukit.Engine.Abstractions.Plugins.Protocol;

public abstract class ProcessPluginHost
{
    protected abstract string PluginName { get; }
    protected abstract string PluginVersion { get; }
    protected abstract IReadOnlyList<string> SupportedHooks { get; }

    protected virtual Task AfterBuildAsync(AfterBuildRequestPayload payload, IReadOnlyDictionary<string, object>? pluginOptions, CancellationToken ct)
        => Task.CompletedTask;

    public async Task RunAsync(CancellationToken ct = default)
    {
        var stdin = await Console.In.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(stdin))
        {
            WriteError("Empty stdin input.");
            return;
        }

        ProtocolPluginInvocationRequest request;
        try
        {
            request = JsonSerializer.Deserialize(stdin, ProtocolPluginJsonContext.Default.ProtocolPluginInvocationRequest)
                      ?? throw new JsonException("Deserialized request is null.");
        }
        catch (Exception ex)
        {
            WriteError($"Failed to parse request: {ex.Message}");
            return;
        }

        if (string.Equals(request.Hook, "handshake", StringComparison.OrdinalIgnoreCase))
        {
            HandleHandshake(request);
            return;
        }

        if (!SupportedHooks.Contains(request.Hook, StringComparer.OrdinalIgnoreCase))
        {
            WriteError($"Unsupported hook: {request.Hook}");
            return;
        }

        await HandleHookAsync(request, ct);
    }

    private void HandleHandshake(ProtocolPluginInvocationRequest request)
    {
        var response = new ProtocolPluginInvocationResponse
        {
            Ok = true,
            Logs = new[]
            {
                new ProtocolPluginLogEntry
                {
                    Level = "info",
                    Message = $"Handshake OK. Plugin: {PluginName} v{PluginVersion}. Supported hooks: {string.Join(", ", SupportedHooks)}."
                }
            }
        };

        WriteResponse(response);
    }

    private async Task HandleHookAsync(ProtocolPluginInvocationRequest request, CancellationToken ct)
    {
        try
        {
            if (string.Equals(request.Hook, "after-build", StringComparison.OrdinalIgnoreCase))
            {
                var payload = request.AfterBuild ?? new AfterBuildRequestPayload { OutputDir = "." };
                var pluginOptions = JsonElementMaterializer.Materialize(request.Config?.PluginOptions);
                payload = MaterializeRoutedPageFields(payload);
                await AfterBuildAsync(payload, pluginOptions, ct);
            }

            var response = new ProtocolPluginInvocationResponse { Ok = true };
            WriteResponse(response);
        }
        catch (Exception ex)
        {
            WriteError($"Hook execution failed: {ex.Message}");
        }
    }

    private static AfterBuildRequestPayload MaterializeRoutedPageFields(AfterBuildRequestPayload payload)
    {
        var pages = payload.RoutedPages;
        if (pages is null || pages.Count == 0)
        {
            return payload;
        }

        var materialized = false;
        var list = new AfterBuildRoutedPage[pages.Count];
        for (var i = 0; i < pages.Count; i++)
        {
            var page = pages[i];
            if (page.Fields is not null)
            {
                materialized = true;
                list[i] = page with { Fields = JsonElementMaterializer.Materialize(page.Fields) };
            }
            else
            {
                list[i] = page;
            }
        }

        return materialized ? payload with { RoutedPages = list } : payload;
    }

    protected void WriteResponse(ProtocolPluginInvocationResponse response)
    {
        var json = JsonSerializer.Serialize(response, ProtocolPluginJsonContext.Default.ProtocolPluginInvocationResponse);
        Console.Out.Write(json);
        Console.Out.Flush();
    }

    protected void WriteError(string message)
    {
        var response = new ProtocolPluginInvocationResponse
        {
            Ok = false,
            Error = new ProtocolPluginError
            {
                Code = "plugin-error",
                Message = message
            }
        };
        WriteResponse(response);
    }
}
