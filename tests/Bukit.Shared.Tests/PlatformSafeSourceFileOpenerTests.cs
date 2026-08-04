using System.Diagnostics;
using System.Runtime.InteropServices;
using Bukit.Shared.IO;
using Xunit;
using Xunit.Sdk;

namespace Bukit.Shared.Tests;

public sealed class PlatformSafeSourceFileOpenerTests : IDisposable
{
    private readonly List<string> _tempRoots = new();

    public void Dispose()
    {
        foreach (var root in _tempRoots)
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private string CreateTempRoot()
    {
        // macOS /tmp and /var are symlinks; the opener validates the
        // already-open handle against the captured root, so tests must use
        // physical roots.
        var root = OperatingSystem.IsMacOS()
            ? Path.Combine("/private/tmp", "bukit-opener-tests", Guid.NewGuid().ToString("N"))
            : Path.Combine(Path.GetTempPath(), "bukit-opener-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        _tempRoots.Add(root);
        return root;
    }

    private static bool IsApprovedOpenerPlatform()
        => OperatingSystem.IsWindows() ||
           RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
           RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    [Fact]
    public void Open_RegularFile_ReadsVerifiedHandle()
    {
        if (!IsApprovedOpenerPlatform())
        {
            throw SkipException.ForSkip("The platform has no approved safe source opener.");
        }

        var root = CreateTempRoot();
        var sourceFile = Path.Combine(root, "regular.txt");
        File.WriteAllText(sourceFile, "verified");

        using var verified = new PlatformSafeSourceFileOpener().Open(sourceFile, root);
        using var reader = new StreamReader(
            verified.Stream,
            System.Text.Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024,
            leaveOpen: true);

        Assert.Equal("verified", reader.ReadToEnd());
        Assert.Equal(Path.GetFullPath(sourceFile), verified.VerifiedPath, ignoreCase: OperatingSystem.IsWindows());
        Assert.Equal("verified".Length, verified.Length);
    }

    [Fact]
    public void Open_FinalSymlink_IsRejected()
    {
        if (!IsApprovedOpenerPlatform())
        {
            throw SkipException.ForSkip("The platform has no approved safe source opener.");
        }

        var root = CreateTempRoot();
        var target = Path.Combine(root, "target.txt");
        var link = Path.Combine(root, "link.txt");
        File.WriteAllText(target, "target");
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            throw SkipException.ForSkip($"File symlinks are unavailable: {ex.GetType().Name}");
        }

        Assert.Throws<IOException>(() =>
            new PlatformSafeSourceFileOpener().Open(link, root));
    }

    [Fact]
    public void Open_PathEscapingRoot_IsRejected()
    {
        if (!IsApprovedOpenerPlatform())
        {
            throw SkipException.ForSkip("The platform has no approved safe source opener.");
        }

        var root = CreateTempRoot();
        var outside = Path.Combine(root, "outside-dir");
        Directory.CreateDirectory(outside);
        var outsideFile = Path.Combine(outside, "secret.txt");
        File.WriteAllText(outsideFile, "secret");
        var innerRoot = Path.Combine(root, "inner");
        Directory.CreateDirectory(innerRoot);

        Assert.Throws<IOException>(() =>
            new PlatformSafeSourceFileOpener().Open(outsideFile, innerRoot));
    }

    [Fact]
    public async Task Open_WhenTargetIsFifo_FailsClosedWithoutBlocking()
    {
        if (OperatingSystem.IsWindows())
        {
            throw SkipException.ForSkip("POSIX FIFO proof does not apply on Windows.");
        }

        var root = CreateTempRoot();
        var fifo = Path.Combine(root, "media.pipe");
        const string mkfifoPath = "/usr/bin/mkfifo";
        if (!File.Exists(mkfifoPath))
        {
            throw SkipException.ForSkip("/usr/bin/mkfifo is unavailable.");
        }

        using (var process = Process.Start(new ProcessStartInfo
               {
                   FileName = mkfifoPath,
                   UseShellExecute = false,
                   ArgumentList = { fifo }
               }) ?? throw new InvalidOperationException("Could not start mkfifo."))
        {
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
            {
                throw SkipException.ForSkip($"mkfifo failed with exit code {process.ExitCode}.");
            }
        }

        var openTask = Task.Run(() => Record.Exception(() =>
        {
            using var verified = new PlatformSafeSourceFileOpener().Open(fifo, root);
        }));
        var completed = await Task.WhenAny(openTask, Task.Delay(TimeSpan.FromMilliseconds(500)));
        if (completed != openTask)
        {
            var writerTask = Task.Run(() =>
            {
                using var writer = new FileStream(fifo, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                writer.WriteByte(1);
            });
            await openTask.WaitAsync(TimeSpan.FromSeconds(2));
            await writerTask.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Fail("Opening a FIFO blocked instead of failing closed from final-handle metadata.");
        }

        var exception = Assert.IsType<IOException>(await openTask);
        Assert.Contains("regular file", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectPosixStatAbi_SelectsDistinctMacOsFStatAbis()
    {
        var x64Abi = PlatformSafeSourceFileOpener.SelectPosixStatAbi(
            isMacOs: true,
            isLinux: false,
            Architecture.X64);
        var arm64Abi = PlatformSafeSourceFileOpener.SelectPosixStatAbi(
            isMacOs: true,
            isLinux: false,
            Architecture.Arm64);

        Assert.Equal(PosixStatAbi.MacOsX64Inode64, x64Abi);
        Assert.Equal(PosixStatAbi.MacOsArm64, arm64Abi);
        Assert.NotEqual(x64Abi, arm64Abi);
        Assert.Equal(4, PlatformSafeSourceFileOpener.GetPosixStatModeOffset(x64Abi));
        Assert.Equal(4, PlatformSafeSourceFileOpener.GetPosixStatModeOffset(arm64Abi));
    }

    [Fact]
    public void SelectPosixStatAbi_SelectsDistinctLinuxFStatAbis()
    {
        var x64Abi = PlatformSafeSourceFileOpener.SelectPosixStatAbi(
            isMacOs: false,
            isLinux: true,
            Architecture.X64);
        var arm64Abi = PlatformSafeSourceFileOpener.SelectPosixStatAbi(
            isMacOs: false,
            isLinux: true,
            Architecture.Arm64);

        Assert.NotEqual(x64Abi, arm64Abi);
        Assert.Equal(24, PlatformSafeSourceFileOpener.GetPosixStatModeOffset(x64Abi));
        Assert.Equal(16, PlatformSafeSourceFileOpener.GetPosixStatModeOffset(arm64Abi));
    }

    [Fact]
    public void SelectPosixOpenFlags_SelectsArchitectureSpecificLinuxOpenFlags()
    {
        var x64Flags = PlatformSafeSourceFileOpener.SelectPosixOpenFlags(
            isMacOs: false,
            isLinux: true,
            Architecture.X64);
        var arm64Flags = PlatformSafeSourceFileOpener.SelectPosixOpenFlags(
            isMacOs: false,
            isLinux: true,
            Architecture.Arm64);

        Assert.Equal(0xA0800, x64Flags);
        Assert.Equal(0x88800, arm64Flags);
    }
}
