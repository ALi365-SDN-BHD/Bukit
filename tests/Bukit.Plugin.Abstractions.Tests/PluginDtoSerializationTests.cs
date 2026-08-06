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
}
