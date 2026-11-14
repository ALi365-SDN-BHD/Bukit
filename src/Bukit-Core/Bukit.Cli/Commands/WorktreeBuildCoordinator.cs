using System.Text.RegularExpressions;
using Bukit.Cli.Shared;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

internal sealed class WorktreeBuildException(string code, string message, Exception? innerException = null)
    : Exception($"[{code}] {message}", innerException)
{
    internal string Code { get; } = code;
}

internal static partial class WorktreeBuildCoordinator
{
    private const string WorkerEnvironmentVariable = "BUKIT_INTERNAL_I18N_WORKER_REQUEST";
    private static readonly TimeSpan WorkerTimeout = TimeSpan.FromHours(1);

    [GeneratedRegex(
        @"^Error: \[(BKT-[0-9A-F]{4})\] (.*)$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex WorkerFailureRegex();

    internal static bool IsWorker => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(WorkerEnvironmentVariable));

    internal static async Task<int> RunWorkerAsync(CancellationToken cancellationToken)
    {
        var requestPath = Environment.GetEnvironmentVariable(WorkerEnvironmentVariable)
            ?? throw new WorktreeBuildException("i18n-worktree-result-invalid", "Internal worker request path is missing.");
        try
        {
            var request = await I18nWorktreeProtocolStore.ReadRequestAsync(requestPath, cancellationToken).ConfigureAwait(false);
            var snapshot = await I18nWorktreeProtocolStore.ReadContentAsync(request.SnapshotPath, cancellationToken).ConfigureAwait(false);
            var contentHash = await I18nWorktreeProtocolStore.Sha256Async(request.SnapshotPath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(request.ContentHash, contentHash, StringComparison.Ordinal) ||
                !string.Equals(request.ConfigHash, I18nWorktreeProtocol.ComputeConfigHash(request.Config, request.Overrides), StringComparison.Ordinal) ||
                !string.Equals(request.Head, snapshot.Head, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Content snapshot hash or HEAD does not match the worker request.");
            }

            // Workers read the parent's snapshot: the committed one offline, or the parent's
            // staging directory while it records (only the parent commits).
            using var notionSnapshot = string.IsNullOrWhiteSpace(request.Overrides.NotionSnapshotDir)
                ? null
                : NotionSnapshot.AttachWorker(request.Overrides.NotionSnapshotDir, request.Overrides.NotionOffline);
            using var notionSnapshotScope = NotionSnapshot.Enter(notionSnapshot);
            var level = request.Overrides.IsCI
                ? LogLevel.Warn
                : ParseLogLevel(request.Config.Logging.Level);
            var logger = new ConsoleLogger(level, request.LogFormat);
            var engine = new SiteEngine(logger);
            var (buildResult, warningCount, errorCount) = await engine.ExecuteI18nWorktreeVariantAsync(
                request,
                snapshot,
                logger,
                cancellationToken).ConfigureAwait(false);
            var result = await I18nWorktreeProtocol.CaptureVariantAsync(
                request.Head,
                request.ConfigHash,
                request.ContentHash,
                buildResult,
                warningCount,
                errorCount,
                cancellationToken).ConfigureAwait(false);
            await I18nWorktreeProtocolStore.WriteResultAsync(request.ResultPath, result, cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex) when (ex is not WorktreeBuildException)
        {
            var code = ex is BukitException { Code: { } diagnosticCode }
                ? DiagnosticCodeFormatter.Format(diagnosticCode)
                : "i18n-worktree-worker-failed";
            throw new WorktreeBuildException(
                code,
                $"Internal multilingual build worker failed: {ex.Message}",
                ex);
        }
    }

    internal static async Task<int> RunAsync(
        AppConfig config,
        ResolvedConfigPath resolved,
        ConfigOverrides overrides,
        string logFormat,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var run = await WorktreeBuildRun.StartAsync(config, resolved, overrides, logFormat, logger, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await run.PrepareAsync(cancellationToken).ConfigureAwait(false);
            await run.ExecuteWorkersAsync(cancellationToken).ConfigureAwait(false);
            await run.FinalizeAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (WorktreeBuildException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new WorktreeBuildException("i18n-worktree-worker-failed", $"Multilingual Worktree build failed: {ex.Message}", ex);
        }
        finally
        {
            await run.CleanupAsync().ConfigureAwait(false);
        }
    }

    internal static async Task RunWorkerProcessAsync(WorkerPlan plan, ILogger logger, CancellationToken cancellationToken)
    {
        var (fileName, prefixArguments) = SelfInvocation();
        var startInfo = WorktreeBuildRepository.NewProcessStartInfo(fileName);
        foreach (var argument in prefixArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        startInfo.ArgumentList.Add("build");
        startInfo.Environment[WorkerEnvironmentVariable] = plan.RequestPath;
        // Secrets passed with --secrets-stdin are forwarded to the worker over its stdin, never
        // through its environment or arguments.
        string? secretsInput = null;
        if (SecretStore.HasStdinSecrets)
        {
            startInfo.ArgumentList.Add(SecretStore.StdinFlag);
            secretsInput = SecretStore.SerializeStdinSecrets();
        }
        var result = await I18nWorktreeGitRunner.RunAsync(startInfo, WorkerTimeout, logger, secretsInput, cancellationToken).ConfigureAwait(false);
        if (result.StandardOutput.Length > 0)
        {
            await Console.Out.WriteAsync(result.StandardOutput).ConfigureAwait(false);
        }
        if (result.StandardError.Length > 0)
        {
            await Console.Error.WriteAsync(result.StandardError).ConfigureAwait(false);
        }
        if (result.ExitCode != 0)
        {
            var failure = LastWorkerFailure(result.StandardError);
            throw new WorktreeBuildException(
                failure?.Code ?? "i18n-worktree-worker-failed",
                failure is null
                    ? $"Worker for language '{plan.Language}' exited with code {result.ExitCode}."
                    : $"Worker for language '{plan.Language}' failed: {failure.Value.Message}");
        }
    }

    internal static (string Code, string Message)? LastWorkerFailure(string standardError)
    {
        var matches = WorkerFailureRegex().Matches(standardError);
        if (matches.Count == 0)
        {
            return null;
        }

        var match = matches[^1];
        return (match.Groups[1].Value, match.Groups[2].Value.TrimEnd('\r'));
    }

    private static (string FileName, IReadOnlyList<string> PrefixArguments) SelfInvocation()
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Current Bukit executable path is unavailable.");
        if (!string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return (processPath, []);
        }
        var assemblyPath = Environment.GetCommandLineArgs().FirstOrDefault();
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new InvalidOperationException("Current Bukit managed entry assembly path is unavailable.");
        }
        return (processPath, [assemblyPath]);
    }

    private static LogLevel ParseLogLevel(string? level)
        => (level ?? "info").Trim().ToLowerInvariant() switch
        {
            "debug" => LogLevel.Debug,
            "warn" => LogLevel.Warn,
            "error" => LogLevel.Error,
            _ => LogLevel.Info
        };
}
