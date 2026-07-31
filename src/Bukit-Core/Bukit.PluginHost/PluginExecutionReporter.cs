using System.Text.Json;
using Bukit.Plugin.Abstractions.Results;
using Bukit.Plugin.Abstractions.Security;

namespace Bukit.PluginHost;

internal sealed class PluginExecutionReporter
{
    internal async Task<string> WriteAsync(
        string projectRoot,
        PluginExecutionReport report,
        CancellationToken cancellationToken)
    {
        string reportDirectory = Path.Combine(projectRoot, ".bukit", "reports", "plugin-executions");
        Directory.CreateDirectory(reportDirectory);
        string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        string reportPath = Path.Combine(reportDirectory, $"{Sanitize(report.PluginId)}-{Sanitize(report.Operation)}-{timestamp}.json");

        await using var stream = File.Create(reportPath);
        await using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteString("pluginId", report.PluginId);
        WriteNullableString(writer, "pluginVersion", report.PluginVersion);
        writer.WriteString("operation", report.Operation);
        WriteNullableString(writer, "protocol", report.Protocol);
        WriteNullableString(writer, "platform", report.Platform);
        WriteNullableString(writer, "command", report.Command);
        writer.WritePropertyName("commandPath");
        WriteStringArray(writer, report.CommandPath);
        WriteNullableString(writer, "entry", report.Entry);
        if (report.StartedAt is DateTimeOffset startedAt)
        {
            writer.WriteString("startedAt", startedAt);
        }
        else
        {
            writer.WriteNull("startedAt");
        }

        if (report.DurationMs is long durationMs)
        {
            writer.WriteNumber("durationMs", durationMs);
        }
        else
        {
            writer.WriteNull("durationMs");
        }

        writer.WriteString("requestId", report.RequestId);
        writer.WriteNumber("processExitCode", report.ProcessExitCode);
        if (report.ResponseExitCode is int responseExitCode)
        {
            writer.WriteNumber("responseExitCode", responseExitCode);
        }
        else
        {
            writer.WriteNull("responseExitCode");
        }

        if (report.Sha256Verified is bool sha256Verified)
        {
            writer.WriteBoolean("sha256Verified", sha256Verified);
        }
        else
        {
            writer.WriteNull("sha256Verified");
        }

        writer.WriteBoolean("success", report.Success);
        writer.WriteBoolean("timedOut", report.TimedOut);
        writer.WriteBoolean("outputLimitExceeded", report.OutputLimitExceeded);
        if (report.ResourceLimitExceeded is not null)
        {
            writer.WriteString("resourceLimitExceeded", report.ResourceLimitExceeded);
        }

        if (report.NetworkPermissionGranted is bool networkGranted)
        {
            writer.WriteBoolean("networkPermissionGranted", networkGranted);
        }

        writer.WriteNumber("stdoutBytes", report.StdoutBytes);
        writer.WriteNumber("stderrBytes", report.StderrBytes);
        writer.WriteString("stderr", PluginSecretMasker.MaskText(report.Stderr, report.Environment));
        writer.WritePropertyName("environment");
        writer.WriteStartObject();
        foreach ((string key, string value) in PluginSecretMasker.MaskEnvironment(report.Environment))
        {
            writer.WriteString(key, value);
        }

        writer.WriteEndObject();
        writer.WritePropertyName("permissions");
        WritePermissions(writer, report.Permissions);
        writer.WritePropertyName("diagnostics");
        WriteDiagnostics(writer, report.Diagnostics, report.Environment);
        writer.WritePropertyName("artifacts");
        WriteArtifacts(writer, report.Artifacts, report.Environment);
        writer.WritePropertyName("responseSummary");
        WriteResponseSummary(writer, report.ResponseSummary);
        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken);
        return reportPath;
    }

    private static void WriteNullableString(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (value is null)
        {
            writer.WriteNull(propertyName);
            return;
        }

        writer.WriteString(propertyName, value);
    }

    private static void WriteNullableMaskedString(
        Utf8JsonWriter writer,
        string propertyName,
        string? value,
        IReadOnlyDictionary<string, string> environment)
    {
        if (value is null)
        {
            writer.WriteNull(propertyName);
            return;
        }

        writer.WriteString(propertyName, PluginSecretMasker.MaskText(value, environment));
    }

    private static void WritePermissions(Utf8JsonWriter writer, PluginPermissionSet? permissions)
    {
        if (permissions is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName("fileSystem");
        writer.WriteStartObject();
        writer.WritePropertyName("read");
        WriteStringArray(writer, permissions.FileSystem.Read);
        writer.WritePropertyName("write");
        WriteStringArray(writer, permissions.FileSystem.Write);
        writer.WriteEndObject();
        writer.WriteBoolean("network", permissions.Network);
        writer.WritePropertyName("environment");
        writer.WriteStartObject();
        writer.WritePropertyName("read");
        WriteStringArray(writer, permissions.Environment.Read);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteDiagnostics(
        Utf8JsonWriter writer,
        IReadOnlyList<PluginDiagnostic> diagnostics,
        IReadOnlyDictionary<string, string> environment)
    {
        writer.WriteStartArray();
        foreach (PluginDiagnostic diagnostic in diagnostics)
        {
            writer.WriteStartObject();
            writer.WriteString("code", diagnostic.Code);
            writer.WriteString("severity", diagnostic.Severity);
            writer.WriteString("message", PluginSecretMasker.MaskText(diagnostic.Message, environment));
            WriteNullableMaskedString(writer, "path", diagnostic.Path, environment);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteArtifacts(
        Utf8JsonWriter writer,
        IReadOnlyList<PluginArtifact> artifacts,
        IReadOnlyDictionary<string, string> environment)
    {
        writer.WriteStartArray();
        foreach (PluginArtifact artifact in artifacts)
        {
            writer.WriteStartObject();
            writer.WriteString("type", artifact.Type);
            writer.WriteString("path", artifact.Path);
            WriteNullableMaskedString(writer, "description", artifact.Description, environment);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteResponseSummary(Utf8JsonWriter writer, PluginExecutionResponseSummary? summary)
    {
        if (summary is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteBoolean("success", summary.Success);
        writer.WriteNumber("exitCode", summary.ExitCode);
        writer.WritePropertyName("diagnosticCodes");
        WriteStringArray(writer, summary.DiagnosticCodes);
        writer.WriteNumber("artifactCount", summary.ArtifactCount);
        writer.WriteEndObject();
    }

    private static void WriteStringArray(Utf8JsonWriter writer, IReadOnlyList<string> values)
    {
        writer.WriteStartArray();
        foreach (string value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }

    private static string Sanitize(string value)
        => string.Concat(value.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-'));
}
