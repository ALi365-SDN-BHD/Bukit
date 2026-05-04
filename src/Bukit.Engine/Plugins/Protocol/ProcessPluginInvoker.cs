using System.Diagnostics;
using System.Text;
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

        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();

        process.Start();

        await process.StandardInput.WriteAsync(requestJson.AsMemory(), cancellationToken);
        await process.StandardInput.FlushAsync();
        process.StandardInput.Close();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(plugin.TimeoutMs);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            stopwatch.Stop();
            return new ProtocolPluginInvocationResult(process.ExitCode, stdout, stderr, false, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
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

            stopwatch.Stop();
            return new ProtocolPluginInvocationResult(-1, string.Empty, "[plugin-timeout] process invocation timed out.", true, stopwatch.ElapsedMilliseconds);
        }
    }
}
