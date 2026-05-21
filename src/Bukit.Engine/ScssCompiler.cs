using System.Diagnostics;
using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class ScssCompiler
{
    internal static void CompileIfEnabled(string assetsDir, ScssConfig? scssConfig, ILogger logger)
    {
        if (scssConfig is not { Enabled: true })
        {
            return;
        }

        var scssFiles = Directory.GetFiles(assetsDir, "*.scss", SearchOption.AllDirectories);
        if (scssFiles.Length == 0)
        {
            return;
        }

        var sassCli = FindSassCli();
        if (sassCli is null)
        {
            logger.Warn("event=scss.skip reason=sass_cli_not_found message=Install Dart Sass (npm install -g sass) for SCSS compilation. SCSS files will be ignored.");
            return;
        }

        foreach (var scssFile in scssFiles)
        {
            try
            {
                var cssFile = Path.ChangeExtension(scssFile, ".css");
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = sassCli,
                    Arguments = $"\"{scssFile}\" \"{cssFile}\" --no-source-map --style=compressed",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process is null)
                {
                    logger.Warn($"event=scss.error file={Path.GetFileName(scssFile)} reason=process_failed");
                    continue;
                }

                process.WaitForExit(5000);
                if (process.ExitCode == 0)
                {
                    logger.Info($"event=scss.compiled file={Path.GetFileName(scssFile)}");

                    if (File.Exists(scssFile))
                    {
                        File.Delete(scssFile);
                    }
                }
                else
                {
                    var stderr = process.StandardError.ReadToEnd();
                    logger.Warn($"event=scss.error file={Path.GetFileName(scssFile)} reason=compile_failed detail={stderr}");
                }
            }
            catch (Exception ex)
            {
                logger.Warn($"event=scss.error file={Path.GetFileName(scssFile)} reason={ex.Message}");
            }
        }
    }

    private static string? FindSassCli()
    {
        var names = new[] { "sass", "dart-sass" };

        foreach (var name in names)
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = name,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process is not null)
                {
                    process.WaitForExit(3000);
                    if (process.ExitCode == 0)
                    {
                        return name;
                    }
                }
            }
            catch
            {
            }
        }

        return null;
    }
}
