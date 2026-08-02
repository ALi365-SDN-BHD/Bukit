using System.Diagnostics;
using Bukit.Config;
using Bukit.Shared;
using Xunit;
using Xunit.Sdk;

namespace Bukit.Engine.Tests;

public sealed class ExternalToolProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_FloodsStdoutAndStderr_CompletesWithoutDeadlock()
    {
        RequireUnix();
        var root = CreateTempDir();
        try
        {
            var tool = WriteTool(root, "flood", """
                i=0
                while [ "$i" -lt 4096 ]; do
                  echo "stdout-$i"
                  echo "stderr-$i" >&2
                  i=$((i + 1))
                done
                """);

            var result = await ExternalToolProcessRunner.RunAsync(
                StartInfo(tool),
                TimeSpan.FromSeconds(5),
                CancellationToken.None);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("stdout-4095", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("stderr-4095", result.StandardError, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_TimesOut_KillsDescendantBeforeDelayedWrite()
    {
        RequireUnix();
        var root = CreateTempDir();
        try
        {
            var marker = Path.Combine(root, "late-marker");
            var tool = WriteTool(root, "timeout", $"""
                ( sleep 1; printf late > '{EscapeSingleQuoted(marker)}' ) &
                wait
                """);

            await Assert.ThrowsAsync<TimeoutException>(() =>
                ExternalToolProcessRunner.RunAsync(
                    StartInfo(tool),
                    TimeSpan.FromMilliseconds(150),
                    CancellationToken.None));

            await Task.Delay(1200);
            Assert.False(File.Exists(marker));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ScssCompiler_ExitZeroWithoutCss_PreservesSource()
    {
        RequireUnix();
        var root = CreateTempDir();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var assetsDir = Path.Combine(root, "assets");
            var toolsDir = Path.Combine(root, "tools");
            Directory.CreateDirectory(assetsDir);
            Directory.CreateDirectory(toolsDir);
            var source = Path.Combine(assetsDir, "main.scss");
            File.WriteAllText(source, "$color: red; body { color: $color; }");
            WriteTool(toolsDir, "sass", "exit 0");
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolsDir, originalPath));

            await ScssCompiler.CompileIfEnabled(
                assetsDir,
                new ScssConfig { Enabled = true },
                new ConsoleLogger(LogLevel.Error));

            Assert.True(File.Exists(source));
            Assert.False(File.Exists(Path.ChangeExtension(source, ".css")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(root, recursive: true);
        }
    }

    private static ProcessStartInfo StartInfo(string tool) => new()
    {
        FileName = tool,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    private static string WriteTool(string directory, string name, string body)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }

        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, "#!/bin/sh\n" + body + "\n");
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "bukit-external-tool-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string EscapeSingleQuoted(string value) => value.Replace("'", "'\\''", StringComparison.Ordinal);

    private static string PrependPath(string directory, string? originalPath) =>
        string.IsNullOrEmpty(originalPath) ? directory : directory + Path.PathSeparator + originalPath;

    private static void RequireUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            throw SkipException.ForSkip("This process-tree probe uses temporary Unix executables.");
        }
    }
}
