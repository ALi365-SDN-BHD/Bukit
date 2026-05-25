using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Bukit.Config;

namespace Bukit.Engine.Plugins.Protocol;

internal sealed class ProcessPluginInvoker : IProtocolPluginInvoker
{
    public async Task<ProtocolPluginInvocationResult> InvokeAsync(
        ExternalPluginConfig plugin,
        string requestJson,
        string? arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = plugin.Entry,
            Arguments = arguments ?? string.Empty,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        ApplyEnvironment(startInfo, plugin, requestJson);

        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();

        process.Start();

        await process.StandardInput.WriteAsync(requestJson.AsMemory(), cancellationToken);
        await process.StandardInput.FlushAsync();
        process.StandardInput.Close();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(plugin.TimeoutMs);
        var stdoutTask = ReadLimitedAsync(process.StandardOutput, plugin.MaxStdoutBytes, "stdout", timeoutCts.Token);
        var stderrTask = ReadLimitedAsync(process.StandardError, plugin.MaxStderrBytes, "stderr", timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            stopwatch.Stop();
            return new ProtocolPluginInvocationResult(process.ExitCode, stdout, stderr, false, stopwatch.ElapsedMilliseconds);
        }
        catch (PluginOutputLimitExceededException ex)
        {
            KillProcessTree(process);
            stopwatch.Stop();
            return new ProtocolPluginInvocationResult(-1, string.Empty, $"[plugin-output-limit] {ex.Message}", false, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            KillProcessTree(process);
            stopwatch.Stop();
            return new ProtocolPluginInvocationResult(-1, string.Empty, "[plugin-timeout] process invocation timed out.", true, stopwatch.ElapsedMilliseconds);
        }
    }

    private static void ApplyEnvironment(ProcessStartInfo startInfo, ExternalPluginConfig plugin, string requestJson)
    {
        var hostEnvironment = Environment.GetEnvironmentVariables();
        startInfo.Environment.Clear();
        if (plugin.AllowEnvironment is not null)
        {
            foreach (var name in plugin.AllowEnvironment)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var value = hostEnvironment[name];
                if (value is string stringValue)
                {
                    startInfo.Environment[name] = stringValue;
                }
            }
        }

        var (pluginName, hook) = ReadInvocationIdentity(requestJson);
        startInfo.Environment["BUKIT_PLUGIN_NAME"] = pluginName;
        startInfo.Environment["BUKIT_PLUGIN_HOOK"] = hook;
    }

    private static (string PluginName, string Hook) ReadInvocationIdentity(string requestJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(requestJson);
            var root = doc.RootElement;
            var hook = root.TryGetProperty("hook", out var hookProperty) && hookProperty.ValueKind == JsonValueKind.String
                ? hookProperty.GetString() ?? string.Empty
                : string.Empty;
            var pluginName = string.Empty;
            if (root.TryGetProperty("plugin", out var pluginProperty)
                && pluginProperty.ValueKind == JsonValueKind.Object
                && pluginProperty.TryGetProperty("name", out var nameProperty)
                && nameProperty.ValueKind == JsonValueKind.String)
            {
                pluginName = nameProperty.GetString() ?? string.Empty;
            }

            return (pluginName, hook);
        }
        catch (JsonException)
        {
            return (string.Empty, string.Empty);
        }
    }

    private static async Task<string> ReadLimitedAsync(StreamReader reader, int maxBytes, string streamName, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var buffer = new char[4096];
        var bytes = 0;
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
            {
                return builder.ToString();
            }

            bytes += Encoding.UTF8.GetByteCount(buffer.AsSpan(0, read));
            if (bytes > maxBytes)
            {
                throw new PluginOutputLimitExceededException($"{streamName} exceeded {maxBytes} bytes.");
            }

            builder.Append(buffer, 0, read);
        }
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private sealed class PluginOutputLimitExceededException : Exception
    {
        public PluginOutputLimitExceededException(string message) : base(message)
        {
        }
    }
}
