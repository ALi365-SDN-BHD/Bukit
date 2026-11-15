using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Bukit.Shared.Processes;

/// <summary>Fields of <c>/proc/&lt;pid&gt;/stat</c> after <c>comm</c> that Bukit reads.</summary>
internal readonly record struct LinuxProcStat(
    int Ppid,
    int Pgrp,
    long UtimeTicks,
    long StimeTicks,
    long StartTimeTicks,
    long RssPages);

/// <summary>
/// A process, its parent and its start time. <see cref="StartTime"/> is an opaque,
/// platform-specific value: only equality and ordering between processes on the same
/// machine are meaningful (Linux clock ticks since boot, macOS microseconds since the
/// epoch, Windows FILETIME).
/// </summary>
internal readonly record struct ProcessIdentity(int Pid, int ParentPid, long StartTime);

/// <summary>
/// Read-only queries about other processes of the current user: parent, start time,
/// executable path and arguments. Every query fails soft (returns false or null) when the
/// process is gone or inaccessible; callers treat that as "unknown" and fail closed.
/// </summary>
internal static partial class ProcessInspector
{
    private const int MaxSymlinkDepth = 40;

    /// <summary>Parses the text of <c>/proc/&lt;pid&gt;/stat</c>; <c>comm</c> may contain spaces and parentheses.</summary>
    internal static bool TryParseLinuxStat(string stat, out LinuxProcStat result)
    {
        result = default;
        var closeParen = stat.LastIndexOf(')');
        if (closeParen < 0 || closeParen + 2 >= stat.Length)
        {
            return false;
        }

        var fields = stat[(closeParen + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // Fields after comm: state(0) ppid(1) pgrp(2) ... utime(11) stime(12) ... starttime(19) vsize(20) rss(21)
        if (fields.Length < 22)
        {
            return false;
        }

        if (!int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ppid) ||
            !int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pgrp) ||
            !long.TryParse(fields[11], NumberStyles.Integer, CultureInfo.InvariantCulture, out var utime) ||
            !long.TryParse(fields[12], NumberStyles.Integer, CultureInfo.InvariantCulture, out var stime) ||
            !long.TryParse(fields[19], NumberStyles.Integer, CultureInfo.InvariantCulture, out var start) ||
            !long.TryParse(fields[21], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rss))
        {
            return false;
        }

        result = new LinuxProcStat(ppid, pgrp, utime, stime, start, rss);
        return true;
    }

