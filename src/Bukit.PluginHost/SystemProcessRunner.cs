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

        LimitedOutput stdout = await stdoutTask;
        LimitedOutput stderr = await stderrTask;
        bool outputLimitExceeded = stdout.Exceeded || stderr.Exceeded;
        ProcessOutputStream? outputLimitStream = stdout.Exceeded
            ? ProcessOutputStream.Stdout
            : stderr.Exceeded
                ? ProcessOutputStream.Stderr
                : null;

        int exitCode = timedOut || outputLimitExceeded ? -1 : process.ExitCode;
        return new ProcessRunResult(
            exitCode,
            stdout.Text,
            stderr.Text,
            timedOut,
            outputLimitExceeded,
            outputLimitStream);
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

        public void MarkExceeded(ProcessOutputStream outputStream)
        {
            if (Interlocked.Exchange(ref _exceeded, 1) == 0)
            {
                KillProcess(_process);
            }
        }
    }

    private sealed record LimitedOutput(string Text, bool Exceeded);
}
