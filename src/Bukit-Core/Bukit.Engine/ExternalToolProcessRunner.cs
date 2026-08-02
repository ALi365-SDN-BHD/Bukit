using System.Diagnostics;
using System.Text;

namespace Bukit.Engine;

internal sealed record ExternalToolProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

internal static class ExternalToolProcessRunner
{
    private const int StreamByteCap = 4 * 1024 * 1024;
    private const int DiagnosticHeadBytes = 32 * 1024;
    private const int DiagnosticTailBytes = 32 * 1024;
    private static readonly TimeSpan TerminationGracePeriod = TimeSpan.FromSeconds(2);
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    internal static async Task<ExternalToolProcessResult> RunAsync(
        ProcessStartInfo startInfo,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Unable to start external tool '{startInfo.FileName}'.");
        }

        var stdoutCollector = new BoundedOutputCollector(StreamByteCap);
        var stderrCollector = new BoundedOutputCollector(StreamByteCap);
        Task stdoutTask = stdoutCollector.ReadAsync(process.StandardOutput.BaseStream, process, CancellationToken.None);
        Task stderrTask = stderrCollector.ReadAsync(process.StandardError.BaseStream, process, CancellationToken.None);

        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);

        try
        {
            await process.WaitForExitAsync(linkedSource.Token);
        }
        catch (OperationCanceledException) when (linkedSource.IsCancellationRequested)
        {
            TryKillProcessTree(process);
            var terminationCompleted = await WaitForTerminationGraceAsync(
                CompleteTerminationAsync(process, stdoutTask, stderrTask),
                TerminationGracePeriod);
            if (!terminationCompleted)
            {
                Console.Error.WriteLine(
                    $"External tool termination did not complete within {TerminationGracePeriod.TotalSeconds:0} seconds: {startInfo.FileName}");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            throw new TimeoutException(
                $"External tool '{startInfo.FileName}' timed out after {timeout.TotalMilliseconds:0}ms." +
                (terminationCompleted
                    ? string.Empty
                    : $" Process termination did not complete within {TerminationGracePeriod.TotalSeconds:0} seconds."));
        }

        await Task.WhenAll(stdoutTask, stderrTask);

        if (stdoutCollector.Exceeded || stderrCollector.Exceeded)
        {
            TryKillProcessTree(process);
            string stream = stdoutCollector.Exceeded ? "stdout" : "stderr";
            throw new InvalidOperationException(
                $"External tool '{startInfo.FileName}' produced more than {StreamByteCap} bytes on {stream}.");
        }

        return new ExternalToolProcessResult(
            process.ExitCode,
            stdoutCollector.GetText(),
            stderrCollector.GetText());
    }

    private static void TryKillProcessTree(Process process)
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
    }

    private static async Task CompleteTerminationAsync(
        Process process,
        Task stdoutTask,
        Task stderrTask)
    {
        try
        {
            await process.WaitForExitAsync(CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
        }

        await ObserveAsync(stdoutTask, stderrTask);
    }

    private static async Task ObserveAsync(params Task[] tasks)
    {
        try
        {
            await Task.WhenAll(tasks);
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
            _ = ObserveEventuallyAsync(completion);
            return false;
        }
    }

    private static async Task ObserveEventuallyAsync(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
        }
    }

    internal sealed class BoundedOutputCollector : IDisposable
    {
        private readonly int _maxBytes;
        private readonly MemoryStream _buffer;
        private long _totalBytesRead;
        private bool _exceeded;

        public BoundedOutputCollector(int maxBytes)
        {
            _maxBytes = maxBytes;
            _buffer = new MemoryStream(capacity: Math.Min(maxBytes, 4096));
        }

        public bool Exceeded => _exceeded;

        public void Dispose() => _buffer.Dispose();

        public async Task ReadAsync(Stream stream, Process process, CancellationToken cancellationToken)
        {
            var readBuffer = new byte[4096];
            while (true)
            {
                int bytesRead = await stream.ReadAsync(readBuffer, cancellationToken);
                if (bytesRead == 0) break;

                _totalBytesRead += bytesRead;
                int space = _maxBytes - (int)_buffer.Length;
                if (space > 0)
                {
                    int toWrite = Math.Min(bytesRead, space);
                    _buffer.Write(readBuffer, 0, toWrite);
                }

                if (_totalBytesRead > _maxBytes)
                {
                    _exceeded = true;
                    TryKillProcess(process);
                    return;
                }
            }
        }

        private static void TryKillProcess(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }

        public string GetText()
        {
            return Utf8NoBom.GetString(_buffer.ToArray());
        }

        public string GetDiagnosticText()
        {
            byte[] data = _buffer.ToArray();
            if (data.Length <= DiagnosticHeadBytes + DiagnosticTailBytes)
            {
                return Utf8NoBom.GetString(data);
            }

            var head = Utf8NoBom.GetString(data, 0, DiagnosticHeadBytes);
            var tail = Utf8NoBom.GetString(data, data.Length - DiagnosticTailBytes, DiagnosticTailBytes);
            return $"{head}\n... [truncated {_totalBytesRead - DiagnosticHeadBytes - DiagnosticTailBytes} bytes] ...\n{tail}";
        }
    }
}
