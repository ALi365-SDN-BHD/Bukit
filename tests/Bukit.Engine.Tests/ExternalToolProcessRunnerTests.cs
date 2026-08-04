using System.Diagnostics;
using System.Text;
using Bukit.Config;
using Bukit.Shared;
using Xunit;
using Xunit.Sdk;

namespace Bukit.Engine.Tests;

public sealed class ExternalToolProcessRunnerTests
{
    [Fact]
    public async Task WaitForTerminationGraceAsync_IncompleteCleanup_ReturnsFalseWithinGrace()
    {
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopwatch = Stopwatch.StartNew();

        var wait = ExternalToolProcessRunner.WaitForTerminationGraceAsync(
            never.Task,
            TimeSpan.FromMilliseconds(50));
        var completed = await wait.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.False(completed);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task BoundedOutputCollector_GetText_SealsSnapshotAgainstLatePumpWrites()
    {
        using var process = new Process();
        using var stream = new TwoPhaseReadStream("before", "after");
        using var collector = new ExternalToolProcessRunner.BoundedOutputCollector(1024);
        Task readTask = collector.ReadAsync(stream, process, CancellationToken.None);
        await stream.SecondReadStarted.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal("before", collector.GetText());

        stream.ReleaseSecondRead();
        await readTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal("before", collector.GetText());
    }

    [Fact]
    public async Task BoundedOutputCollector_GetDiagnosticText_MalformedUtf8_EmitsByteBoundedHeadAndTail()
    {
        var bytes = Enumerable.Repeat((byte)0xff, 96 * 1024).ToArray();
        using var process = new Process();
        using var stream = new MemoryStream(bytes);
        using var collector = new ExternalToolProcessRunner.BoundedOutputCollector(128 * 1024);

        await collector.ReadAsync(stream, process, CancellationToken.None);

        var diagnostic = collector.GetDiagnosticText();
        AssertDiagnosticSlicesAreUtf8ByteBounded(diagnostic);
    }

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

    [Fact]
    public async Task ScssCompiler_ConfiguredEntryPointMissing_Throws()
    {
        var root = CreateTempDir();
        try
        {
            var assetsDir = Path.Combine(root, "assets");
            Directory.CreateDirectory(assetsDir);

            var exception = await Assert.ThrowsAsync<ConfigException>(() =>
                ScssCompiler.CompileIfEnabled(
                    assetsDir,
                    new ScssConfig { Enabled = true, EntryPoint = "styles/main.scss" },
                    new ConsoleLogger(LogLevel.Error)));

            Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
            Assert.Contains("styles/main.scss", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AssetSourceWorkspace_ConfiguredEntryPointWithMissingAssetsDir_Throws()
    {
        var root = CreateTempDir();
        try
        {
            var exception = await Assert.ThrowsAsync<ConfigException>(() =>
                AssetSourceWorkspace.PrepareAsync(
                    Path.Combine(root, "missing-assets"),
                    new ScssConfig { Enabled = true, EntryPoint = "styles/main.scss" },
                    imageConfig: null,
                    new ConsoleLogger(LogLevel.Error),
                    publishDotFiles: false,
                    followSymlinks: false));

            Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
            Assert.Contains("styles/main.scss", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ScssCompiler_EntryPointUnset_CompilesAllFilesToStagingTree()
    {
        RequireUnix();
        var root = CreateTempDir();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var assetsDir = Path.Combine(root, "assets");
            var nestedDir = Path.Combine(assetsDir, "nested");
            var toolsDir = Path.Combine(root, "tools");
            var outputDir = Path.Combine(root, "scss-output");
            Directory.CreateDirectory(nestedDir);
            File.WriteAllText(Path.Combine(assetsDir, "main.scss"), "body { color: red; }");
            File.WriteAllText(Path.Combine(assetsDir, "UPPER.SCSS"), "body { color: green; }");
            File.WriteAllText(Path.Combine(nestedDir, "theme.scss"), "body { color: blue; }");
            WriteTool(toolsDir, "sass", """
                if [ "$1" = "--version" ]; then
                  exit 0
                fi
                printf 'compiled' > "$2"
                """);
            Environment.SetEnvironmentVariable("PATH", PrependPath(toolsDir, originalPath));

            await ScssCompiler.CompileIfEnabled(
                assetsDir,
                new ScssConfig { Enabled = true },
                new ConsoleLogger(LogLevel.Error),
                generatedOutputDir: outputDir);

            Assert.Equal("compiled", File.ReadAllText(Path.Combine(outputDir, "main.css")));
            Assert.Equal("compiled", File.ReadAllText(Path.Combine(outputDir, "UPPER.css")));
            Assert.Equal("compiled", File.ReadAllText(Path.Combine(outputDir, "nested", "theme.css")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_OutputBeyondLimit_TerminatesAndThrowsBoundedDiagnostic()
    {
        RequireUnix();
        var root = CreateTempDir();
        try
        {
            var marker = Path.Combine(root, "overflow-child-completed.marker");
            var tool = WriteTool(root, "flood-beyond-limit", $"""
                ( head -c 5242880 /dev/zero; /bin/sleep 1; printf completed > '{EscapeSingleQuoted(marker)}' ) &
                wait
                """);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ExternalToolProcessRunner.RunAsync(
                    StartInfo(tool),
                    TimeSpan.FromSeconds(30),
                    CancellationToken.None));

            Assert.Contains("produced more than", exception.Message, StringComparison.Ordinal);
            Assert.Contains("stdout", exception.Message, StringComparison.Ordinal);
            await Task.Delay(1200);
            Assert.False(File.Exists(marker), "External-tool overflow should terminate the process tree before its child can finish.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExternalTool_OutputBeyondLimit_TerminatesTreeAndReturnsBoundedDiagnostic()
    {
        RequireUnix();
        var root = CreateTempDir();
        try
        {
            var tool = WriteTool(root, "large-failure", """
                head -c 32768 /dev/zero | tr '\000' H >&2
                head -c 65536 /dev/zero | tr '\000' M >&2
                head -c 32768 /dev/zero | tr '\000' T >&2
                exit 1
                """);

            var result = await ExternalToolProcessRunner.RunAsync(
                StartInfo(tool),
                TimeSpan.FromSeconds(10),
                CancellationToken.None);

            Assert.Equal(1, result.ExitCode);
            Assert.StartsWith(new string('H', 256), result.StandardError, StringComparison.Ordinal);
            Assert.EndsWith(new string('T', 256), result.StandardError, StringComparison.Ordinal);
            Assert.Contains("[truncated", result.StandardError, StringComparison.Ordinal);
            Assert.True(Encoding.UTF8.GetByteCount(result.StandardError) <= 66 * 1024,
                $"External-tool diagnostic was {Encoding.UTF8.GetByteCount(result.StandardError)} UTF-8 bytes.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_Timeout_WithInheritedPipes_ReturnsWithinDrainDeadline()
    {
        RequireUnix();
        var root = CreateTempDir();
        try
        {
            var tool = WriteTool(root, "long-sleep", """
                ( /bin/sleep 30 ) &
                wait
                """);

            var stopwatch = Stopwatch.StartNew();
            await Assert.ThrowsAsync<TimeoutException>(() =>
                ExternalToolProcessRunner.RunAsync(
                    StartInfo(tool),
                    TimeSpan.FromMilliseconds(200),
                    CancellationToken.None));
            stopwatch.Stop();

            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(8),
                $"Timeout + drain took {stopwatch.Elapsed.TotalSeconds:F1}s, expected < 8s");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ParentExitsWithInheritedPipe_ReturnsWithinDrainDeadline()
    {
        RequireUnix();
        var root = CreateTempDir();
        try
        {
            var tool = WriteTool(root, "parent-exits", """
                ( /bin/sleep 3 ) &
                exit 0
                """);

            var stopwatch = Stopwatch.StartNew();
            var result = await ExternalToolProcessRunner.RunAsync(
                StartInfo(tool),
                TimeSpan.FromSeconds(10),
                CancellationToken.None);
            stopwatch.Stop();

            Assert.Equal(0, result.ExitCode);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
                $"External-tool drain took {stopwatch.Elapsed.TotalSeconds:F1}s with an inherited pipe.");
        }
        finally
        {
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

    private static void AssertDiagnosticSlicesAreUtf8ByteBounded(string diagnostic)
    {
        const string markerStart = "\n... [truncated ";
        var markerIndex = diagnostic.IndexOf(markerStart, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, "Expected a diagnostic truncation marker.");
        var tailStart = diagnostic.IndexOf("] ...\n", markerIndex, StringComparison.Ordinal);
        Assert.True(tailStart >= 0, "Expected a complete diagnostic truncation marker.");

        var head = diagnostic[..markerIndex];
        var tail = diagnostic[(tailStart + "] ...\n".Length)..];
        Assert.True(Encoding.UTF8.GetByteCount(head) <= 32 * 1024);
        Assert.True(Encoding.UTF8.GetByteCount(tail) <= 32 * 1024);
    }

    private static string PrependPath(string directory, string? originalPath) =>
        string.IsNullOrEmpty(originalPath) ? directory : directory + Path.PathSeparator + originalPath;

    private static void RequireUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            throw SkipException.ForSkip("This process-tree probe uses temporary Unix executables.");
        }
    }

    private sealed class TwoPhaseReadStream(string first, string second) : Stream
    {
        private readonly byte[] _first = Encoding.UTF8.GetBytes(first);
        private readonly byte[] _second = Encoding.UTF8.GetBytes(second);
        private readonly TaskCompletionSource _secondReadStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseSecondRead =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _readCount;

        internal Task SecondReadStarted => _secondReadStarted.Task;
        internal void ReleaseSecondRead() => _releaseSecondRead.TrySetResult();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var readCount = Interlocked.Increment(ref _readCount);
            if (readCount == 1)
            {
                _first.AsSpan().CopyTo(buffer.Span);
                return _first.Length;
            }

            if (readCount == 2)
            {
                _secondReadStarted.TrySetResult();
                await _releaseSecondRead.Task.WaitAsync(cancellationToken);
                _second.AsSpan().CopyTo(buffer.Span);
                return _second.Length;
            }

            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
