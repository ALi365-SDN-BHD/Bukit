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

        using var stdoutCollector = new BoundedOutputCollector(StreamByteCap);
        using var stderrCollector = new BoundedOutputCollector(StreamByteCap);
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

        // Bounded drain: even on normal exit, a grandchild may hold the pipe
        var drainCompleted = await WaitForTerminationGraceAsync(
            Task.WhenAll(stdoutTask, stderrTask),
            TerminationGracePeriod);
        if (!drainCompleted)
        {
            Console.Error.WriteLine(
                $"External tool output drain did not complete within {TerminationGracePeriod.TotalSeconds:0} seconds: {startInfo.FileName}");
        }

        var stdout = stdoutCollector.Seal();
        var stderr = stderrCollector.Seal();
        if (stdout.Exceeded || stderr.Exceeded)
        {
            TryKillProcessTree(process);
            string stream = stdout.Exceeded ? "stdout" : "stderr";
            throw new InvalidOperationException(
                $"External tool '{startInfo.FileName}' produced more than {StreamByteCap} bytes on {stream}.");
        }

        return new ExternalToolProcessResult(
            process.ExitCode,
            stdout.GetText(),
            stderrCollector.GetDiagnosticText());
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
        private readonly object _gate = new();
        private long _totalBytesRead;
        private bool _exceeded;
        private bool _sealed;
        private bool _disposed;

        public BoundedOutputCollector(int maxBytes)
        {
            _maxBytes = maxBytes;
            _buffer = new MemoryStream(capacity: Math.Min(maxBytes, 4096));
        }

        public bool Exceeded
        {
            get
            {
                lock (_gate)
                {
                    return _exceeded;
                }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _sealed = true;
                _disposed = true;
                _buffer.Dispose();
            }
        }

        public async Task ReadAsync(Stream stream, Process process, CancellationToken cancellationToken)
        {
            var readBuffer = new byte[4096];
            while (true)
            {
                int bytesRead = await stream.ReadAsync(readBuffer, cancellationToken);
                if (bytesRead == 0) break;

                var exceeded = false;
                lock (_gate)
                {
                    if (_sealed) return;

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
                        _sealed = true;
                        exceeded = true;
                    }
                }

                if (exceeded)
                {
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

        internal OutputSnapshot Seal()
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _sealed = true;
                return new OutputSnapshot(_buffer.GetBuffer(), checked((int)_buffer.Length), _totalBytesRead, _exceeded);
            }
        }

        public string GetText() => Seal().GetText();

        public string GetDiagnosticText()
        {
            var snapshot = Seal();
            byte[] buffer = snapshot.Buffer;
            if (snapshot.Length <= DiagnosticHeadBytes + DiagnosticTailBytes)
            {
                return Utf8NoBom.GetString(buffer, 0, snapshot.Length);
            }

            var head = DecodeUtf8PrefixWithinByteLimit(buffer, 0, DiagnosticHeadBytes, DiagnosticHeadBytes);
            var tail = DecodeUtf8SuffixWithinByteLimit(buffer, snapshot.Length - DiagnosticTailBytes, DiagnosticTailBytes, DiagnosticTailBytes);
            return $"{head}\n... [truncated {snapshot.TotalBytesRead - DiagnosticHeadBytes - DiagnosticTailBytes} bytes] ...\n{tail}";
        }

        private static string DecodeUtf8PrefixWithinByteLimit(byte[] buffer, int offset, int length, int maxEmittedBytes)
        {
            var text = Utf8NoBom.GetString(buffer, offset, length);
            if (Utf8NoBom.GetByteCount(text) <= maxEmittedBytes)
            {
                return text;
            }

            var low = 0;
            var high = text.Length;
            while (low < high)
            {
                var middle = low + ((high - low + 1) / 2);
                if (Utf8NoBom.GetByteCount(text.AsSpan(0, middle)) <= maxEmittedBytes)
                {
                    low = middle;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return text[..low];
        }

        private static string DecodeUtf8SuffixWithinByteLimit(byte[] buffer, int offset, int length, int maxEmittedBytes)
        {
            var text = Utf8NoBom.GetString(buffer, offset, length);
            if (Utf8NoBom.GetByteCount(text) <= maxEmittedBytes)
            {
                return text;
            }

            var low = 0;
            var high = text.Length;
            while (low < high)
            {
                var middle = low + ((high - low) / 2);
                if (Utf8NoBom.GetByteCount(text.AsSpan(middle)) <= maxEmittedBytes)
                {
                    high = middle;
                }
                else
                {
                    low = middle + 1;
                }
            }

            return text[high..];
        }

        internal sealed record OutputSnapshot(byte[] Buffer, int Length, long TotalBytesRead, bool Exceeded)
        {
            internal string GetText() => Utf8NoBom.GetString(Buffer, 0, Length);
        }
    }
}
