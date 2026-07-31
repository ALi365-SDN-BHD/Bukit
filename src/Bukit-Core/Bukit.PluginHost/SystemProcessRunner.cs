using System.Diagnostics;
using System.Text;

namespace Bukit.PluginHost;

public sealed class SystemProcessRunner : IProcessRunner
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public async Task<ProcessRunResult> RunAsync(
        ProcessRunRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        using var process = new Process
        {
            StartInfo = CreateStartInfo(request),
            EnableRaisingEvents = true
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start plugin process: {request.ExecutablePath}");
        }

        using var timeoutCts = new CancellationTokenSource(request.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var limitState = new OutputLimitState(process);
        string? resourceLimitExceeded = null;

        Task<LimitedOutput> stdoutTask = ReadLimitedAsync(
            process.StandardOutput.BaseStream,
            request.StdoutMaxBytes,
            ProcessOutputStream.Stdout,
            limitState,
            cancellationToken);
        Task<LimitedOutput> stderrTask = ReadLimitedAsync(
            process.StandardError.BaseStream,
            request.StderrMaxBytes,
            ProcessOutputStream.Stderr,
            limitState,
            cancellationToken);

        // Start resource monitoring if limits are configured
        Task? resourceMonitorTask = null;
        if (request.MaxCpuTime is not null || request.MaxMemoryBytes is not null)
        {
            resourceMonitorTask = MonitorResourceLimitsAsync(
                process, request.MaxCpuTime, request.MaxMemoryBytes, linkedCts.Token);
        }

        await WriteStandardInputAsync(process, request.StandardInput, cancellationToken);

        bool timedOut = false;
        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            KillProcess(process);
            await process.WaitForExitAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            KillProcess(process);
            throw;
        }

        // Check if resource monitor killed the process
        if (resourceMonitorTask is not null)
        {
            try { await resourceMonitorTask; } catch { /* swallow — already killed process */ }
            if (process.HasExited && !timedOut && !limitState.WasExceeded)
            {
                resourceLimitExceeded = DetectResourceLimitViolation(process, request);
            }
        }

        LimitedOutput stdout = await stdoutTask;
        LimitedOutput stderr = await stderrTask;
        bool outputLimitExceeded = stdout.Exceeded || stderr.Exceeded;
        ProcessOutputStream? outputLimitStream = stdout.Exceeded
            ? ProcessOutputStream.Stdout
            : stderr.Exceeded
                ? ProcessOutputStream.Stderr
                : null;

        int exitCode = timedOut || outputLimitExceeded || resourceLimitExceeded is not null ? -1 : process.ExitCode;
        return new ProcessRunResult(
            exitCode,
            stdout.Text,
            stderr.Text,
            timedOut,
            outputLimitExceeded,
            outputLimitStream,
            resourceLimitExceeded);
    }

    private static ProcessStartInfo CreateStartInfo(ProcessRunRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            WorkingDirectory = request.WorkingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8
        };

        foreach (string argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment.Clear();
        foreach ((string key, string? value) in request.EnvironmentVariables)
        {
            if (value is null)
            {
                startInfo.Environment.Remove(key);
            }
            else
            {
                startInfo.Environment[key] = value;
            }
        }

        return startInfo;
    }

    private static void ValidateRequest(ProcessRunRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ExecutablePath))
        {
            throw new ArgumentException("Executable path is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            throw new ArgumentException("Working directory is required.", nameof(request));
        }

        if (request.Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("Timeout must be positive.", nameof(request));
        }

        if (request.StdoutMaxBytes <= 0 || request.StderrMaxBytes <= 0)
        {
            throw new ArgumentException("Output limits must be positive.", nameof(request));
        }
    }

    private static async Task WriteStandardInputAsync(
        Process process,
        string standardInput,
        CancellationToken cancellationToken)
    {
        try
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken);
            await process.StandardInput.FlushAsync(cancellationToken);
        }
        catch (IOException) when (process.HasExited)
        {
        }
        finally
        {
            process.StandardInput.Close();
        }
    }

    private static async Task<LimitedOutput> ReadLimitedAsync(
        Stream stream,
        int maxBytes,
        ProcessOutputStream outputStream,
        OutputLimitState limitState,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        using var output = new MemoryStream(capacity: Math.Min(maxBytes, 4096));

        while (true)
        {
            int bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                return new LimitedOutput(Utf8.GetString(output.ToArray()), Exceeded: false);
            }

            int remaining = maxBytes - checked((int)output.Length);
            if (bytesRead > remaining)
            {
                if (remaining > 0)
                {
                    output.Write(buffer, 0, remaining);
                }

                limitState.MarkExceeded(outputStream);
                return new LimitedOutput(Utf8.GetString(output.ToArray()), Exceeded: true);
            }

            output.Write(buffer, 0, bytesRead);
        }
    }

    private static void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private sealed class OutputLimitState
    {
        private readonly Process _process;
        private int _exceeded;

        public OutputLimitState(Process process)
        {
            _process = process;
        }

        public bool WasExceeded => Volatile.Read(ref _exceeded) == 1;

        public void MarkExceeded(ProcessOutputStream outputStream)
        {
            if (Interlocked.Exchange(ref _exceeded, 1) == 0)
            {
                KillProcess(_process);
            }
        }
    }

    private sealed record LimitedOutput(string Text, bool Exceeded);

    // ── Resource monitoring ──────────────────────────────────────────────

    private static async Task MonitorResourceLimitsAsync(
        Process process,
        TimeSpan? maxCpuTime,
        long? maxMemoryBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && !process.HasExited)
            {
                await Task.Delay(500, cancellationToken);
                if (process.HasExited) break;

                try
                {
                    process.Refresh();

                    if (maxCpuTime is not null && process.TotalProcessorTime > maxCpuTime.Value)
                    {
                        KillProcess(process);
                        return;
                    }

                    if (maxMemoryBytes is not null && process.PeakWorkingSet64 > maxMemoryBytes.Value)
                    {
                        KillProcess(process);
                        return;
                    }
                }
                catch (InvalidOperationException) { break; }
                catch (System.ComponentModel.Win32Exception) { break; }
            }
        }
        catch (OperationCanceledException) { }
    }

    private static string? DetectResourceLimitViolation(Process process, ProcessRunRequest request)
    {
        try
        {
            if (request.MaxCpuTime is not null)
            {
                var cpuTime = process.TotalProcessorTime;
                if (cpuTime > request.MaxCpuTime.Value)
                {
                    return $"CPU time {cpuTime.TotalSeconds:F1}s exceeded limit {request.MaxCpuTime.Value.TotalSeconds:F1}s";
                }
            }

            if (request.MaxMemoryBytes is not null)
            {
                var peakMem = process.PeakWorkingSet64;
                if (peakMem > request.MaxMemoryBytes.Value)
                {
                    return $"Peak memory {peakMem / (1024 * 1024)}MB exceeded limit {request.MaxMemoryBytes.Value / (1024 * 1024)}MB";
                }
            }
        }
        catch (InvalidOperationException) { }

        return null;
    }
}
