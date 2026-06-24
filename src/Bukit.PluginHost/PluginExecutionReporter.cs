using System.Text.Json;

namespace Bukit.PluginHost;

public sealed class PluginExecutionReporter
{
    public async Task<string> WriteAsync(
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
        writer.WriteString("operation", report.Operation);
        writer.WriteString("requestId", report.RequestId);
        writer.WriteNumber("processExitCode", report.ProcessExitCode);
        writer.WriteBoolean("success", report.Success);
        writer.WriteBoolean("timedOut", report.TimedOut);
        writer.WriteBoolean("outputLimitExceeded", report.OutputLimitExceeded);
        writer.WriteNumber("stdoutBytes", report.StdoutBytes);
        writer.WriteNumber("stderrBytes", report.StderrBytes);
        writer.WriteString("stderr", report.Stderr);
        writer.WritePropertyName("environment");
        writer.WriteStartObject();
        foreach ((string key, string value) in PluginSecretMasker.MaskEnvironment(report.Environment))
        {
            writer.WriteString(key, value);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken);
        return reportPath;
    }

    private static string Sanitize(string value)
        => string.Concat(value.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-'));
}
