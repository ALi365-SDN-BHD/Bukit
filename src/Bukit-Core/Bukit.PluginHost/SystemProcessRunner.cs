using System.Diagnostics;
using System.Text;
using Bukit.PluginHost.ProcessTree;
using Bukit.Shared;

namespace Bukit.PluginHost;

public sealed class SystemProcessRunner : IProcessRunner
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly TimeSpan TerminationGracePeriod = TimeSpan.FromSeconds(2);

    public async Task<ProcessRunResult> RunAsync(
        ProcessRunRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var limitsConfigured = request.MaxCpuTime is not null || request.MaxMemoryBytes is not null;
        IProcessTreeLimiter? treeLimiter = null;
        if (PlatformProcessTreeLimiter.IsSupported)
        {
            treeLimiter = PlatformProcessTreeLimiter.Create();
        }
        else if (limitsConfigured)
        {
            throw new ConfigException(
                $"{PluginHostErrorCodes.ResourceLimitUnsupported}: Resource limits are configured but process-tree limits cannot be proven on this platform.",
                DiagnosticCode.PluginExecutionFailed);
        }

        using var process = new Process
        {
            StartInfo = CreateStartInfo(request),
            EnableRaisingEvents = true
        };

        if (treeLimiter is not null)
        {
            // Runs the child as its own process-group/job leader before start.
            PlatformProcessTreeLimiter.PrepareStartInfo(process.StartInfo);
        }

        try
        {
            return await RunCoreAsync(process, request, treeLimiter, limitsConfigured, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (treeLimiter is not null)
            {
                await treeLimiter.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task<ProcessRunResult> RunCoreAsync(
        Process process,
        ProcessRunRequest request,
        IProcessTreeLimiter? treeLimiter,
        bool limitsConfigured,
        CancellationToken cancellationToken)
    {
        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start plugin process: {request.ExecutablePath}");
        }

        // Windows: job assignment happens after start; see WindowsJobProcessTreeLimiter
        // for the documented start-to-attach window. Unix: containment predates launch.
        treeLimiter?.Attach(process);

        using var timeoutCts = new CancellationTokenSource(request.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var limitState = new OutputLimitState(process, treeLimiter);
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

        // Start process-tree resource monitoring if limits are configured
        Task<string?>? resourceMonitorTask = null;
        if (limitsConfigured)
        {
            resourceMonitorTask = MonitorTreeLimitsAsync(
                process, treeLimiter, request.MaxCpuTime, request.MaxMemoryBytes, linkedCts.Token);
        }

        bool timedOut = false;
        bool terminationCompleted = true;
        try
        {
            await WriteStandardInputAsync(process, request.StandardInput, linkedCts.Token);
            CloseStandardInputBestEffort(process);
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            terminationCompleted = await TerminateProcessAsync(
                process,
                treeLimiter,
                stdoutTask,
                stderrTask,
                resourceMonitorTask);
        }
        catch (OperationCanceledException)
        {
            await TerminateProcessAsync(process, treeLimiter, stdoutTask, stderrTask, resourceMonitorTask);

            throw;
        }
        catch
        {
            await TerminateProcessAsync(process, treeLimiter, stdoutTask, stderrTask, resourceMonitorTask);

            throw;
        }
        finally
        {
            CloseStandardInputBestEffort(process);
        }

        if (timedOut && !terminationCompleted)
        {
            return new ProcessRunResult(
                -1,
                string.Empty,
                $"Process termination did not complete within {TerminationGracePeriod.TotalSeconds:0} seconds.",
                TimedOut: true,
                OutputLimitExceeded: false,
                OutputLimitStream: null);
        }

        // Check whether the tree monitor killed the process for a resource violation
        if (resourceMonitorTask is not null)
        {
            try { resourceLimitExceeded = await resourceMonitorTask; } catch { /* monitor already terminated the tree */ }
        }

        // Bounded drain: stdout/stderr pump must complete within grace period
        // even on normal exit, a grandchild process may hold the pipe
        var drainCompleted = await WaitForTerminationGraceAsync(
            DrainOutputTasksAsync(stdoutTask, stderrTask),
            TerminationGracePeriod);
        string? drainFailure = null;
        if (!drainCompleted)
        {
            drainFailure = $"Plugin process output drain did not complete within {TerminationGracePeriod.TotalSeconds:0} seconds.";
            await TerminateProcessAsync(
                process,
                treeLimiter,
                stdoutTask,
                stderrTask,
                resourceMonitorTask);
        }

        LimitedOutput stdout = stdoutTask.IsCompleted ? await stdoutTask : new LimitedOutput(string.Empty, Exceeded: false);
        LimitedOutput stderr = stderrTask.IsCompleted ? await stderrTask : new LimitedOutput(string.Empty, Exceeded: false);
        var stderrText = drainFailure is null
            ? stderr.Text
            : string.IsNullOrEmpty(stderr.Text)
                ? drainFailure
                : $"{stderr.Text}{Environment.NewLine}{drainFailure}";
        bool outputLimitExceeded = stdout.Exceeded || stderr.Exceeded;
        ProcessOutputStream? outputLimitStream = stdout.Exceeded
            ? ProcessOutputStream.Stdout
            : stderr.Exceeded
                ? ProcessOutputStream.Stderr
                : null;

        int exitCode = timedOut || outputLimitExceeded || resourceLimitExceeded is not null || drainFailure is not null
            ? -1
            : process.ExitCode;
        return new ProcessRunResult(
            exitCode,
            stdout.Text,
            stderrText,
            timedOut,
            outputLimitExceeded,
            outputLimitStream)
        {
            ResourceLimitExceeded = resourceLimitExceeded
        };
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
        catch (IOException)
        {
        }
    }

    private static void CloseStandardInputBestEffort(Process process)
    {
        try
        {
            process.StandardInput.Close();
        }
        catch (IOException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static async Task<bool> TerminateProcessAsync(
        Process process,
        IProcessTreeLimiter? treeLimiter,
        Task<LimitedOutput> stdoutTask,
        Task<LimitedOutput> stderrTask,
        Task<string?>? resourceMonitorTask)
    {
        KillProcess(process, treeLimiter);
        var tasks = new List<Task>
        {
            WaitForExitIgnoringFailureAsync(process),
            ObserveTerminationTaskAsync(stdoutTask),
            ObserveTerminationTaskAsync(stderrTask)
        };
        if (resourceMonitorTask is not null)
        {
            tasks.Add(ObserveTerminationTaskAsync(resourceMonitorTask));
        }

        var completed = await WaitForTerminationGraceAsync(
            Task.WhenAll(tasks),
            TerminationGracePeriod);
        if (!completed)
        {
            Console.Error.WriteLine(
                $"Plugin process termination did not complete within {TerminationGracePeriod.TotalSeconds:0} seconds.");
        }

        return completed;
    }

    private static async Task WaitForExitIgnoringFailureAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync(CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static async Task DrainOutputTasksAsync(
        Task<LimitedOutput> stdoutTask,
        Task<LimitedOutput> stderrTask)
    {
        await Task.WhenAll(stdoutTask, stderrTask);
    }

    private static async Task ObserveTerminationTaskAsync(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
        }
    }

    internal static async Task<bool> WaitForTerminationGraceAsync(
        Task completion,
        TimeSpan gracePeriod)
    {
        try
        {
            await completion.WaitAsync(gracePeriod);
            return true;
        }
        catch (TimeoutException)
        {
            _ = ObserveTerminationTaskAsync(completion);
            return false;
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

    private static void KillProcess(Process process, IProcessTreeLimiter? treeLimiter)
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
        catch (System.ComponentModel.Win32Exception)
        {
        }

        try
        {
            treeLimiter?.Terminate();
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    private sealed class OutputLimitState
    {
        private readonly Process _process;
        private readonly IProcessTreeLimiter? _treeLimiter;
        private int _exceeded;

        public OutputLimitState(Process process, IProcessTreeLimiter? treeLimiter)
        {
            _process = process;
            _treeLimiter = treeLimiter;
        }

        public bool WasExceeded => Volatile.Read(ref _exceeded) == 1;

        public void MarkExceeded(ProcessOutputStream outputStream)
        {
            if (Interlocked.Exchange(ref _exceeded, 1) == 0)
            {
                KillProcess(_process, _treeLimiter);
            }
        }
    }

    private sealed record LimitedOutput(string Text, bool Exceeded);
    // ── Process-tree resource monitoring ─────────────────────────────────

    private static async Task<string?> MonitorTreeLimitsAsync(
        Process process,
        IProcessTreeLimiter? treeLimiter,
        TimeSpan? maxCpuTime,
        long? maxMemoryBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && !process.HasExited)
            {
                await Task.Delay(250, cancellationToken);
                if (process.HasExited) break;

                try
                {
                    ProcessTreeUsage usage;
                    if (treeLimiter is not null)
                    {
                        usage = await treeLimiter.SampleAsync(cancellationToken);
                    }
                    else
                    {
                        process.Refresh();
                        usage = new ProcessTreeUsage(process.TotalProcessorTime, process.PeakWorkingSet64);
                    }

                    if (maxCpuTime is not null && usage.CpuTime > maxCpuTime.Value)
                    {
                        KillProcess(process, treeLimiter);
                        return $"CPU time {usage.CpuTime.TotalSeconds:F1}s exceeded limit {maxCpuTime.Value.TotalSeconds:F1}s";
                    }

                    if (maxMemoryBytes is not null && usage.PeakMemoryBytes > maxMemoryBytes.Value)
                    {
                        KillProcess(process, treeLimiter);
                        return $"Peak memory {usage.PeakMemoryBytes / (1024 * 1024)}MB exceeded limit {maxMemoryBytes.Value / (1024 * 1024)}MB";
                    }
                }
                catch (InvalidOperationException) { break; }
                catch (System.ComponentModel.Win32Exception) { break; }
            }
        }
        catch (OperationCanceledException) { }

        return null;
    }
}
