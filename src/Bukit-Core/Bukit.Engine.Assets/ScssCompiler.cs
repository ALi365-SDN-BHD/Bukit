using System.Diagnostics;
using System.Globalization;
using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class ScssCompiler
{
    internal static async Task CompileIfEnabled(
        string assetsDir,
        ScssConfig? scssConfig,
        ILogger logger,
        CancellationToken cancellationToken = default,
        string? generatedOutputDir = null)
    {
        if (scssConfig is not { Enabled: true })
        {
            return;
        }

        var scssFiles = ResolveScssFiles(assetsDir, scssConfig);
        if (scssFiles.Length == 0)
        {
            return;
        }

        var sassCli = await FindSassCliAsync(cancellationToken, logger);
        if (sassCli is null)
        {
            logger.Warn("event=scss.skip reason=sass_cli_not_found message=Install Dart Sass (npm install -g sass) for SCSS compilation. SCSS files will be ignored.");
            return;
        }

        var outputRoot = generatedOutputDir ?? Path.Combine(assetsDir, ".bukit-scss-output");

        foreach (var scssFile in scssFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var relativeScssPath = Path.GetRelativePath(assetsDir, scssFile);
                var cssFile = Path.ChangeExtension(Path.Combine(outputRoot, relativeScssPath), ".css");
                Directory.CreateDirectory(Path.GetDirectoryName(cssFile)!);
                var temporaryCssFile = Path.Combine(
                    Path.GetDirectoryName(cssFile)!,
                    $".{Path.GetFileNameWithoutExtension(cssFile)}.bukit-{Guid.NewGuid():N}.css");
                var startInfo = new ProcessStartInfo
                {
                    FileName = sassCli,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add(scssFile);
                startInfo.ArgumentList.Add(temporaryCssFile);
                startInfo.ArgumentList.Add("--no-source-map");
                startInfo.ArgumentList.Add("--style=compressed");
                try
                {
                    var result = await RunSassProcessAsync("compile", sassCli,
                        startInfo,
                        TimeSpan.FromSeconds(5),
                        logger,
                        cancellationToken);
                    if (result.ExitCode == 0 && IsReadableFile(temporaryCssFile))
                    {
                        File.Move(temporaryCssFile, cssFile, overwrite: true);
                        logger.Info($"event=scss.compiled file={Path.GetFileName(scssFile)}");
                    }
                    else
                    {
                        var reason = result.ExitCode == 0 ? "output_missing" : "compile_failed";
                        logger.Warn($"event=scss.error file={Path.GetFileName(scssFile)} reason={reason} detail={result.StandardError}");
                    }
                }
                finally
                {
                    TryDelete(temporaryCssFile);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.Warn($"event=scss.error file={Path.GetFileName(scssFile)} reason={ex.Message}");
            }
        }
    }

    private static async Task<ExternalToolProcessResult> RunSassProcessAsync(
        string phase, string tool, ProcessStartInfo startInfo, TimeSpan timeout,
        ILogger logger, CancellationToken cancellationToken)
    {
        void Observe(string message)
        {
            // Supplemental diagnostics must not replace tool results or cancellation when a sink fails.
            try { logger.Debug(message); }
            catch { }
        }

        var attempt = Guid.NewGuid().ToString("N");
        Observe($"event=scss.tool.start attempt={attempt} phase={phase} tool={tool} timeout_ms={timeout.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture)}");
        var stopwatch = Stopwatch.StartNew();
        ExternalToolProcessResult? result = null;
        var outcome = "error";
        var error = "none";
        try
        {
            result = await ExternalToolProcessRunner.RunAsync(startInfo, timeout, logger, cancellationToken);
            outcome = "exit";
            return result;
        }
        catch (Exception ex)
        {
            outcome = ex is TimeoutException ? "timeout" : ex is OperationCanceledException ? "cancelled" : "error";
            error = ex.GetType().Name;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var exitCode = result?.ExitCode.ToString(CultureInfo.InvariantCulture) ?? "unavailable";
            var stderr = result is null ? "unavailable" : result.StandardError.Length == 0 ? "empty" : "redacted";
            var stderrChars = result?.StandardError.Length.ToString(CultureInfo.InvariantCulture) ?? "unavailable";
            Observe($"event=scss.tool.end attempt={attempt} phase={phase} tool={tool} outcome={outcome} exit_code={exitCode} elapsed_ms={stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)} stderr={stderr} stderr_chars={stderrChars} error={error}");
        }
    }

    private static string[] ResolveScssFiles(string assetsDir, ScssConfig scssConfig)
    {
        if (scssConfig.EntryPoint is null)
        {
            return SafeFileEnumerator.EnumerateFiles(assetsDir)
                .Where(path => string.Equals(
                    Path.GetExtension(path),
                    ".scss",
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        var assetsRoot = Path.GetFullPath(assetsDir)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedEntryPoint = scssConfig.EntryPoint
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        var entryPoint = Path.GetFullPath(Path.Combine(assetsRoot, normalizedEntryPoint));
        var rootPrefix = assetsRoot + Path.DirectorySeparatorChar;
        if (!entryPoint.StartsWith(rootPrefix, PlatformPathHelper.PathComparison))
        {
            throw new ConfigException(
                "theme.scss.entryPoint must resolve within the theme assets directory.",
                DiagnosticCode.ConfigPathTraversal);
        }

        if (!File.Exists(entryPoint))
        {
            throw new ConfigException(
                $"theme.scss.entryPoint '{scssConfig.EntryPoint}' does not exist in the theme assets directory.",
                DiagnosticCode.ConfigInvalidValue);
        }

        return [entryPoint];
    }

    private static async Task<string?> FindSassCliAsync(CancellationToken cancellationToken, ILogger logger)
    {
        var names = new[] { "sass", "dart-sass" };

        foreach (var name in names)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var result = await RunSassProcessAsync("probe", name, new ProcessStartInfo
                {
                    FileName = name,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }, TimeSpan.FromSeconds(3), logger, cancellationToken);
                if (result.ExitCode == 0)
                {
                    return name;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.Debug($"event=scss.tool.probe.failed tool={name} reason={ex.Message}");
            }
        }

        return null;
    }

    private static bool IsReadableFile(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
