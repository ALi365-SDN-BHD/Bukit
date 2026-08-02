using System.Diagnostics;

namespace Bukit.Engine;

internal sealed record ExternalToolProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

internal static class ExternalToolProcessRunner
{
    private static readonly TimeSpan TerminationGracePeriod = TimeSpan.FromSeconds(2);

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

        var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
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
        return new ExternalToolProcessResult(
            process.ExitCode,
            stdoutTask.Result,
            stderrTask.Result);
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
        Task<string> stdoutTask,
        Task<string> stderrTask)
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

    private static async Task ObserveAsync(params Task<string>[] tasks)
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
}
