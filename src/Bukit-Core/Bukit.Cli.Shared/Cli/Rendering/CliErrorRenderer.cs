using System.Text.Json;
using System.Text.Json.Serialization;
using Bukit.Shared;
using Bukit.Cli.Shared.Cli.Parsing;

namespace Bukit.Cli.Shared.Cli.Rendering;

public static class CliErrorRenderer
{
    public record CliErrorDiagnostic(string Code, string Message, bool ShowUsage = true);
    internal record CliErrorPayload(
        string Schema,
        string Version,
        string? Command,
        int ExitCode,
        IReadOnlyList<CliErrorDiagnostic> Errors,
        string? Usage);

    private const string Schema = "https://bukit.dev/schemas/cli-error.v1.json";
    private const string SchemaVersion = "1.0";

    public static string Render(CliDiagnostic diagnostic)
    {
        return $"Error: {diagnostic.Message}";
    }

    public static string RenderJson(string? command, IReadOnlyList<CliDiagnostic> diagnostics, string? usage = null)
    {
        return RenderJson(
            Schema,
            SchemaVersion,
            command,
            2,
            diagnostics.Select(d => new CliErrorDiagnostic(d.Code, d.Message, d.ShowUsage)).ToList(),
            usage);
    }

    public static string RenderJson(string? command, int exitCode, IReadOnlyList<CliDiagnostic> diagnostics, string? usage = null)
    {
        return RenderJson(
            Schema,
            SchemaVersion,
            command,
            exitCode,
            diagnostics.Select(d => new CliErrorDiagnostic(d.Code, d.Message, d.ShowUsage)).ToList(),
            usage);
    }

    public static string RenderJson(string? command, int exitCode, IReadOnlyList<CliErrorDiagnostic> errors, string? usage = null)
    {
        return RenderJson(
            Schema,
            SchemaVersion,
            command,
            exitCode,
            errors,
            usage);
    }

    public static string RenderJson(string schema, string schemaVersion, string? command, int exitCode, IReadOnlyList<CliErrorDiagnostic> errors, string? usage = null)
    {
        var payload = new CliErrorPayload(schema, schemaVersion, command, exitCode, errors, usage);

        return JsonSerializer.Serialize(payload, CliErrorJsonContext.Default.CliErrorPayload);
    }

    public static string RenderJson(string? command, Exception ex, int exitCode, string? usage = null)
    {
        var code = ex is BukitException { Code: { } diagnosticCode }
            ? DiagnosticCodeFormatter.Format(diagnosticCode)
            : "cli-error";
        return RenderJson(
            Schema,
            SchemaVersion,
            command,
            exitCode,
            new[] { new CliErrorDiagnostic(code, ex.Message, false) },
            usage);
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CliErrorRenderer.CliErrorPayload))]
[JsonSerializable(typeof(CliErrorRenderer.CliErrorDiagnostic))]
internal sealed partial class CliErrorJsonContext : JsonSerializerContext;
