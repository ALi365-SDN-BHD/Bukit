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

        var json = CliErrorRenderer.RenderJson("bukit build", diagnostics, "Usage: bukit build");

        Assert.Equal(
            """
            {
              "schema": "https://bukit.dev/schemas/cli-error.v1.json",
              "version": "1.0",
              "command": "bukit build",
              "exitCode": 2,
              "errors": [
                {
                  "code": "missing-option-value",
                  "message": "Missing value for --output",
                  "showUsage": false
                }
              ],
              "usage": "Usage: bukit build"
            }
            """,
            json);
        Assert.False(json.EndsWith('\n'));
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
    public void RenderJson_NullCommandAndUsage_AreOmittedAndEmptyErrorsRemainAnArray()
    {
        var json = CliErrorRenderer.RenderJson(
            command: null,
            exitCode: 0,
            errors: Array.Empty<CliErrorRenderer.CliErrorDiagnostic>(),
            usage: null);

        Assert.Equal(
            """
            {
              "schema": "https://bukit.dev/schemas/cli-error.v1.json",
              "version": "1.0",
              "exitCode": 0,
              "errors": []
            }
            """,
            json);
    }

    [Fact]
    public void RenderJson_MultipleErrors_PreserveOrderShowUsageAndDefaultEscaping()
    {
        var errors = new[]
        {
            new CliErrorRenderer.CliErrorDiagnostic("first", "<tag>&中\"", ShowUsage: false),
            new CliErrorRenderer.CliErrorDiagnostic("second", "plain", ShowUsage: true),
        };

        var json = CliErrorRenderer.RenderJson("bukit build", 2, errors);
        using var payload = Parse(json);
        var serializedErrors = payload.RootElement.GetProperty("errors");

        Assert.Equal(2, serializedErrors.GetArrayLength());
        Assert.Equal("first", serializedErrors[0].GetProperty("code").GetString());
        Assert.Equal("<tag>&中\"", serializedErrors[0].GetProperty("message").GetString());
        Assert.False(serializedErrors[0].GetProperty("showUsage").GetBoolean());
        Assert.Equal("second", serializedErrors[1].GetProperty("code").GetString());
        Assert.True(serializedErrors[1].GetProperty("showUsage").GetBoolean());
        Assert.Contains(@"\u003Ctag\u003E\u0026", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(@"\u4E2D", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(@"\u0022", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<tag>&中\"", json, StringComparison.Ordinal);
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
