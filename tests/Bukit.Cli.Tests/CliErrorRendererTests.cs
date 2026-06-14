using System.Text.Json;
using Bukit.Cli.Shared.Cli.Parsing;
using Bukit.Cli.Shared.Cli.Rendering;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CliErrorRendererTests
{
    [Fact]
    public void RenderJson_Diagnostics_UsesDefaultSchemaAndExitCode()
    {
        var diagnostics = new[]
        {
            new CliDiagnostic("missing-option-value", "Missing value for --output", ShowUsage: false),
        };

        using var payload = Parse(CliErrorRenderer.RenderJson("bukit build", diagnostics, "Usage: bukit build"));

        Assert.Equal("https://bukit.dev/schemas/cli-error.v1.json", payload.RootElement.GetProperty("schema").GetString());
        Assert.Equal("1.0", payload.RootElement.GetProperty("version").GetString());
        Assert.Equal("bukit build", payload.RootElement.GetProperty("command").GetString());
        Assert.Equal(2, payload.RootElement.GetProperty("exitCode").GetInt32());
        Assert.Equal("Usage: bukit build", payload.RootElement.GetProperty("usage").GetString());

        var error = payload.RootElement.GetProperty("errors")[0];
        Assert.Equal("missing-option-value", error.GetProperty("code").GetString());
        Assert.Equal("Missing value for --output", error.GetProperty("message").GetString());
        Assert.False(error.GetProperty("showUsage").GetBoolean());
    }

    [Fact]
    public void RenderJson_DiagnosticsWithExitCode_UsesProvidedExitCode()
    {
        var diagnostics = new[]
        {
            new CliDiagnostic("invalid-option-value", "Invalid value for --port: abc"),
        };

        using var payload = Parse(CliErrorRenderer.RenderJson("bukit preview", 64, diagnostics));

        Assert.Equal(64, payload.RootElement.GetProperty("exitCode").GetInt32());
        Assert.Equal("invalid-option-value", payload.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void RenderJson_ErrorDiagnostics_UsesProvidedErrors()
    {
        var errors = new[]
        {
            new CliErrorRenderer.CliErrorDiagnostic("custom", "Something broke", ShowUsage: true),
        };

        using var payload = Parse(CliErrorRenderer.RenderJson("bukit publish", 7, errors));

        Assert.Equal(7, payload.RootElement.GetProperty("exitCode").GetInt32());
        Assert.Equal("custom", payload.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.True(payload.RootElement.GetProperty("errors")[0].GetProperty("showUsage").GetBoolean());
    }

    [Fact]
    public void RenderJson_BukitException_FormatsDiagnosticCode()
    {
        using var payload = Parse(
            CliErrorRenderer.RenderJson(
                "bukit config",
                new BukitException("Invalid config", DiagnosticCode.ConfigInvalidValue),
                9,
                "Usage: bukit config"));

        var root = payload.RootElement;
        var error = root.GetProperty("errors")[0];

        Assert.Equal(9, root.GetProperty("exitCode").GetInt32());
        Assert.Equal("BKT-0002", error.GetProperty("code").GetString());
        Assert.Equal("Invalid config", error.GetProperty("message").GetString());
        Assert.False(error.GetProperty("showUsage").GetBoolean());
    }

    [Fact]
    public void RenderJson_GenericException_UsesCliErrorCode()
    {
        using var payload = Parse(
            CliErrorRenderer.RenderJson(
                "bukit deploy",
                new InvalidOperationException("network unavailable"),
                3));

        var error = payload.RootElement.GetProperty("errors")[0];

        Assert.Equal("cli-error", error.GetProperty("code").GetString());
        Assert.Equal("network unavailable", error.GetProperty("message").GetString());
        Assert.False(error.GetProperty("showUsage").GetBoolean());
    }

    private static JsonDocument Parse(string json) => JsonDocument.Parse(json);
}