    /// <summary>Reads <c>/proc/&lt;pid&gt;/stat</c> on Linux.</summary>
    internal static bool TryReadLinuxStat(int pid, out LinuxProcStat result)
    {
        result = default;
        try
        {
            return TryParseLinuxStat(File.ReadAllText($"/proc/{pid}/stat"), out result);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>The parent pid and start time of <paramref name="pid"/>.</summary>
    internal static bool TryGetIdentity(int pid, out ProcessIdentity identity)
    {
        identity = default;
        if (pid <= 0)
        {
            return false;
        }

        if (OperatingSystem.IsLinux())
        {
            if (!TryReadLinuxStat(pid, out var stat))
            {
                return false;
            }

            identity = new ProcessIdentity(pid, stat.Ppid, stat.StartTimeTicks);
            return true;
        }

        if (OperatingSystem.IsMacOS())
        {
            return Mac.TryGetIdentity(pid, out identity);
        }

        if (OperatingSystem.IsWindows())
        {
            return Windows.TryGetIdentity(pid, out identity);
        }

        return false;
    }

    /// <summary>The canonical path of the executable image of <paramref name="pid"/>.</summary>
    internal static string? TryGetExecutablePath(int pid)
    {
        if (pid <= 0)
        {
            return null;
        }

        if (OperatingSystem.IsLinux())
        {
            try
            {
                // The kernel resolves the link; a replaced binary shows " (deleted)" and stays unequal.
                return new FileInfo($"/proc/{pid}/exe").LinkTarget;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        var path = OperatingSystem.IsMacOS() ? Mac.TryGetExecutablePath(pid)
            : OperatingSystem.IsWindows() ? Windows.TryGetExecutablePath(pid)
            : null;
        return path is null ? null : TryRealPath(path);
    }

    /// <summary>The argument vector of <paramref name="pid"/>, including argv[0].</summary>
    internal static IReadOnlyList<string>? TryGetArguments(int pid)
    {
        if (pid <= 0)
        {
            return null;
        }

        if (OperatingSystem.IsLinux())
        {
            try
            {
                var raw = File.ReadAllBytes($"/proc/{pid}/cmdline");
                return SplitNulTerminated(raw, 0, raw.Length);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        if (OperatingSystem.IsMacOS())
        {
            return Mac.TryGetArguments(pid);
        }

        if (OperatingSystem.IsWindows())
        {
            return Windows.TryGetArguments(pid);
        }

        return null;
    }

    /// <summary>The effective user id of the current process on macOS and Linux.</summary>
    internal static bool TryGetCurrentUserId(out uint uid)
    {
        uid = 0;
        if (OperatingSystem.IsMacOS())
        {
            uid = Mac.GetEffectiveUserId();
            return true;
        }

        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        try
        {
            // "Uid:\treal\teffective\tsaved\tfs"; libc is not loadable by name on every distribution.
            foreach (var line in File.ReadLines("/proc/self/status"))
            {
                if (!line.StartsWith("Uid:", StringComparison.Ordinal))
                {
                    continue;
                }

                var fields = line[4..].Split('\t', StringSplitOptions.RemoveEmptyEntries);
                return fields.Length >= 2 &&
                       uint.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out uid);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return false;
    }

    /// <summary>
    /// The client process of a connected Windows named pipe server handle, and whether the
    /// client is on another machine (then its pid means nothing locally).
    /// </summary>
    internal static bool TryGetNamedPipeClient(nint pipeHandle, out int pid, out bool isRemote)
    {
        pid = 0;
        isRemote = true;
        return OperatingSystem.IsWindows() && Windows.TryGetNamedPipeClient(pipeHandle, out pid, out isRemote);
    }

    /// <summary>
    /// The absolute path of <paramref name="path"/> with every symbolic link resolved, or null
    /// when a component cannot be read or links loop.
    /// </summary>
    internal static string? TryRealPath(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var full = Path.GetFullPath(path);
                return File.ResolveLinkTarget(full, returnFinalTarget: true)?.FullName ?? full;
            }

            return ResolveUnix(path, 0);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static string? ResolveUnix(string path, int depth)
    {
        if (depth > MaxSymlinkDepth)
        {
            return null;
        }

        var current = "/";
        foreach (var part in Path.GetFullPath(path).Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Join(current, part);
            var link = new FileInfo(candidate).LinkTarget;
            if (link is null)
            {
                current = candidate;
                continue;
            }

            var target = Path.IsPathRooted(link) ? link : Path.Join(current, link);
            var resolved = ResolveUnix(target, depth + 1);
            if (resolved is null)
            {
                return null;
            }

            current = resolved;
        }

        return current;
    }

    private static List<string> SplitNulTerminated(byte[] raw, int offset, int end)
    {
        var result = new List<string>();
        var start = offset;
        for (var i = offset; i < end; i++)
        {
            if (raw[i] == 0)
            {
                result.Add(Encoding.UTF8.GetString(raw, start, i - start));
                start = i + 1;
            }
        }

        if (start < end)
        {
            result.Add(Encoding.UTF8.GetString(raw, start, end - start));
        }

        return result;
    }

    private static partial class Mac
    {
        private const int ProcPidTbsdInfo = 3;
        private const int ProcBsdInfoSize = 136;
        private const int PpidOffset = 16;
        private const int StartSecondsOffset = 120;
        private const int StartMicrosecondsOffset = 128;
        private const int ProcPidPathMax = 4096;
        private const int CtlKern = 1;
        private const int KernArgMax = 8;
        private const int KernProcArgs2 = 49;

        internal static unsafe bool TryGetIdentity(int pid, out ProcessIdentity identity)
        {
            identity = default;
            var info = stackalloc byte[ProcBsdInfoSize];
            if (proc_pidinfo(pid, ProcPidTbsdInfo, 0, info, ProcBsdInfoSize) != ProcBsdInfoSize)
            {
                return false;
            }

            var ppid = *(int*)(info + PpidOffset);
            var seconds = *(long*)(info + StartSecondsOffset);
            var micros = *(long*)(info + StartMicrosecondsOffset);
            identity = new ProcessIdentity(pid, ppid, checked((seconds * 1_000_000) + micros));
            return true;
        }

        internal static unsafe string? TryGetExecutablePath(int pid)
        {
            var buffer = stackalloc byte[ProcPidPathMax];
            var length = proc_pidpath(pid, buffer, ProcPidPathMax);
            return length <= 0 ? null : Encoding.UTF8.GetString(buffer, length);
        }

        internal static unsafe List<string>? TryGetArguments(int pid)
        {
            var argMaxMib = stackalloc int[] { CtlKern, KernArgMax };
            int argMax;
            var size = (nuint)sizeof(int);
            if (sysctl(argMaxMib, 2, &argMax, &size, null, 0) != 0 || argMax <= sizeof(int))
            {
                return null;
            }

            var buffer = new byte[argMax];
            var mib = stackalloc int[] { CtlKern, KernProcArgs2, pid };
            size = (nuint)argMax;
            fixed (byte* p = buffer)
            {
                if (sysctl(mib, 3, p, &size, null, 0) != 0)
                {
                    return null;
                }
            }

            // Layout: int argc, executable path, NUL padding, then argc NUL-terminated strings.
            var length = (int)size;
            if (length < sizeof(int))
            {
                return null;
            }

            var argc = BitConverter.ToInt32(buffer, 0);
            var index = sizeof(int);
            while (index < length && buffer[index] != 0)
            {
                index++;
            }

            while (index < length && buffer[index] == 0)
            {
                index++;
            }

            var all = SplitNulTerminated(buffer, index, length);
            return argc < 0 || argc > all.Count ? null : all.GetRange(0, argc);
        }

        internal static uint GetEffectiveUserId() => geteuid();

        [LibraryImport("libc")]
        private static partial uint geteuid();

        [LibraryImport("libc")]
        private static unsafe partial int proc_pidinfo(int pid, int flavor, ulong arg, void* buffer, int size);

        [LibraryImport("libc")]
        private static unsafe partial int proc_pidpath(int pid, byte* buffer, uint size);

        [LibraryImport("libc")]
        private static unsafe partial int sysctl(int* name, uint nameLength, void* oldValue, nuint* oldLength, void* newValue, nuint newLength);
    }

    private static partial class Windows
    {
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const uint Th32csSnapProcess = 0x2;
        private const int ProcessCommandLineInformation = 60;
        private const int ErrorPipeLocal = 229;
        private static readonly nint InvalidHandle = -1;

        internal static unsafe bool TryGetIdentity(int pid, out ProcessIdentity identity)
        {
            identity = default;
            if (!TryGetStartTime(pid, out var start))
            {
                return false;
            }

            var snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
            if (snapshot == InvalidHandle)
            {
                return false;
            }

            try
            {
                var entry = default(ProcessEntry32W);
                entry.Size = (uint)sizeof(ProcessEntry32W);
                for (var ok = Process32FirstW(snapshot, &entry); ok; ok = Process32NextW(snapshot, &entry))
                {
                    if (entry.ProcessId == (uint)pid)
                    {
                        identity = new ProcessIdentity(pid, (int)entry.ParentProcessId, start);
                        return true;
                    }
                }
            }
            finally
            {
                CloseHandle(snapshot);
            }

            return false;
        }

        internal static unsafe string? TryGetExecutablePath(int pid)
        {
            var handle = OpenProcess(ProcessQueryLimitedInformation, false, (uint)pid);
            if (handle == 0)
            {
                return null;
            }

            try
            {
                var buffer = stackalloc char[32_768];
                var length = 32_768u;
                return QueryFullProcessImageNameW(handle, 0, buffer, &length) ? new string(buffer, 0, (int)length) : null;
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        internal static unsafe string[]? TryGetArguments(int pid)
        {
            var handle = OpenProcess(ProcessQueryLimitedInformation, false, (uint)pid);
            if (handle == 0)
            {
                return null;
            }

            try
            {
                var buffer = new byte[65_536];
                fixed (byte* p = buffer)
                {
                    uint returned;
                    if (NtQueryInformationProcess(handle, ProcessCommandLineInformation, p, (uint)buffer.Length, &returned) != 0)
                    {
                        return null;
                    }

                    var unicode = (UnicodeString*)p;
                    var commandLine = new string(unicode->Buffer, 0, unicode->Length / sizeof(char));
                    return SplitCommandLine(commandLine);
                }
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        internal static unsafe bool TryGetNamedPipeClient(nint pipeHandle, out int pid, out bool isRemote)
        {
            pid = 0;
            isRemote = true;
            uint clientPid;
            if (!GetNamedPipeClientProcessId(pipeHandle, &clientPid))
            {
                return false;
            }

            // Fails with ERROR_PIPE_LOCAL for a client on this machine; success means a remote client.
            var name = stackalloc char[256];
            // Anything other than ERROR_PIPE_LOCAL (229) is treated as remote: fail closed.
            isRemote = GetNamedPipeClientComputerNameW(pipeHandle, name, 256 * sizeof(char)) ||
                       Marshal.GetLastPInvokeError() != ErrorPipeLocal;
            pid = (int)clientPid;
            return true;
        }

        private static unsafe string[]? SplitCommandLine(string commandLine)
        {
            int count;
            char** argv;
            fixed (char* text = commandLine)
            {
                argv = CommandLineToArgvW(text, &count);
            }

            if (argv is null)
            {
                return null;
            }

            try
            {
                var result = new string[count];
                for (var i = 0; i < count; i++)
                {
                    result[i] = new string(argv[i]);
                }

                return result;
            }
            finally
            {
                LocalFree(argv);
            }
        }

        private static unsafe bool TryGetStartTime(int pid, out long start)
        {
            start = 0;
            var handle = OpenProcess(ProcessQueryLimitedInformation, false, (uint)pid);
            if (handle == 0)
            {
                return false;
            }

            try
            {
                long creation, exit, kernel, user;
                if (!GetProcessTimes(handle, &creation, &exit, &kernel, &user))
                {
                    return false;
                }

                start = creation;
                return true;
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct ProcessEntry32W
        {
            public uint Size;
            public uint Usage;
            public uint ProcessId;
            public nuint DefaultHeapId;
            public uint ModuleId;
            public uint Threads;
            public uint ParentProcessId;
            public int PriorityClassBase;
            public uint Flags;
            public fixed char ExeFile[260];
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct UnicodeString
        {
            public ushort Length;
            public ushort MaximumLength;
            public char* Buffer;
        }

        [LibraryImport("kernel32.dll")]
        private static partial nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint pid);

        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool CloseHandle(nint handle);

        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static unsafe partial bool GetProcessTimes(nint handle, long* creation, long* exit, long* kernel, long* user);

        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static unsafe partial bool QueryFullProcessImageNameW(nint handle, uint flags, char* name, uint* size);

        [LibraryImport("kernel32.dll")]
        private static partial nint CreateToolhelp32Snapshot(uint flags, uint pid);

        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static unsafe partial bool Process32FirstW(nint snapshot, ProcessEntry32W* entry);

        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static unsafe partial bool Process32NextW(nint snapshot, ProcessEntry32W* entry);

        [LibraryImport("kernel32.dll")]
        private static unsafe partial nint LocalFree(void* memory);

        [LibraryImport("shell32.dll")]
        private static unsafe partial char** CommandLineToArgvW(char* commandLine, int* count);

        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static unsafe partial bool GetNamedPipeClientProcessId(nint pipe, uint* pid);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static unsafe partial bool GetNamedPipeClientComputerNameW(nint pipe, char* name, uint size);

        [LibraryImport("ntdll.dll")]
        private static unsafe partial int NtQueryInformationProcess(nint handle, int infoClass, void* info, uint length, uint* returned);
    }
}
