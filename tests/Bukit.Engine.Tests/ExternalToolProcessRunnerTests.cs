using Xunit.Abstractions;
using System.Diagnostics;
using System.Text;
using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ExternalToolProcessRunnerTests(ITestOutputHelper output)
{
    private static readonly ILogger Logger = new ConsoleLogger(LogLevel.Warn);

    [Fact]
    public async Task RunAsync_CallerCancellationRemainsCancellationAndRequiresLogger()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => ExternalToolProcessRunner.RunAsync(
            StartInfo("unused"), TimeSpan.FromSeconds(1), null!));
        if (!RequireUnix()) return;
        string root = Path.Combine(Path.GetTempPath(), "bukit-tool-cancel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string tool = WriteTool(root, "cancel", "sleep 30");
            using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ExternalToolProcessRunner.RunAsync(
                StartInfo(tool), TimeSpan.FromSeconds(20), Logger, cancel.Token));
        }
        finally { TestCleanup.DeleteDirectory(root); }
    }
    [Fact]
    public async Task RunAsync_WindowsNativeProcess_CapturesOutputAndExitCode()
    {
        if (!OperatingSystem.IsWindows())
        {
            output.WriteLine("BUKIT_NOT_APPLICABLE: Windows native process probe");
            return;
        }
        var info = StartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"));
        info.ArgumentList.Add("/d");
        info.ArgumentList.Add("/c");
        info.ArgumentList.Add("echo windows-out & echo windows-error 1>&2 & exit /b 7");
        var result = await ExternalToolProcessRunner.RunAsync(info, TimeSpan.FromSeconds(10), Logger);
        Assert.Equal(7, result.ExitCode);
        Assert.Contains("windows-out", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("windows-error", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void PrepareSetSidStartInfo_PreservesExecutableAndArgumentsWithoutShellJobControl()
    {
        var startInfo = new ProcessStartInfo("/opt/bukit/tool");
        startInfo.ArgumentList.Add("--label");
        startInfo.ArgumentList.Add("value with spaces");

        ExternalToolProcessTree.PrepareSetSidStartInfo(startInfo, "/usr/bin/setsid");

        Assert.Equal("/usr/bin/setsid", startInfo.FileName);
        Assert.Equal(
            ["/opt/bukit/tool", "--label", "value with spaces"],
            startInfo.ArgumentList);
        Assert.DoesNotContain("set -m", string.Join(' ', startInfo.ArgumentList), StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyMacWrapperStartInfo_StatePathTravelsAsPositionalArgumentNotProgramText()
    {
        var startInfo = new ProcessStartInfo("/opt/bukit/tool");
        startInfo.ArgumentList.Add("--input");
        startInfo.ArgumentList.Add("value with spaces");
        var hostilePath = "/tmp/bukit'$(touch /tmp/bukit-pwned)'pgid";

        ExternalToolProcessTree.ApplyMacWrapperStartInfo(startInfo, hostilePath);

        Assert.Equal("/bin/sh", startInfo.FileName);
        Assert.Equal("-c", startInfo.ArgumentList[0]);
        Assert.Equal(ExternalToolProcessTree.MacWrapperProgram, startInfo.ArgumentList[1]);
        Assert.DoesNotContain(hostilePath, startInfo.ArgumentList[1], StringComparison.Ordinal);
        Assert.Equal(
            ["bukit-tool-tree", hostilePath, "/opt/bukit/tool", "--input", "value with spaces"],
            startInfo.ArgumentList.Skip(2));
    }

    [Fact]
    public async Task MacWrapper_StatePathWithSingleQuote_StillPublishesPgidAndRunsTool()
    {
        if (!RequireUnix()) return;
        var root = CreateTempDir();
        try
        {
            var quotedDir = Path.Combine(root, "state'dir with'quotes");
            Directory.CreateDirectory(quotedDir);
            var pgidPath = Path.Combine(quotedDir, "pgid");
            var startInfo = StartInfo("/bin/echo");
            startInfo.ArgumentList.Add("bukit-wrapper-probe");

            ExternalToolProcessTree.ApplyMacWrapperStartInfo(startInfo, pgidPath);

            using var process = Process.Start(startInfo)!;
            var stdout = await process.StandardOutput.ReadToEndAsync();
            using var exitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await process.WaitForExitAsync(exitTimeout.Token);

            Assert.Equal(0, process.ExitCode);
            Assert.Contains("bukit-wrapper-probe", stdout, StringComparison.Ordinal);
            Assert.True(File.Exists(pgidPath), "The wrapper must publish its group pgid even for quoted state paths.");
            var text = await File.ReadAllTextAsync(pgidPath);
            Assert.True(
                int.TryParse(
                    text.Trim(),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var publishedPid) && publishedPid > 0,
                "The published pgid must be a positive integer.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ParentExitWithPipeChild_TerminatesTreeAndReaders()
    {
        if (OperatingSystem.IsWindows())
        {
            output.WriteLine("BUKIT_NOT_APPLICABLE: POSIX process probe");
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "bukit-exttool-tree-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var markerPath = Path.Combine(root, "child.pid");
        try
        {
            // Parent exits immediately after backgrounding a child that inherits the
            // stdout pipe and sleeps; the runner must terminate the leftover process
            // tree so its output readers close instead of draining forever.
            var startInfo = new ProcessStartInfo("/bin/sh")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add($"sleep 60 & echo $! > '{markerPath}'; exit 0");

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => ExternalToolProcessRunner.RunAsync(startInfo, TimeSpan.FromSeconds(30), Logger));
            Assert.Contains("process tree", exception.Message, StringComparison.OrdinalIgnoreCase);

            var childPidText = await File.ReadAllTextAsync(markerPath);
            var childPid = int.Parse(childPidText.Trim(), System.Globalization.CultureInfo.InvariantCulture);
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (IsProcessAlive(childPid) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            Assert.False(IsProcessAlive(childPid));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

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
        if (!RequireUnix()) return;
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
                Logger,
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
        if (!RequireUnix()) return;
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
                    Logger,
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
        if (!RequireUnix()) return;
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
        if (!RequireUnix()) return;
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
        if (!RequireUnix()) return;
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
                    Logger,
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
        if (!RequireUnix()) return;
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
                Logger,
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
        if (!RequireUnix()) return;
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
                    Logger,
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
        if (!RequireUnix()) return;
        var root = CreateTempDir();
        try
        {
            var tool = WriteTool(root, "parent-exits", """
                ( /bin/sleep 30 ) &
                exit 0
                """);

            var stopwatch = Stopwatch.StartNew();
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => ExternalToolProcessRunner.RunAsync(
                    StartInfo(tool),
                    TimeSpan.FromSeconds(30),
                    Logger,
                    CancellationToken.None));
            stopwatch.Stop();

            // The leftover descendant holding the pipe is a stable failure (CI-02):
            // bounded drain, terminate the tree, and report instead of returning a
            // result while a background process stays attached to our pipes.
            Assert.Contains("process tree", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
                $"Drain + tree termination took {stopwatch.Elapsed.TotalSeconds:F1}s, expected < 10s");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("success")]
    [InlineData("debug-sink-fails")]
    [InlineData("probe-fallback")]
    [InlineData("probe-rejected")]
    [InlineData("compile-failed")]
    [InlineData("output-missing")]
    [InlineData("probe-timeout")]
    [InlineData("compile-timeout")]
    [InlineData("probe-cancel")]
    [InlineData("compile-cancel")]
    public async Task ScssCompiler_ProcessObservation_PreservesOutcomesAndMasksStderr(string scenario)
    {
        if (!RequireUnix()) return;
        var root = CreateTempDir();
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        using var cancellation = new CancellationTokenSource();
        try
        {
            var assets = Path.Combine(root, "assets");
            var tools = Path.Combine(root, "tools");
            var generated = Path.Combine(root, "generated");
            var callsPath = Path.Combine(root, "calls");
            var marker = Path.Combine(root, "started");
            Directory.CreateDirectory(assets);
            var source = Path.Combine(assets, "main.scss");
            File.WriteAllText(source, "body {}");
            const string privateStderr = "private-scss-observation-token";
            foreach (var tool in new[] { "sass", "dart-sass" })
            {
                var probeExit = scenario == "probe-rejected" || (scenario == "probe-fallback" && tool == "sass") ? 7 : 0;
                var probe = scenario == "probe-timeout" && tool == "sass" ? "/bin/sleep 30; exit 0" : $"exit {probeExit}";
                if (scenario == "probe-cancel" && tool == "sass")
                    probe = $"printf started > '{EscapeSingleQuoted(marker)}'; /bin/sleep 30; exit 0";
                var compile = scenario switch
                {
                    "compile-failed" => "exit 7",
                    "output-missing" => "exit 0",
                    "compile-timeout" => "/bin/sleep 30; exit 0",
                    "compile-cancel" => $"printf started > '{EscapeSingleQuoted(marker)}'; /bin/sleep 30; exit 0",
                    _ => "printf compiled > \"$2\"; exit 0"
                };
                var stderr = scenario == "output-missing" ? string.Empty : $"printf '{privateStderr}\\n' >&2";
                WriteTool(tools, tool, $"""
                    {stderr}
                    if [ "$1" = "--version" ]; then
                      printf 'probe:{tool}\n' >> '{EscapeSingleQuoted(callsPath)}'
                      {probe}
                    fi
                    printf 'compile:{tool}\n' >> '{EscapeSingleQuoted(callsPath)}'
                    {compile}
                    """);
            }
            Environment.SetEnvironmentVariable("PATH", PrependPath(tools, originalPath));
            var logger = new ScssObservationLogger(scenario == "debug-sink-fails");
            var operation = ScssCompiler.CompileIfEnabled(assets,
                new ScssConfig { Enabled = true, EntryPoint = "main.scss" }, logger,
                cancellation.Token, generated);
            bool? sawMarker = null;
            if (scenario.EndsWith("-cancel", StringComparison.Ordinal))
            {
                var wait = Stopwatch.StartNew();
                while (!File.Exists(marker) && !operation.IsCompleted && wait.Elapsed < TimeSpan.FromSeconds(10))
                    await Task.Delay(10);
                sawMarker = File.Exists(marker);
                cancellation.Cancel();
            }
            var failure = await Record.ExceptionAsync(() => operation);
            if (sawMarker is not null)
            {
                Assert.True(sawMarker.Value, "Cancellation must follow actual entry into the requested fake-tool branch.");
                Assert.IsAssignableFrom<OperationCanceledException>(failure);
            }
            else Assert.Null(failure);
            Assert.Equal("body {}", File.ReadAllText(source));
            var producesCss = scenario is "success" or "debug-sink-fails" or "probe-fallback" or "probe-timeout";
            Assert.Equal(producesCss, File.Exists(Path.Combine(generated, "main.css")));
            if (producesCss) Assert.Equal("compiled", File.ReadAllText(Path.Combine(generated, "main.css")));
            if (scenario == "probe-rejected") Assert.Contains(logger.Warnings, message => message.Contains("reason=sass_cli_not_found", StringComparison.Ordinal));
            if (scenario == "compile-failed") Assert.Contains(logger.Warnings, message => message.Contains("reason=compile_failed", StringComparison.Ordinal));
            if (scenario == "output-missing") Assert.Contains(logger.Warnings, message => message.Contains("reason=output_missing", StringComparison.Ordinal));
            if (scenario == "compile-timeout") Assert.Contains(logger.Warnings, message => message.Contains("timed out after 5000ms", StringComparison.Ordinal));
            string[] expectedCalls = scenario switch
            {
                "probe-cancel" => ["probe:sass"],
                "probe-fallback" or "probe-timeout" => ["probe:sass", "probe:dart-sass", "compile:dart-sass"],
                "probe-rejected" => ["probe:sass", "probe:dart-sass"],
                _ => ["probe:sass", "compile:sass"]
            };
            Assert.Equal(expectedCalls, File.ReadAllLines(callsPath));
            var starts = logger.Debugs.Where(message => message.StartsWith("event=scss.tool.start ", StringComparison.Ordinal)).ToArray();
            var ends = logger.Debugs.Where(message => message.StartsWith("event=scss.tool.end ", StringComparison.Ordinal)).ToArray();
            Assert.Equal(expectedCalls.Length, starts.Length);
            Assert.Equal(expectedCalls.Length, ends.Length);
            var attemptIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < ends.Length; i++)
            {
                var fields = ends[i].Split(' ').Select(part => part.Split('=', 2)).ToDictionary(parts => parts[0], parts => parts[1]);
                Assert.True(Guid.TryParseExact(fields["attempt"], "N", out _));
                Assert.True(attemptIds.Add(fields["attempt"]));
                Assert.Equal(expectedCalls[i], fields["phase"] + ":" + fields["tool"]);
                Assert.Contains(starts, message => message.Contains($"attempt={fields["attempt"]} phase={fields["phase"]} tool={fields["tool"]} ", StringComparison.Ordinal)
                    && message.EndsWith(fields["phase"] == "probe" ? "timeout_ms=3000" : "timeout_ms=5000", StringComparison.Ordinal));
                Assert.True(long.TryParse(fields["elapsed_ms"], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var elapsed) && elapsed >= 0);
                var interrupted = fields["tool"] == "sass" && (scenario == fields["phase"] + "-timeout" || scenario == fields["phase"] + "-cancel");
                Assert.Equal(interrupted ? scenario.EndsWith("-timeout", StringComparison.Ordinal) ? "timeout" : "cancelled" : "exit", fields["outcome"]);
                if (interrupted)
                {
                    Assert.Equal("unavailable", fields["exit_code"]);
                    Assert.Equal("unavailable", fields["stderr"]);
                    Assert.Equal("unavailable", fields["stderr_chars"]);
                    Assert.NotEqual("none", fields["error"]);
                }
                else
                {
                    var nonzero = scenario == "probe-rejected" || (scenario == "probe-fallback" && fields["tool"] == "sass") || (scenario == "compile-failed" && fields["phase"] == "compile");
                    Assert.Equal(nonzero ? "7" : "0", fields["exit_code"]);
                    Assert.Equal(scenario == "output-missing" ? "empty" : "redacted", fields["stderr"]);
                    Assert.Equal(scenario == "output-missing" ? "0" : (privateStderr.Length + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), fields["stderr_chars"]);
                    Assert.Equal("none", fields["error"]);
                }
            }
            foreach (var message in starts.Concat(ends))
            {
                Assert.DoesNotContain(privateStderr, message, StringComparison.Ordinal);
                Assert.DoesNotContain(root, message, StringComparison.Ordinal);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            TestCleanup.DeleteDirectory(root);
        }
    }

    private sealed class ScssObservationLogger(bool throwOnDebug = false) : ILogger
    {
        public System.Collections.Concurrent.ConcurrentQueue<string> Debugs { get; } = new();
        public System.Collections.Concurrent.ConcurrentQueue<string> Warnings { get; } = new();
        public void Debug(string message)
        {
            Debugs.Enqueue(message);
            if (throwOnDebug) throw new InvalidOperationException("diagnostic sink failure");
        }
        public void Info(string message) { }
        public void Warn(string message) => Warnings.Enqueue(message);
        public void Error(string message) => throw new InvalidOperationException(message);
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

    private bool RequireUnix()
    {
        if (!OperatingSystem.IsWindows()) return true;
        output.WriteLine("BUKIT_NOT_APPLICABLE: POSIX process probe");
        return false;
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
