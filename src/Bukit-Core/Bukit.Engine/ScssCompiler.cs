using System.Diagnostics;
using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class ScssCompiler
{
    internal static async Task CompileIfEnabled(string assetsDir, ScssConfig? scssConfig, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (scssConfig is not { Enabled: true })
        {
            return;
        }

        var scssFiles = SafeFileEnumerator.EnumerateFiles(assetsDir, "*.scss").ToArray();
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

        foreach (var scssFile in scssFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var cssFile = Path.ChangeExtension(scssFile, ".css");
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
                    var result = await ExternalToolProcessRunner.RunAsync(
                        startInfo,
                        TimeSpan.FromSeconds(5),
                        cancellationToken);
                    if (result.ExitCode == 0 && IsReadableFile(temporaryCssFile))
                    {
                        File.Move(temporaryCssFile, cssFile, overwrite: true);
                        logger.Info($"event=scss.compiled file={Path.GetFileName(scssFile)}");
                        File.Delete(scssFile);
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

    private static async Task<string?> FindSassCliAsync(CancellationToken cancellationToken = default, ILogger? logger = null)
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
                var result = await ExternalToolProcessRunner.RunAsync(new ProcessStartInfo
                {
                    FileName = name,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }, TimeSpan.FromSeconds(3), cancellationToken);
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
                logger?.Debug($"event=scss.tool.probe.failed tool={name} reason={ex.Message}");
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
