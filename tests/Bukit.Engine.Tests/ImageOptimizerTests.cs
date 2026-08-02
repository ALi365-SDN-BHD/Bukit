using Bukit.Config;
using Bukit.Shared;
using Xunit;
using Xunit.Sdk;

namespace Bukit.Engine.Tests;

public sealed class ImageOptimizerTests
{
    [Theory]
    [InlineData("webp", "cwebp")]
    [InlineData("webp", "magick")]
    [InlineData("webp", "convert")]
    [InlineData("avif", "magick")]
    [InlineData("avif", "convert")]
    public async Task OptimizeIfEnabled_UsesFormatCompatibleToolAndArguments(
        string format,
        string toolName)
    {
        RequireUnix();
        var root = CreateRoot();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        var originalLog = Environment.GetEnvironmentVariable("BUKIT_IMAGE_TOOL_LOG");
        try
        {
            var assetsDir = Path.Combine(root, "assets");
            var toolDir = Path.Combine(root, "tools");
            var logPath = Path.Combine(root, "tool.log");
            Directory.CreateDirectory(assetsDir);
            Directory.CreateDirectory(toolDir);
            var input = Path.Combine(assetsDir, "photo.jpg");
            File.WriteAllText(input, "input");
            WriteTool(toolDir, toolName, """
                if [ "$1" = "-version" ] || [ "$1" = "--version" ]; then exit 0; fi
                printf '%s\n' "$*" >> "$BUKIT_IMAGE_TOOL_LOG"
                for last in "$@"; do :; done
                printf converted > "$last"
                """);
            Environment.SetEnvironmentVariable("PATH", toolDir);
            Environment.SetEnvironmentVariable("BUKIT_IMAGE_TOOL_LOG", logPath);

            await ImageOptimizer.OptimizeIfEnabled(
                assetsDir,
                new ImageOptimizationConfig
                {
                    Enabled = true,
                    Formats = new[] { format },
                    Quality = 73
                },
                new ConsoleLogger(LogLevel.Error));

            Assert.True(File.Exists(Path.ChangeExtension(input, $".{format}")));
            var args = Assert.Single(File.ReadAllLines(logPath));
            if (toolName == "cwebp")
            {
                Assert.StartsWith($"-q 73 {input} -o ", args, StringComparison.Ordinal);
            }
            else
            {
                Assert.StartsWith($"{input} -quality 73 ", args, StringComparison.Ordinal);
                Assert.False(args.StartsWith("magick ", StringComparison.Ordinal), args);
            }

            Assert.EndsWith($".{format}", args, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Environment.SetEnvironmentVariable("BUKIT_IMAGE_TOOL_LOG", originalLog);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OptimizeIfEnabled_AvifDoesNotProbeOrInvokeCwebp()
    {
        RequireUnix();
        var root = CreateRoot();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        var originalLog = Environment.GetEnvironmentVariable("BUKIT_IMAGE_TOOL_LOG");
        try
        {
            var assetsDir = Path.Combine(root, "assets");
            var toolDir = Path.Combine(root, "tools");
            var logPath = Path.Combine(root, "tool.log");
            Directory.CreateDirectory(assetsDir);
            Directory.CreateDirectory(toolDir);
            var input = Path.Combine(assetsDir, "photo.jpg");
            File.WriteAllText(input, "input");
            WriteTool(toolDir, "cwebp", """
                printf '%s\n' "$*" >> "$BUKIT_IMAGE_TOOL_LOG"
                if [ "$1" = "-version" ]; then exit 0; fi
                for last in "$@"; do :; done
                printf converted > "$last"
                """);
            Environment.SetEnvironmentVariable("PATH", toolDir);
            Environment.SetEnvironmentVariable("BUKIT_IMAGE_TOOL_LOG", logPath);

            await ImageOptimizer.OptimizeIfEnabled(
                assetsDir,
                new ImageOptimizationConfig { Enabled = true, Formats = new[] { "avif" } },
                new ConsoleLogger(LogLevel.Error));

            Assert.False(File.Exists(Path.ChangeExtension(input, ".avif")));
            Assert.False(File.Exists(logPath));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Environment.SetEnvironmentVariable("BUKIT_IMAGE_TOOL_LOG", originalLog);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateRoot()
        => Path.Combine(Path.GetTempPath(), "bukit-image-optimizer-" + Guid.NewGuid().ToString("N"));

    private static void WriteTool(string directory, string name, string body)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }

        var path = Path.Combine(directory, name);
        File.WriteAllText(path, "#!/bin/sh\n" + body + "\n");
        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static void RequireUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            throw SkipException.ForSkip("This command-matrix test uses temporary Unix executables.");
        }
    }
}
