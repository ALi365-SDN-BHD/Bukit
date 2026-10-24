using Xunit.Abstractions;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Bukit.Shared.IO;
using Xunit;

namespace Bukit.Shared.Tests;

public sealed class PlatformSafeSourceFileOpenerTests(ITestOutputHelper output) : IDisposable
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
            Assert.Fail("Verification gap: no approved safe source opener for this platform.");
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
            Assert.Fail("Verification gap: no approved safe source opener for this platform.");
        }

        var root = CreateTempRoot();
        var target = Path.Combine(root, "target.txt");
        var link = Path.Combine(root, "link.txt");
        File.WriteAllText(target, "target");
        File.CreateSymbolicLink(link, target);

        Assert.Throws<IOException>(() =>
            new PlatformSafeSourceFileOpener().Open(link, root));
    }

    [Fact]
    public void Open_PathEscapingRoot_IsRejected()
    {
        if (!IsApprovedOpenerPlatform())
        {
            Assert.Fail("Verification gap: no approved safe source opener for this platform.");
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
            output.WriteLine("BUKIT_NOT_APPLICABLE: POSIX FIFO proof");
            return;
        }

        var root = CreateTempRoot();
        var fifo = Path.Combine(root, "media.pipe");
        const string mkfifoPath = "/usr/bin/mkfifo";
        if (!File.Exists(mkfifoPath))
        {
            Assert.Fail("Verification gap: /usr/bin/mkfifo is unavailable.");
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
                Assert.Fail($"Verification gap: mkfifo failed with exit code {process.ExitCode}.");
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

    [Fact]
    public void Open_NestedFile_VerifiesMetadataAndDisposesOwnedHandle()
    {
        var root = CreateTempRoot();
        var nested = Directory.CreateDirectory(Path.Combine(root, "nested")).FullName;
        var path = Path.Combine(nested, "page.md");
        File.WriteAllBytes(path, [1, 2, 3]);
        File.SetLastWriteTimeUtc(path, new DateTime(2026, 1, 2, 3, 4, 6, DateTimeKind.Utc));
        var expectedTime = File.GetLastWriteTimeUtc(path);

        using var verified = new PlatformSafeSourceFileOpener().Open(path, root);
        Assert.Equal(Path.GetFullPath(path), verified.VerifiedPath, ignoreCase: OperatingSystem.IsWindows());
        Assert.Equal(3, verified.Length);
        Assert.Equal(expectedTime, verified.LastWriteTimeUtc);
        Assert.Equal(1, verified.Stream.ReadByte());
        Assert.False(verified.Stream.CanWrite);
        verified.Dispose();

        Assert.True(verified.Handle.IsClosed);
        Assert.Throws<ObjectDisposedException>(() => verified.Stream.ReadByte());
        Assert.Throws<ObjectDisposedException>(() => verified.Length);
        Assert.Throws<ObjectDisposedException>(() => verified.LastWriteTimeUtc);
        verified.Dispose();
    }

    [Fact]
    public void VerifiedSourceFile_StalePath_ReadsMetadataAndContentFromOwnedHandle()
    {
        var root = CreateTempRoot();
        var original = Path.Combine(root, "original.txt");
        var replacement = Path.Combine(root, "replacement.txt");
        File.WriteAllBytes(original, [1, 2, 3]);
        File.WriteAllBytes(replacement, [9, 8, 7, 6, 5]);
        File.SetLastWriteTimeUtc(original, new DateTime(2025, 1, 2, 3, 4, 6, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(replacement, new DateTime(2026, 1, 2, 3, 4, 6, DateTimeKind.Utc));
        var originalTime = File.GetLastWriteTimeUtc(original);
        Assert.NotEqual(originalTime, File.GetLastWriteTimeUtc(replacement));

        using var stream = File.OpenRead(original);
        // Model a stale pathname without relying on OS-specific rename/sharing behavior.
        using var verified = new VerifiedSourceFile(stream.SafeFileHandle, stream, replacement);

        Assert.Equal(3, verified.Length);
        Assert.Equal(originalTime, verified.LastWriteTimeUtc);
        Assert.Equal(1, verified.Stream.ReadByte());
    }

    [Fact]
    public void Open_MissingFileOrDirectory_FailsClosed()
    {
        var root = CreateTempRoot();
        var directory = Directory.CreateDirectory(Path.Combine(root, "directory")).FullName;
        var opener = new PlatformSafeSourceFileOpener();

        Assert.Throws<IOException>(() => opener.Open(Path.Combine(root, "missing.txt"), root));
        Assert.Throws<IOException>(() => opener.Open(directory, root));
        Assert.Throws<IOException>(() => opener.Open(root, root));
    }

    [Fact]
    public void Open_ParentDirectoryLinkToOutside_FailsClosed()
    {
        var root = CreateTempRoot();
        var outside = CreateTempRoot();
        File.WriteAllText(Path.Combine(outside, "secret.txt"), "secret");
        var link = Path.Combine(root, "external");
        Directory.CreateSymbolicLink(link, outside);

        Assert.Throws<IOException>(() =>
            new PlatformSafeSourceFileOpener().Open(Path.Combine(link, "secret.txt"), root));
    }

    [Fact]
    public void Open_FileReplacedBySymlinkAfterEnumeration_FailsClosed()
    {
        var root = CreateTempRoot();
        var outside = CreateTempRoot();
        var path = Path.Combine(root, "page.md");
        var secret = Path.Combine(outside, "secret.md");
        File.WriteAllText(path, "page");
        File.WriteAllText(secret, "secret");
        var enumerated = Assert.Single(SafeFileEnumerator.EnumerateFiles(root));
        File.Delete(path);
        File.CreateSymbolicLink(path, secret);

        Assert.Throws<IOException>(() => new PlatformSafeSourceFileOpener().Open(enumerated, root));
    }

    [Fact]
    public void Open_NullPathOrRoot_RejectsInvalidArguments()
    {
        var opener = new PlatformSafeSourceFileOpener();
        var root = CreateTempRoot();

        Assert.Throws<ArgumentNullException>(() => opener.Open(null!, root));
        Assert.Throws<ArgumentNullException>(() => opener.Open(Path.Combine(root, "page.md"), null!));
    }

    [Theory]
    [InlineData(Architecture.Arm64)]
    [InlineData(Architecture.X64)]
    public void SelectPosixOpenFlags_MacOs_UsesNoFollowNonBlockingAndCloseOnExec(Architecture architecture)
    {
        Assert.Equal(0x1000104, PlatformSafeSourceFileOpener.SelectPosixOpenFlags(true, false, architecture));
    }

    [Theory]
    [InlineData(true, false, Architecture.X86)]
    [InlineData(false, true, Architecture.Arm)]
    [InlineData(false, false, Architecture.X64)]
    public void PosixSelectors_UnsupportedPlatformOrArchitecture_FailClosed(bool macOs, bool linux, Architecture architecture)
    {
        Assert.Throws<PlatformNotSupportedException>(() =>
            PlatformSafeSourceFileOpener.SelectPosixOpenFlags(macOs, linux, architecture));
        Assert.Throws<PlatformNotSupportedException>(() =>
            PlatformSafeSourceFileOpener.SelectPosixStatAbi(macOs, linux, architecture));
        Assert.Throws<PlatformNotSupportedException>(() =>
            PlatformSafeSourceFileOpener.GetPosixStatModeOffset((PosixStatAbi)(-1)));
    }
}
