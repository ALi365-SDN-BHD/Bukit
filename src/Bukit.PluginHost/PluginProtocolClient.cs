using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using Bukit.Plugin.Abstractions;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;
using Bukit.Shared;

namespace Bukit.PluginHost;

public sealed partial class PluginProtocolClient : IPluginProtocolClient
{
    private const string HandshakeResponseType = "handshakeResponse";
    private const string ManifestResponseType = "manifestResponse";
    private const string InvokeResponseType = "invokeResponse";

    private readonly IPluginProcessInvoker _processInvoker;
    private readonly IPluginRequestIdFactory _requestIdFactory;
    private readonly PluginExecutionReporter _executionReporter;

    public PluginProtocolClient(
        IPluginProcessInvoker processInvoker,
        IPluginRequestIdFactory requestIdFactory,
        PluginExecutionReporter? executionReporter = null)
    {
        _processInvoker = processInvoker ?? throw new ArgumentNullException(nameof(processInvoker));
        _requestIdFactory = requestIdFactory ?? throw new ArgumentNullException(nameof(requestIdFactory));
        _executionReporter = executionReporter ?? new PluginExecutionReporter();
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

        PluginInvokeResponse? response = null;
        try
        {
            EnsureInvokeProcessReadable(processResult);
            response = Deserialize(
                processResult.StdoutJson,
                PluginJsonSerializerContext.Default.PluginInvokeResponse);
            ValidateInvokeResponse(response.Type, response.Protocol, response.RequestId, InvokeResponseType, requestId);

            ValidateArtifactPaths(response);
            if (processResult.ExitCode != 0 && processResult.ExitCode != response.ExitCode)
            {
                response = response with
                {
                    Diagnostics = response.Diagnostics
                        .Concat([
                            new PluginDiagnostic(
                                "plugin.processExitMismatch",
                                "warning",
                                $"Plugin process exited with code {processResult.ExitCode}, but invoke response exitCode was {response.ExitCode}.")
                        ])
                        .ToArray()
                };
            }

            return response;
        }
        finally
        {
            await WriteExecutionReportAsync(
                plugin,
                normalizedRequest.Context.RootDir,
                requestId,
                processResult,
                response?.Success ?? false,
                cancellationToken);
        }
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

    private Task<string> WriteExecutionReportAsync(
        ResolvedPlugin plugin,
        string? contextRoot,
        string requestId,
        PluginProcessResult result,
        bool success,
        CancellationToken cancellationToken)
    {
        string? projectRoot = ResolveProjectRoot(plugin, contextRoot);
        return projectRoot is null
            ? Task.FromResult(string.Empty)
            : _executionReporter.WriteAsync(
                projectRoot,
                new PluginExecutionReport(
                    plugin.Id,
                    "invoke",
                    requestId,
                    result.ExitCode,
                    success,
                    result.TimedOut,
                    result.OutputLimitExceeded,
                    Encoding.UTF8.GetByteCount(result.StdoutJson),
                    Encoding.UTF8.GetByteCount(result.Stderr),
                    result.Stderr,
                    ToReportEnvironment(plugin.EnvironmentVariables)),
                cancellationToken);
    }

    private static string? ResolveProjectRoot(ResolvedPlugin plugin, string? contextRoot)
    {
        if (!string.IsNullOrWhiteSpace(plugin.ProjectRoot))
        {
            return plugin.ProjectRoot;
        }

        if (!string.IsNullOrWhiteSpace(contextRoot) && Directory.Exists(contextRoot))
        {
            return contextRoot;
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string> ToReportEnvironment(
        IReadOnlyDictionary<string, string?> environment)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, string? value) in environment)
        {
            result[key] = value ?? string.Empty;
        }

        return result;
    }

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

    private static void EnsureInvokeProcessReadable(PluginProcessResult result)
    {
        if (result.TimedOut)
        {
            throw ProtocolError(PluginHostErrorCodes.Timeout, "Plugin process timed out.");
        }

        if (result.OutputLimitExceeded)
        {
            throw ProtocolError(PluginHostErrorCodes.OutputTooLarge, "Plugin process output exceeded configured limits.");
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

    private static void ValidateInvokeResponse(
        string type,
        string protocol,
        string requestId,
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
