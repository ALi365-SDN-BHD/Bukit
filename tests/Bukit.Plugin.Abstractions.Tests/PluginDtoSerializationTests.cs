using System.Text.Json;
using Bukit.Plugin.Abstractions.Manifest;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;
using Bukit.Plugin.Abstractions.Runtime;
using Bukit.Plugin.Abstractions.Security;
using Xunit;

namespace Bukit.Plugin.Abstractions.Tests;

public sealed class PluginDtoSerializationTests
{
    [Fact]
    public void HandshakeRequest_SerializesProtocolEnvelopeWithCamelCaseNames()
    {
        var request = new PluginHandshakeRequest(
            Type: PluginProtocolConstants.Handshake,
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: "req-1",
            Host: new PluginHostInfo("bukit", "1.0.0", "osx-arm64"));

        var json = JsonSerializer.Serialize(request, PluginJsonSerializerContext.Default.PluginHandshakeRequest);

        Assert.Contains("\"requestId\":\"req-1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"protocol\":\"bukit-plugin-v1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"host\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ManifestResponse_DefaultCollectionsAreEmpty()
    {
        var response = new PluginManifestResponse(
            Type: "manifestResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: "req-2",
            Success: true);

        Assert.Empty(response.Capabilities);
        Assert.Empty(response.Commands);
        Assert.NotNull(response.RequiredPermissions);
        Assert.Empty(response.Messages);
        Assert.Empty(response.Diagnostics);
    }

    [Fact]
    public void InvokeRequest_UsesProtocolShapeForCommandContextAndPermissions()
    {
        var request = new PluginInvokeRequest(
            Type: PluginProtocolConstants.Invoke,
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: "req-3",
            Host: new PluginHostInfo("bukit", "1.0.0", "linux-x64"),
            Command: new PluginInvokeCommand(
                Name: "echo",
                Path: ["echo"],
                Arguments: ["hello"],
                Options: new Dictionary<string, JsonElement>()),
            Context: new PluginInvokeContext(
                RootDir: "/repo",
                WorkingDir: "/repo"),
            Permissions: new PluginPermissionSet());

        var json = JsonSerializer.Serialize(request, PluginJsonSerializerContext.Default.PluginInvokeRequest);

        Assert.Contains("\"command\"", json, StringComparison.Ordinal);
        Assert.Contains("\"workingDir\":\"/repo\"", json, StringComparison.Ordinal);
        Assert.Contains("\"permissions\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void PluginManifest_RoundTripsWithCommandsAndPermissions()
    {
        var manifest = new PluginManifest(
            Id: "echo",
            Name: "Bukit Echo Plugin",
            Version: "1.0.0",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            Kind: "process",
            Distribution: "self-contained",
            Platforms: new Dictionary<string, PluginPlatformEntry>
            {
                ["osx-arm64"] = new("bin/osx-arm64/bukit-plugin-echo", "abc")
            },
            Commands:
            [
                new PluginCommandSpec(
                    Name: "echo",
                    Description: "Echo input",
                    Arguments: [new PluginArgumentSpec("value", "Value to echo", Required: true)],
                    Options: [new PluginOptionSpec("--upper", "flag", "Uppercase output")])
            ],
            RequiredPermissions: new PluginPermissionSet())
        {
            ManifestVersion = 2
        };

        var json = JsonSerializer.Serialize(manifest, PluginJsonSerializerContext.Default.PluginManifest);
        var roundTripped = JsonSerializer.Deserialize(json, PluginJsonSerializerContext.Default.PluginManifest);

        Assert.NotNull(roundTripped);
        Assert.Equal(manifest.Id, roundTripped.Id);
        Assert.Equal(manifest.Protocol, roundTripped.Protocol);
        Assert.Single(roundTripped.Commands);
        Assert.Single(roundTripped.Platforms);
        Assert.Equal(2, roundTripped.ManifestVersion);
    }

    [Fact]
    public void InvokeResponse_RecordEqualityIncludesMessagesAndArtifacts()
    {
        var left = new PluginInvokeResponse(
            Type: "invokeResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: "req-4",
            Success: true,
            ExitCode: 0,
            Messages: [new PluginMessage("info", "done")],
            Artifacts: [new PluginArtifact("file", "reports/echo.json", "Echo report")]);
        var right = left with { };

        Assert.Equal(left, right);
    }

    [Fact]
    public void ManifestRequestAndRequestEnvelope_RoundTripProtocolShape()
    {
        var host = new PluginHostInfo("bukit", "1.0.0", "osx-arm64");

        string manifestJson = JsonSerializer.Serialize(
            new PluginManifestRequest("manifest", PluginProtocolConstants.ProtocolVersion, "req-manifest", host),
            PluginJsonSerializerContext.Default.PluginManifestRequest);
        string envelopeJson = JsonSerializer.Serialize(
            new PluginRequestEnvelope("handshake", PluginProtocolConstants.ProtocolVersion, "req-envelope", host),
            PluginJsonSerializerContext.Default.PluginRequestEnvelope);

        PluginManifestRequest? manifest = JsonSerializer.Deserialize(
            manifestJson,
            PluginJsonSerializerContext.Default.PluginManifestRequest);
        PluginRequestEnvelope? envelope = JsonSerializer.Deserialize(
            envelopeJson,
            PluginJsonSerializerContext.Default.PluginRequestEnvelope);

        Assert.Contains("\"type\":\"manifest\"", manifestJson, StringComparison.Ordinal);
        Assert.Contains("\"requestId\":\"req-manifest\"", manifestJson, StringComparison.Ordinal);
        Assert.Contains("\"platform\":\"osx-arm64\"", manifestJson, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"handshake\"", envelopeJson, StringComparison.Ordinal);
        Assert.Contains("\"requestId\":\"req-envelope\"", envelopeJson, StringComparison.Ordinal);
        Assert.Equal("manifest", manifest?.Type);
        Assert.Equal("req-manifest", manifest?.RequestId);
        Assert.Equal("osx-arm64", manifest?.Host.Platform);
        Assert.Equal("handshake", envelope?.Type);
        Assert.Equal("req-envelope", envelope?.RequestId);
        Assert.Equal("osx-arm64", envelope?.Host.Platform);
    }

    [Fact]
    public void ResponseEnvelope_RoundTripsErrorMessagesAndDiagnostics()
    {
        var envelope = new PluginResponseEnvelope(
            Type: "manifestResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: "req-response",
            Success: false,
            Error: new PluginError(
                Code: "plugin.invalid",
                Message: "Invalid plugin",
                Details: new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["field"] = JsonSerializer.SerializeToElement("commands")
                }),
            Messages: [new PluginMessage("warning", "check manifest")],
            Diagnostics: [new PluginDiagnostic("plugin.invalid", "error", "Invalid plugin", "plugin.yaml")]);

        string json = JsonSerializer.Serialize(envelope, PluginJsonSerializerContext.Default.PluginResponseEnvelope);
        PluginResponseEnvelope? roundTripped = JsonSerializer.Deserialize(
            json,
            PluginJsonSerializerContext.Default.PluginResponseEnvelope);

        Assert.NotNull(roundTripped);
        Assert.False(roundTripped.Success);
        Assert.Equal("plugin.invalid", roundTripped.Error?.Code);
        Assert.Equal("commands", roundTripped.Error?.Details["field"].GetString());
        Assert.Equal("check manifest", Assert.Single(roundTripped.Messages).Message);
        Assert.Equal("plugin.yaml", Assert.Single(roundTripped.Diagnostics).Path);
    }

    [Fact]
    public void HandshakeResponse_RoundTripsIdentityAndCollections()
    {
        var response = new PluginHandshakeResponse(
            Type: "handshakeResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: "req-handshake",
            Success: true,
            Plugin: new PluginIdentity("echo", "Echo", "1.0.0", "osx-arm64", ["echo"]),
            Messages: [new PluginMessage("info", "ready")],
            Diagnostics: [new PluginDiagnostic("plugin.ready", "info", "Ready")]);

        string json = JsonSerializer.Serialize(response, PluginJsonSerializerContext.Default.PluginHandshakeResponse);
        PluginHandshakeResponse? roundTripped = JsonSerializer.Deserialize(
            json,
            PluginJsonSerializerContext.Default.PluginHandshakeResponse);

        Assert.NotNull(roundTripped);
        Assert.True(roundTripped.Success);
        Assert.Equal("echo", roundTripped.Plugin?.Id);
        Assert.Equal("Echo", roundTripped.Plugin?.Name);
        Assert.Equal("1.0.0", roundTripped.Plugin?.Version);
        Assert.Equal("osx-arm64", roundTripped.Plugin?.Platform);
        Assert.Equal("echo", Assert.Single(roundTripped.Plugin!.Capabilities));
        Assert.Equal("ready", Assert.Single(roundTripped.Messages).Message);
        Assert.Equal("plugin.ready", Assert.Single(roundTripped.Diagnostics).Code);
    }

    [Fact]
    public void InvokeResponse_RoundTripsErrorArtifactsAndDiagnostics()
    {
        var response = new PluginInvokeResponse(
            Type: "invokeResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: "req-invoke",
            Success: false,
            ExitCode: 2,
            Error: new PluginError("plugin.permissionDenied", "Permission denied"),
            Messages: [new PluginMessage("warning", "denied")],
            Diagnostics: [new PluginDiagnostic("plugin.permissionDenied", "error", "Denied", "content/index.md")],
            Artifacts: [new PluginArtifact("file", "reports/echo.json", "Echo report")]);

        string json = JsonSerializer.Serialize(response, PluginJsonSerializerContext.Default.PluginInvokeResponse);
        PluginInvokeResponse? roundTripped = JsonSerializer.Deserialize(
            json,
            PluginJsonSerializerContext.Default.PluginInvokeResponse);

        Assert.NotNull(roundTripped);
        Assert.False(roundTripped.Success);
        Assert.Equal(2, roundTripped.ExitCode);
        Assert.Equal("plugin.permissionDenied", roundTripped.Error?.Code);
        Assert.Equal("denied", Assert.Single(roundTripped.Messages).Message);
        Assert.Equal("content/index.md", Assert.Single(roundTripped.Diagnostics).Path);
        Assert.Equal("reports/echo.json", Assert.Single(roundTripped.Artifacts).Path);
    }

    [Fact]
    public void ManifestResponse_RoundTripsCommandsAndPermissions()
    {
        var response = new PluginManifestResponse(
            Type: "manifestResponse",
            Protocol: PluginProtocolConstants.ProtocolVersion,
            RequestId: "req-runtime-manifest",
            Success: true,
            Capabilities: ["echo"],
            Commands:
            [
                new PluginCommandSpec(
                    Name: "echo",
                    Description: "Echo input",
                    Aliases: ["say"],
                    Arguments: [new PluginArgumentSpec("value", "Value to echo", Required: true)],
                    Options: [new PluginOptionSpec("--upper", "flag", "Uppercase output")],
                    Subcommands: [new PluginCommandSpec("nested", "Nested command")])
            ],
            RequiredPermissions: new PluginPermissionSet(Network: true));

        string json = JsonSerializer.Serialize(response, PluginJsonSerializerContext.Default.PluginManifestResponse);
        PluginManifestResponse? roundTripped = JsonSerializer.Deserialize(
            json,
            PluginJsonSerializerContext.Default.PluginManifestResponse);

        Assert.NotNull(roundTripped);
        Assert.True(roundTripped.Success);
        Assert.Equal("echo", Assert.Single(roundTripped.Capabilities));
        PluginCommandSpec command = Assert.Single(roundTripped.Commands);
        Assert.Equal("say", Assert.Single(command.Aliases));
        Assert.True(Assert.Single(command.Arguments).Required);
        Assert.Equal("flag", Assert.Single(command.Options).Type);
        Assert.Equal("nested", Assert.Single(command.Subcommands).Name);
        Assert.True(roundTripped.RequiredPermissions.Network);
    }
}
