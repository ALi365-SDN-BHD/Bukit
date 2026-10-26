using System.Diagnostics;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed record I18nGitCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

internal static class I18nWorktreeGitRunner
{
    internal static async Task<I18nGitCommandResult> RunAsync(
        ProcessStartInfo startInfo,
        TimeSpan timeout,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var result = await ExternalToolProcessRunner.RunAsync(startInfo, timeout, logger, cancellationToken)
            .ConfigureAwait(false);
        return new I18nGitCommandResult(result.ExitCode, result.StandardOutput, result.StandardError);
    }
}
