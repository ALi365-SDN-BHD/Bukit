using System.Text.Json;
using System.Text.RegularExpressions;
using Bukit.Plugin.Abstractions;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Shared;

namespace Bukit.PluginHost;

public sealed partial class PluginProtocolClient : IPluginProtocolClient
{
    private const string HandshakeResponseType = "handshakeResponse";
    private const string ManifestResponseType = "manifestResponse";
    private const string InvokeResponseType = "invokeResponse";

    private readonly IPluginProcessInvoker _processInvoker;
    private readonly IPluginRequestIdFactory _requestIdFactory;

    public PluginProtocolClient(
        IPluginProcessInvoker processInvoker,
        IPluginRequestIdFactory requestIdFactory)
    {
        _processInvoker = processInvoker ?? throw new ArgumentNullException(nameof(processInvoker));
        _requestIdFactory = requestIdFactory ?? throw new ArgumentNullException(nameof(requestIdFactory));
    }

    public async Task<PluginHandshakeResponse> HandshakeAsync(
        ResolvedPlugin plugin,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        string requestId = _requestIdFactory.Create();
        var request = new PluginHandshakeRequest(
            PluginProtocolConstants.Handshake,
            PluginProtocolConstants.ProtocolVersion,
            requestId,
            plugin.Host);

        PluginProcessResult processResult = await InvokeProcessAsync(
            plugin,
            Serialize(request),
            TimeSpan.FromMilliseconds(plugin.Timeout.HandshakeMs),
            cancellationToken);

        EnsureProcessSucceeded(processResult);
        PluginHandshakeResponse response = Deserialize(
            processResult.StdoutJson,
            PluginJsonSerializerContext.Default.PluginHandshakeResponse);
        ValidateCommonResponse(response.Type, response.Protocol, response.RequestId, response.Success, HandshakeResponseType, requestId);

        if (response.Plugin is null)
        {
            throw ProtocolError(PluginHostErrorCodes.InvalidResponse, "Handshake response must include plugin identity.");
        }

        if (!StringComparer.Ordinal.Equals(response.Plugin.Id, plugin.Id)
            || !StringComparer.Ordinal.Equals(response.Plugin.Version, plugin.Version)
            || !StringComparer.Ordinal.Equals(response.Plugin.Platform, plugin.Platform))
        {
            throw ProtocolError(PluginHostErrorCodes.InvalidResponse, "Handshake plugin identity does not match resolved plugin.");
        }

        return response;
    }

    public async Task<PluginManifestResponse> GetManifestAsync(
        ResolvedPlugin plugin,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        string requestId = _requestIdFactory.Create();
        var request = new PluginManifestRequest(
            PluginProtocolConstants.Manifest,
            PluginProtocolConstants.ProtocolVersion,
            requestId,
            plugin.Host);

        PluginProcessResult processResult = await InvokeProcessAsync(
            plugin,
            Serialize(request),
            TimeSpan.FromMilliseconds(plugin.Timeout.ManifestMs),
            cancellationToken);

        EnsureProcessSucceeded(processResult);
        PluginManifestResponse response = Deserialize(
            processResult.StdoutJson,
            PluginJsonSerializerContext.Default.PluginManifestResponse);
        ValidateCommonResponse(response.Type, response.Protocol, response.RequestId, response.Success, ManifestResponseType, requestId);

        if (response.Commands.Count == 0)
        {
            throw ProtocolError(PluginHostErrorCodes.InvalidResponse, "Manifest response must include at least one command.");
        }

        return response;
    }

    public async Task<PluginInvokeResponse> InvokeAsync(
        ResolvedPlugin plugin,
        PluginInvokeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(request);

        string requestId = _requestIdFactory.Create();
        PluginInvokeRequest normalizedRequest = request with
        {
            Type = PluginProtocolConstants.Invoke,
            Protocol = PluginProtocolConstants.ProtocolVersion,
            RequestId = requestId,
            Host = plugin.Host
        };

        PluginProcessResult processResult = await InvokeProcessAsync(
            plugin,
            Serialize(normalizedRequest),
            TimeSpan.FromMilliseconds(plugin.Timeout.InvokeMs),
            cancellationToken);

        EnsureProcessSucceeded(processResult);
        PluginInvokeResponse response = Deserialize(
            processResult.StdoutJson,
            PluginJsonSerializerContext.Default.PluginInvokeResponse);
        ValidateCommonResponse(response.Type, response.Protocol, response.RequestId, response.Success, InvokeResponseType, requestId);

        if (response.ExitCode != 0)
        {
            throw ProtocolError(PluginHostErrorCodes.ExecutionFailed, $"Plugin invoke returned exit code {response.ExitCode}.");
        }

        ValidateArtifactPaths(response);
        return response;
    }

