using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Bukit.Shared.IO;

internal enum PosixStatAbi
{
    LinuxX64,
    LinuxArm64,
    MacOsArm64,
    MacOsX64Inode64
}

/// <summary>
/// POSIX (Linux/macOS) and Windows no-follow source file opener.
/// POSIX: open(O_RDONLY | O_CLOEXEC | O_NOFOLLOW | O_NONBLOCK); target verified via
/// /proc/self/fd (Linux) or proc_pidfdinfo (macOS). Windows: CreateFileW
/// with FILE_FLAG_OPEN_REPARSE_POINT and GetFinalPathNameByHandleW.
/// Unsupported platforms fail closed.
/// </summary>
internal sealed partial class PlatformSafeSourceFileOpener : ISafeSourceFileOpener
{
    public VerifiedSourceFile Open(string path, string sourceRoot)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(sourceRoot);

        if (OperatingSystem.IsWindows())
        {
            return OpenWindows(path, sourceRoot);
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return OpenPosix(path, sourceRoot);
        }

        throw new PlatformNotSupportedException("Safe source file opening is unsupported on this platform.");
    }

    // ── POSIX ─────────────────────────────────────────────────────────────

    private const int O_RDONLY = 0x0000;

    // O_NOFOLLOW / O_CLOEXEC numeric values differ between macOS and Linux.
    // On macOS 0x100000 is O_DIRECTORY (not O_CLOEXEC); passing it by mistake
    // makes open() fail with ENOTDIR (errno 20) for regular files. Select the
    // correct per-platform values at runtime.
    private static readonly int PosixOpenFlags = SelectPosixOpenFlags(
        OperatingSystem.IsMacOS(),
        OperatingSystem.IsLinux(),
        RuntimeInformation.ProcessArchitecture);

    internal static int SelectPosixOpenFlags(
        bool isMacOs,
        bool isLinux,
        Architecture architecture)
    {
        if (isMacOs)
        {
            if (architecture is not Architecture.X64 and not Architecture.Arm64)
            {
                throw new PlatformNotSupportedException(
                    $"Safe source file opening does not support macOS architecture '{architecture}'.");
            }

            return O_RDONLY | MacOs.O_CLOEXEC | MacOs.O_NOFOLLOW | MacOs.O_NONBLOCK;
        }

        if (isLinux)
        {
            var noFollow = architecture switch
            {
                Architecture.X64 => Linux.O_NOFOLLOW_X64,
                Architecture.Arm64 => Linux.O_NOFOLLOW_ARM64,
                _ => throw new PlatformNotSupportedException(
                    $"Safe source file opening does not support Linux architecture '{architecture}'.")
            };
            return O_RDONLY | Linux.O_CLOEXEC | noFollow | Linux.O_NONBLOCK;
        }

        throw new PlatformNotSupportedException(
            $"Safe source file opening does not support the current POSIX platform architecture '{architecture}'.");
    }

    private static VerifiedSourceFile OpenPosix(string path, string sourceRoot)
    {
        var relativePath = Path.GetRelativePath(sourceRoot, path);
        if (Path.IsPathRooted(relativePath) ||
            relativePath.Equals("..", StringComparison.Ordinal) ||
            relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new IOException($"Source file '{path}' escapes source root '{sourceRoot}'.");
        }

        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw new IOException($"Source file '{path}' resolves to the source root itself.");
        }

        // Open the source root without following a symlink, verify that handle
        // still names the captured physical root, then walk every remaining
        // component with O_NOFOLLOW.
        var currentDirFd = NativeMethods.Open(sourceRoot, PosixOpenFlags);
        if (currentDirFd < 0)
        {
            throw new IOException(
                $"No-follow open failed for source root '{sourceRoot}' (errno {Marshal.GetLastPInvokeError()}).");
        }

        var fileFd = -1;
        try
        {
            var openedRoot = ResolvePosixHandlePath(currentDirFd);
            if (!PathComparer.Equals(ResolvePhysicalRoot(sourceRoot), openedRoot))
            {
                throw new IOException($"Already-open source root '{openedRoot}' no longer matches captured root '{sourceRoot}'.");
            }

            for (var index = 0; index < segments.Length; index++)
            {
                var nextFd = NativeMethods.OpenAt(currentDirFd, segments[index], PosixOpenFlags);
                NativeMethods.Close(currentDirFd);
                currentDirFd = -1;
                if (nextFd < 0)
                {
                    throw new IOException(
                        $"No-follow openat failed for '{segments[index]}' while opening '{path}' (errno {Marshal.GetLastPInvokeError()}).");
                }

                if (index == segments.Length - 1)
                {
                    fileFd = nextFd;
                }
                else
                {
                    currentDirFd = nextFd;
                }
            }

            EnsurePosixRegularFile(fileFd, path);
            var verifiedPath = ResolvePosixHandlePath(fileFd);
            if (!IsSameOrSubPathOf(openedRoot, verifiedPath))
            {
                throw new IOException($"Already-open target '{verifiedPath}' escapes source root '{openedRoot}'.");
            }

            var handle = new SafeFileHandle((IntPtr)fileFd, ownsHandle: true);
            fileFd = -1;
            var stream = new FileStream(handle, FileAccess.Read, bufferSize: 81920, isAsync: false);
            return new VerifiedSourceFile(handle, stream, verifiedPath);
        }
        finally
        {
            if (currentDirFd >= 0)
            {
                NativeMethods.Close(currentDirFd);
            }

            if (fileFd >= 0)
            {
                NativeMethods.Close(fileFd);
            }
        }
    }

    // ── Windows ───────────────────────────────────────────────────────────

    private const uint GENERIC_READ = 0x80000000;
    private const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    private const uint FILE_TYPE_DISK = 0x0001;
    private const uint OPEN_EXISTING = 3;

    private static VerifiedSourceFile OpenWindows(string path, string sourceRoot)
    {
        var handle = NativeMethods.CreateFile(
            path,
            GENERIC_READ,
            shareMode: 0x00000001, // FILE_SHARE_READ
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_FLAG_OPEN_REPARSE_POINT | FILE_FLAG_BACKUP_SEMANTICS,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastPInvokeError();
            throw new IOException($"CreateFileW no-follow open failed for '{path}' (error {error}).");
        }

        try
        {
            if (NativeMethods.GetFileType(handle) != FILE_TYPE_DISK)
            {
                throw new IOException($"Source file '{path}' is not a regular disk file.");
            }

            var attributes = File.GetAttributes(handle);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Source file '{path}' is a reparse point.");
            }

            if ((attributes & (FileAttributes.Directory | FileAttributes.Device)) != 0)
            {
                throw new IOException($"Source file '{path}' is not a regular file.");
            }

            var finalPath = ResolveWindowsHandlePath(handle, path);
            if (!IsSameOrSubPathOf(ResolvePhysicalRoot(sourceRoot), finalPath))
            {
                throw new IOException($"Already-open target '{finalPath}' escapes source root '{sourceRoot}'.");
            }

            var stream = new FileStream(handle, FileAccess.Read, bufferSize: 81920, isAsync: false);
            return new VerifiedSourceFile(handle, stream, finalPath);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    // ── Shared ────────────────────────────────────────────────────────────

    private static bool IsSameOrSubPathOf(string root, string candidate)
    {
        var relative = Path.GetRelativePath(root, candidate);
        return relative == "." ||
               (!Path.IsPathRooted(relative) &&
                !relative.Equals("..", StringComparison.Ordinal) &&
                !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
                !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal));
    }

    private static void EnsurePosixRegularFile(int fd, string path)
    {
        const int bufferSize = 512;
        const uint fileTypeMask = 0xF000;
        const uint regularFile = 0x8000;
        var abi = SelectPosixStatAbi(
            OperatingSystem.IsMacOS(),
            OperatingSystem.IsLinux(),
            RuntimeInformation.ProcessArchitecture);
        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            var result = abi switch
            {
                PosixStatAbi.MacOsX64Inode64 => NativeMethods.FStatMacOsX64Inode64(fd, buffer),
                PosixStatAbi.MacOsArm64 or PosixStatAbi.LinuxX64 or PosixStatAbi.LinuxArm64
                    => NativeMethods.FStat(fd, buffer),
                _ => throw new PlatformNotSupportedException($"Unsupported POSIX stat ABI '{abi}'.")
            };
            if (result != 0)
            {
                throw new IOException(
                    $"Cannot inspect already-open source file '{path}' (errno {Marshal.GetLastPInvokeError()}).");
            }

            var modeOffset = GetPosixStatModeOffset(abi);
            var mode = abi is PosixStatAbi.MacOsArm64 or PosixStatAbi.MacOsX64Inode64
                ? (uint)(ushort)Marshal.ReadInt16(buffer, modeOffset)
                : (uint)Marshal.ReadInt32(buffer, modeOffset);
            if ((mode & fileTypeMask) != regularFile)
            {
                throw new IOException($"Source file '{path}' is not a regular file.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    internal static PosixStatAbi SelectPosixStatAbi(
        bool isMacOs,
        bool isLinux,
        Architecture architecture)
    {
        if (isMacOs)
        {
            return architecture switch
            {
                Architecture.X64 => PosixStatAbi.MacOsX64Inode64,
                Architecture.Arm64 => PosixStatAbi.MacOsArm64,
                _ => throw new PlatformNotSupportedException(
                    $"Safe source file opening does not support macOS architecture '{architecture}'.")
            };
        }

        if (isLinux)
        {
            return architecture switch
            {
                Architecture.X64 => PosixStatAbi.LinuxX64,
                Architecture.Arm64 => PosixStatAbi.LinuxArm64,
                _ => throw new PlatformNotSupportedException(
                    $"Safe source file opening does not support Linux architecture '{architecture}'.")
            };
        }

        throw new PlatformNotSupportedException(
            $"Safe source file opening does not support the current POSIX platform architecture '{architecture}'.");
    }

    internal static int GetPosixStatModeOffset(PosixStatAbi abi)
        => abi switch
        {
            PosixStatAbi.MacOsArm64 or PosixStatAbi.MacOsX64Inode64 => 4,
            PosixStatAbi.LinuxX64 => 24,
            PosixStatAbi.LinuxArm64 => 16,
            _ => throw new PlatformNotSupportedException($"Unsupported POSIX stat ABI '{abi}'.")
        };

    private static string ResolvePosixHandlePath(int fd)
    {
        if (OperatingSystem.IsLinux())
        {
            var resolved = File.ResolveLinkTarget($"/proc/self/fd/{fd}", returnFinalTarget: true)?.FullName;
            if (string.IsNullOrWhiteSpace(resolved))
            {
                throw new IOException($"Cannot resolve already-open file descriptor {fd}.");
            }

            return Path.GetFullPath(resolved);
        }

        const int procPidFdVnodePathInfo = 2;
        const int pathOffset = 176;
        const int pathLength = 1024;
        const int bufferLength = pathOffset + pathLength;
        var buffer = Marshal.AllocHGlobal(bufferLength);
        try
        {
            var bytesWritten = NativeMethods.ProcPidFdInfo(
                Environment.ProcessId,
                fd,
                procPidFdVnodePathInfo,
                buffer,
                bufferLength);
            if (bytesWritten < bufferLength)
            {
                throw new IOException($"Cannot resolve already-open file descriptor {fd} (errno {Marshal.GetLastPInvokeError()}).");
            }

            var resolved = Marshal.PtrToStringUTF8(IntPtr.Add(buffer, pathOffset));
            if (string.IsNullOrWhiteSpace(resolved))
            {
                throw new IOException($"Cannot resolve already-open file descriptor {fd}.");
            }

            return Path.GetFullPath(resolved);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string ResolveWindowsHandlePath(SafeFileHandle handle, string path)
    {
        var buffer = new char[4096];
        uint length = NativeMethods.GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Length, 0);
        if (length == 0 || length >= buffer.Length)
        {
            throw new IOException($"Cannot resolve already-open target for '{path}'.");
        }

        var finalPath = new string(buffer, 0, (int)length);
        return finalPath.StartsWith(@"\\?\", StringComparison.Ordinal) ? finalPath[4..] : finalPath;
    }

    private static StringComparer PathComparer
        => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// Resolves reparse points along <paramref name="path"/> component-wise so a
    /// captured root such as macOS /var/folders can be compared against the
    /// already-open handle's physical path. Falls back to the lexical full path
    /// when resolution is unavailable; the handle comparison stays authoritative.
    /// </summary>
    private static string ResolvePhysicalRoot(string path)
        => ResolvePhysicalRoot(path, new HashSet<string>(PathComparer), remainingHops: 64) ?? Path.GetFullPath(path);

    private static string? ResolvePhysicalRoot(string path, HashSet<string> visitedLinks, int remainingHops)
    {
        try
        {
            if (remainingHops <= 0)
            {
                return null;
            }

            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                return null;
            }

            var segments = fullPath[root.Length..].Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
            var current = root;
            for (var index = 0; index < segments.Length; index++)
            {
                current = Path.Combine(current, segments[index]);
                var target = GetImmediateLinkTarget(current);
                if (target is null)
                {
                    continue;
                }

                var fullLink = Path.GetFullPath(current);
                var remainingPath = index + 1 < segments.Length
                    ? Path.Combine(segments[(index + 1)..])
                    : string.Empty;
                var resolutionState = fullLink + "\0" + remainingPath;
                if (!visitedLinks.Add(resolutionState))
                {
                    return null;
                }

                var targetPath = Path.IsPathRooted(target)
                    ? target
                    : Path.Combine(Path.GetDirectoryName(fullLink)!, target);
                if (remainingPath.Length > 0)
                {
                    targetPath = Path.Combine(targetPath, remainingPath);
                }

                return ResolvePhysicalRoot(targetPath, visitedLinks, remainingHops - 1);
            }

            return Path.GetFullPath(current);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetImmediateLinkTarget(string path)
    {
        try
        {
            FileSystemInfo info = Directory.Exists(path)
                ? new DirectoryInfo(path)
                : new FileInfo(path);
            return info.LinkTarget;
        }
        catch
        {
            return null;
        }
    }

    private static class MacOs
    {
        public const int O_NOFOLLOW = 0x0100;
        public const int O_CLOEXEC = 0x1000000;
        public const int O_NONBLOCK = 0x0004;
    }

    private static class Linux
    {
        public const int O_NOFOLLOW_X64 = 0x20000;
        public const int O_NOFOLLOW_ARM64 = 0x8000;
        public const int O_CLOEXEC = 0x80000;
        public const int O_NONBLOCK = 0x0800;
    }

    private static partial class NativeMethods
    {
        [LibraryImport("libc", EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int Open(string pathname, int flags);

        [LibraryImport("libc", EntryPoint = "openat", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int OpenAt(int dirFd, string pathname, int flags);

        [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
        internal static partial int Close(int fd);

        [LibraryImport("libc", EntryPoint = "fstat", SetLastError = true)]
        internal static partial int FStat(int fd, IntPtr buffer);

        [LibraryImport("libc", EntryPoint = "fstat$INODE64", SetLastError = true)]
        internal static partial int FStatMacOsX64Inode64(int fd, IntPtr buffer);

        [LibraryImport("libproc", EntryPoint = "proc_pidfdinfo", SetLastError = true)]
        internal static partial int ProcPidFdInfo(
            int processId,
            int fd,
            int flavor,
            IntPtr buffer,
            int bufferSize);

        [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [LibraryImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", SetLastError = true)]
        internal static partial uint GetFinalPathNameByHandle(
            SafeFileHandle handle,
            char[] buffer,
            uint bufferLength,
            uint flags);

        [LibraryImport("kernel32.dll", EntryPoint = "GetFileType", SetLastError = true)]
        internal static partial uint GetFileType(SafeFileHandle handle);
    }
}