    private Task<PluginProcessResult> InvokeProcessAsync(
        ResolvedPlugin plugin,
        string standardInputJson,
        TimeSpan timeout,
        CancellationToken cancellationToken)
        => _processInvoker.InvokeAsync(
            new PluginProcessRequest(
                plugin.ExecutablePath,
                plugin.Arguments,
                standardInputJson,
                plugin.WorkingDirectory,
                timeout,
                Math.Min(plugin.Output.StdoutMaxBytes, plugin.Output.ResponseMaxBytes),
                plugin.Output.StderrMaxBytes,
                plugin.EnvironmentVariables),
            cancellationToken);

    private static string Serialize(PluginHandshakeRequest request)
        => JsonSerializer.Serialize(request, PluginJsonSerializerContext.Default.PluginHandshakeRequest);

    private static string Serialize(PluginManifestRequest request)
        => JsonSerializer.Serialize(request, PluginJsonSerializerContext.Default.PluginManifestRequest);

    private static string Serialize(PluginInvokeRequest request)
        => JsonSerializer.Serialize(request, PluginJsonSerializerContext.Default.PluginInvokeRequest);

    private static T Deserialize<T>(string json, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        try
        {
            T? response = JsonSerializer.Deserialize(json, typeInfo);
            return response ?? throw ProtocolError(PluginHostErrorCodes.InvalidResponse, "Plugin response JSON was null.");
        }
        catch (JsonException ex)
        {
            throw ProtocolError(PluginHostErrorCodes.InvalidResponse, "Plugin stdout was not valid protocol JSON.", ex);
        }
        catch (NotSupportedException ex)
        {
            throw ProtocolError(PluginHostErrorCodes.InvalidResponse, "Plugin stdout used unsupported protocol JSON.", ex);
        }
    }

    private static void EnsureProcessSucceeded(PluginProcessResult result)
    {
        if (result.TimedOut)
        {
            throw ProtocolError(PluginHostErrorCodes.Timeout, "Plugin process timed out.");
        }

        if (result.OutputLimitExceeded)
        {
            throw ProtocolError(PluginHostErrorCodes.OutputTooLarge, "Plugin process output exceeded configured limits.");
        }

        if (result.ExitCode != 0)
        {
            throw ProtocolError(PluginHostErrorCodes.ExecutionFailed, $"Plugin process exited with code {result.ExitCode}.");
        }
    }

    private static void ValidateCommonResponse(
        string type,
        string protocol,
        string requestId,
        bool success,
        string expectedType,
        string expectedRequestId)
    {
        if (!StringComparer.Ordinal.Equals(type, expectedType))
        {
            throw ProtocolError(PluginHostErrorCodes.InvalidResponse, $"Plugin response type must be {expectedType}.");
        }

        if (!StringComparer.Ordinal.Equals(protocol, PluginProtocolConstants.ProtocolVersion))
        {
            throw ProtocolError(PluginHostErrorCodes.UnsupportedProtocol, "Plugin response protocol is unsupported.");
        }

        if (!StringComparer.Ordinal.Equals(requestId, expectedRequestId))
        {
            throw ProtocolError(PluginHostErrorCodes.InvalidResponse, "Plugin response requestId did not match request.");
        }

        if (!success)
        {
            throw ProtocolError(PluginHostErrorCodes.ExecutionFailed, "Plugin response reported failure.");
        }
    }

    private static void ValidateArtifactPaths(PluginInvokeResponse response)
    {
        foreach (var artifact in response.Artifacts)
        {
            string path = artifact.Path;
            if (string.IsNullOrWhiteSpace(path)
                || Path.IsPathFullyQualified(path)
                || WindowsAbsolutePathRegex().IsMatch(path)
                || path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Any(part => part is "." or ".."))
            {
                throw ProtocolError(PluginHostErrorCodes.InvalidResponse, "Plugin artifact path must be a project-relative safe path.");
            }
        }
    }

    private static ConfigException ProtocolError(string code, string message, Exception? innerException = null)
    {
        string formatted = $"{code}: {message}";
        return innerException is null
            ? new ConfigException(formatted, DiagnosticCode.PluginExecutionFailed)
            : new ConfigException(formatted, innerException, DiagnosticCode.PluginExecutionFailed);
    }

    [GeneratedRegex(@"^(?:[A-Za-z]:[\\/]|\\\\)")]
    private static partial Regex WindowsAbsolutePathRegex();
}
