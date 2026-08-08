using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Bukit.PluginHost.ProcessTree;

/// <summary>
/// Creates the platform process-tree limiter. Windows uses a kill-on-close Job Object
/// with job accounting; Linux uses util-linux setsid and macOS uses a monitored shell
/// job to place the plugin in a dedicated process group so the whole group can be
/// sampled and terminated together.
/// Platforms that cannot prove tree control throw so the caller can fail closed with
/// <see cref="PluginHostErrorCodes.ResourceLimitUnsupported"/>.
/// </summary>
internal static class PlatformProcessTreeLimiter
{
    internal static bool IsSupported =>
        OperatingSystem.IsWindows() ||
        OperatingSystem.IsMacOS() ||
        (OperatingSystem.IsLinux() && TryResolveSetSidPath() is not null);

    internal static IProcessTreeLimiter Create()
    {
        if (OperatingSystem.IsWindows())
        {
            return WindowsJobProcessTreeLimiter.Create();
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return new UnixProcessGroupTreeLimiter();
        }

        throw new PlatformNotSupportedException(
            "Process-tree resource limits cannot be proven on this platform.");
    }

    /// <summary>
    /// Rewrites the start info so the child runs as the leader of its own process
    /// group on Linux (setsid) or as a monitored shell job on macOS. No-op on
    /// Windows: containment there is best-effort from the moment of
    /// <see cref="IProcessTreeLimiter.Attach"/>, not from process creation (see the
    /// Windows job attach window note on the limiter implementations).
    /// </summary>
    internal static void PrepareStartInfo(ProcessStartInfo startInfo)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        if (OperatingSystem.IsLinux())
        {
            PrepareSetSidStartInfo(
                startInfo,
                TryResolveSetSidPath()
                    ?? throw new PlatformNotSupportedException(
                        "Linux plugin process-tree isolation requires util-linux setsid at /usr/bin/setsid or /bin/setsid."));
            return;
        }

        var originalFileName = startInfo.FileName;
        var originalArguments = startInfo.ArgumentList.ToArray();
        startInfo.ArgumentList.Clear();
        startInfo.ArgumentList.Add("-c");
        // Monitor mode gives the foreground job its own process group (pgid == job pid),
        // which the limiter samples and terminates as a unit.
        startInfo.ArgumentList.Add("set -m; \"$@\"");
        startInfo.ArgumentList.Add("bukit-plugin-tree");
        startInfo.ArgumentList.Add(originalFileName);
        foreach (var argument in originalArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.FileName = "/bin/sh";
    }

    /// <summary>
    /// Runs the original executable directly under util-linux setsid. This avoids
    /// non-interactive shell job-control warnings and makes the process id the pgid.
    /// </summary>
    internal static void PrepareSetSidStartInfo(ProcessStartInfo startInfo, string setSidPath)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        ArgumentException.ThrowIfNullOrWhiteSpace(setSidPath);

        var originalFileName = startInfo.FileName;
        var originalArguments = startInfo.ArgumentList.ToArray();
        startInfo.ArgumentList.Clear();
        startInfo.ArgumentList.Add(originalFileName);
        foreach (var argument in originalArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.FileName = setSidPath;
    }

    private static string? TryResolveSetSidPath()
    {
        foreach (var candidate in new[] { "/usr/bin/setsid", "/bin/setsid" })
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}

internal sealed class UnixProcessGroupTreeLimiter : IProcessTreeLimiter
{
    private const int Sigkill = 9;

    private Process? _wrapperProcess;
    private int _groupLeaderPid;
    private long _peakAggregateMemoryBytes;

    public void Attach(Process process)
    {
        _wrapperProcess = process;
        // util-linux setsid execs the plugin in place, so Linux can capture the
        // stable process-group id without racing child enumeration.
        _groupLeaderPid = OperatingSystem.IsLinux() ? process.Id : 0;
        _peakAggregateMemoryBytes = 0;
        if (_groupLeaderPid <= 0)
        {
            ResolveGroupLeaderAtStartup();
        }
    }

    public ValueTask<ProcessTreeUsage> SampleAsync(CancellationToken cancellationToken)
    {
        var members = EnumerateGroupMembers();
        var cpuTicks = 0L;
        var aggregateMemory = 0L;
        foreach (var pid in members)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryReadProcessUsage(pid, out var cpu, out var memory))
            {
                cpuTicks += cpu;
                aggregateMemory = SaturatingAdd(aggregateMemory, memory);
            }
        }

        _peakAggregateMemoryBytes = Math.Max(_peakAggregateMemoryBytes, aggregateMemory);
        return ValueTask.FromResult(new ProcessTreeUsage(ToCpuTime(cpuTicks), _peakAggregateMemoryBytes));
    }

    public void Terminate()
    {
        var leaderPid = ResolveGroupLeader();
        if (leaderPid > 0)
        {
            // Negative pid targets the whole process group.
            Libc.kill(-leaderPid, Sigkill);
        }

        try
        {
            var wrapper = _wrapperProcess;
            if (wrapper is not null && !wrapper.HasExited)
            {
                wrapper.Kill();
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private int ResolveGroupLeader()
    {
        var leader = _groupLeaderPid;
        if (leader > 0)
        {
            return leader;
        }

        var wrapper = _wrapperProcess;
        if (wrapper is null)
        {
            return 0;
        }

        try
        {
            foreach (var pid in EnumerateChildren(wrapper.Id))
            {
                _groupLeaderPid = pid;
                return pid;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
        }

        return 0;
    }

    private void ResolveGroupLeaderAtStartup()
    {
        for (var attempt = 0; attempt < 100 && _groupLeaderPid <= 0; attempt++)
        {
            ResolveGroupLeader();
            if (_groupLeaderPid <= 0)
            {
                Thread.Sleep(1);
            }
        }
    }

    private static long SaturatingAdd(long left, long right)
    {
        if (right <= 0)
        {
            return left;
        }

        return left > long.MaxValue - right ? long.MaxValue : left + right;
    }

    private List<int> EnumerateGroupMembers()
    {
        var leaderPid = ResolveGroupLeader();
        if (leaderPid <= 0)
        {
            return [];
        }

        if (OperatingSystem.IsMacOS())
        {
            return EnumeratePgrpMembersMac(leaderPid);
        }

        return EnumeratePgrpMembersLinux(leaderPid);
    }

    private static List<int> EnumeratePgrpMembersMac(int pgid)
    {
        // PROC_PGRP_ONLY = 2
        var buffer = new int[1024];
        var count = Libc.proc_listpids(2, (uint)pgid, buffer, buffer.Length * sizeof(int));
        if (count <= 0)
        {
            return [];
        }

        var result = new List<int>(Math.Min(count, buffer.Length));
        for (var i = 0; i < Math.Min(count, buffer.Length); i++)
        {
            if (buffer[i] > 0)
            {
                result.Add(buffer[i]);
            }
        }

        return result;
    }

    private static List<int> EnumeratePgrpMembersLinux(int pgid)
    {
        var result = new List<int>();
        if (!Directory.Exists("/proc"))
        {
            return result;
        }

        foreach (var entry in Directory.EnumerateDirectories("/proc"))
        {
            var name = Path.GetFileName(entry);
            if (!int.TryParse(name, out var pid))
            {
                continue;
            }

            if (TryReadStatField(pid, out var stat) && stat.Pgrp == pgid)
            {
                result.Add(pid);
            }
        }

        return result;
    }

    private static List<int> EnumerateChildren(int parentPid)
    {
        var result = new List<int>();
        if (OperatingSystem.IsMacOS())
        {
            var buffer = new int[256];
            var count = Libc.proc_listchildpids(parentPid, buffer, buffer.Length * sizeof(int));
            for (var i = 0; i < Math.Max(0, Math.Min(count, buffer.Length)); i++)
            {
                if (buffer[i] > 0)
                {
                    result.Add(buffer[i]);
                }
            }

            return result;
        }

        if (!Directory.Exists("/proc"))
        {
            return result;
        }

        foreach (var entry in Directory.EnumerateDirectories("/proc"))
        {
            var name = Path.GetFileName(entry);
            if (!int.TryParse(name, out var pid))
            {
                continue;
            }

            if (TryReadStatField(pid, out var stat) && stat.Ppid == parentPid)
            {
                result.Add(pid);
            }
        }

        return result;
    }

    private readonly record struct StatUsage(int Ppid, int Pgrp, long UtimeTicks, long StimeTicks, long RssPages);

    private static bool TryReadStatField(int pid, out StatUsage usage)
    {
        usage = default;
        try
        {
            var stat = File.ReadAllText($"/proc/{pid}/stat");
            var closeParen = stat.LastIndexOf(')');
            if (closeParen < 0 || closeParen + 2 >= stat.Length)
            {
                return false;
            }

            var fields = stat[(closeParen + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // Fields after comm: state(0) ppid(1) pgrp(2) ... utime(11) stime(12) ... rss(21)
            if (fields.Length < 22)
            {
                return false;
            }

            usage = new StatUsage(
                int.Parse(fields[1], System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(fields[2], System.Globalization.CultureInfo.InvariantCulture),
                long.Parse(fields[11], System.Globalization.CultureInfo.InvariantCulture),
                long.Parse(fields[12], System.Globalization.CultureInfo.InvariantCulture),
                long.Parse(fields[21], System.Globalization.CultureInfo.InvariantCulture));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or OverflowException)
        {
            return false;
        }
    }

    private static bool TryReadProcessUsage(int pid, out long cpuTicks, out long memoryBytes)
    {
        cpuTicks = 0;
        memoryBytes = 0;
        if (OperatingSystem.IsMacOS())
        {
            var info = default(Libc.ProcTaskInfoRaw);
            var size = Marshal.SizeOf<Libc.ProcTaskInfoRaw>();
            // PROC_PIDTASKINFO = 4
            if (Libc.proc_pidinfo(pid, 4, 0, ref info, size) <= 0)
            {
                return false;
            }

            var nanos = (long)((long)(info.TotalUser + info.TotalSystem) * MachTimebase.NanosecondsPerTick);
            cpuTicks = nanos / 100; // convert ns to 100ns ticks used by ToCpuTime
            memoryBytes = (long)info.ResidentSize;
            return true;
        }

        if (TryReadStatField(pid, out var stat))
        {
            cpuTicks = stat.UtimeTicks + stat.StimeTicks;
            memoryBytes = stat.RssPages * LinuxConstants.PageSize;
            return true;
        }

        return false;
    }

    private static TimeSpan ToCpuTime(long ticks)
    {
        if (OperatingSystem.IsLinux())
        {
            // /proc stat times are in clock ticks (usually 100Hz); normalize via sysconf.
            var hz = LinuxConstants.ClockTicksPerSecond;
            if (hz > 0)
            {
                return TimeSpan.FromSeconds((double)ticks / hz);
            }
        }

        // macOS already converted to 100ns ticks.
        return TimeSpan.FromTicks(ticks);
    }

    private static class MachTimebase
    {
        public static readonly double NanosecondsPerTick = Resolve();

        private static double Resolve()
        {
            try
            {
                if (Libc.mach_timebase_info(out var info) == 0 && info.Denom > 0)
                {
                    return (double)info.Numer / info.Denom;
                }
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }

            return 1.0;
        }
    }

    private static class LinuxConstants
    {
        public static readonly long ClockTicksPerSecond = Sysconf(2);   // _SC_CLK_TCK
        public static readonly long PageSize = Sysconf(30);            // _SC_PAGESIZE

        private static long Sysconf(int name)
        {
            try
            {
                return Libc.sysconf(name);
            }
            catch (DllNotFoundException)
            {
                return name == 2 ? 100 : 4096;
            }
            catch (EntryPointNotFoundException)
            {
                return name == 2 ? 100 : 4096;
            }
        }
    }

    private static class Libc
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct ProcTaskInfoRaw
        {
            public ulong VirtualSize;
            public ulong ResidentSize;
            public ulong TotalUser;
            public ulong TotalSystem;
            public ulong ThreadsUser;
            public ulong ThreadsSystem;
            public int Policy;
            public int Faults;
            public int Pageins;
            public int CowFaults;
            public int MessagesSent;
            public int MessagesReceived;
            public int SyscallsMach;
            public int SyscallsUnix;
            public int SyscallsBsd;
            public uint Csw;
            public uint ThreadNum;
            public uint NumRunning;
            public uint Priority;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MachTimebaseInfoData
        {
            public uint Numer;
            public uint Denom;
        }

        [DllImport("libc", SetLastError = true)]
        public static extern int kill(int pid, int sig);

        [DllImport("libc")]
        public static extern int proc_listpids(uint type, uint arg, int[] buffer, int size);

        [DllImport("libc")]
        public static extern int proc_listchildpids(int ppid, int[] buffer, int size);

        [DllImport("libc")]
        public static extern int proc_pidinfo(int pid, int flavor, ulong arg, ref ProcTaskInfoRaw buffer, int size);

        [DllImport("libc")]
        public static extern int mach_timebase_info(out MachTimebaseInfoData info);

        [DllImport("libc")]
        public static extern long sysconf(int name);
    }
}

/// <summary>
/// Windows job-object tree limiter. Containment guarantee is precise as follows: the
/// job is created before launch and the plugin process is assigned to it immediately
/// after <c>Process.Start()</c> returns; from that moment every descendant created by
/// an in-job process joins the job automatically. Because .NET process launch cannot
/// assign the job before the first thread runs, a malicious or faulty plugin that
/// spawns a child inside the start-to-attach window can leave that child outside the
/// job. Closing containment fully would require suspended creation
/// (CreateProcess CREATE_SUSPENDED → assign → resume), which is not implemented.
/// </summary>
internal sealed class WindowsJobProcessTreeLimiter : IProcessTreeLimiter
{
    private IntPtr _jobHandle;

    private WindowsJobProcessTreeLimiter(IntPtr jobHandle)
    {
        _jobHandle = jobHandle;
    }

    internal static WindowsJobProcessTreeLimiter Create()
    {
        var jobHandle = Kernel32.CreateJobObjectW(IntPtr.Zero, null);
        if (jobHandle == IntPtr.Zero)
        {
            throw new PlatformNotSupportedException("Unable to create a Windows job object for process-tree limits.");
        }

        var info = new Kernel32.JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new Kernel32.JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = Kernel32.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            }
        };
        if (!Kernel32.SetInformationJobObject(
                jobHandle,
                Kernel32.JobObjectExtendedLimitInformation,
                ref info,
                Marshal.SizeOf<Kernel32.JOBOBJECT_EXTENDED_LIMIT_INFORMATION>()))
        {
            Kernel32.CloseHandle(jobHandle);
            throw new PlatformNotSupportedException("Unable to configure a Windows job object for process-tree limits.");
        }

        return new WindowsJobProcessTreeLimiter(jobHandle);
    }

    public void Attach(Process process)
    {
        if (_jobHandle == IntPtr.Zero)
        {
            throw new ObjectDisposedException(nameof(WindowsJobProcessTreeLimiter));
        }

        if (!Kernel32.AssignProcessToJobObject(_jobHandle, process.Handle))
        {
            throw new PlatformNotSupportedException(
                "Unable to assign the plugin process to the resource-limit job object.");
        }
    }

    public ValueTask<ProcessTreeUsage> SampleAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_jobHandle == IntPtr.Zero)
        {
            throw new ObjectDisposedException(nameof(WindowsJobProcessTreeLimiter));
        }

        Kernel32.JOBOBJECT_BASIC_ACCOUNTING_INFORMATION accounting;
        if (!Kernel32.QueryInformationJobObject(
                _jobHandle,
                Kernel32.JobObjectBasicAccountingInformation,
                out accounting,
                Marshal.SizeOf<Kernel32.JOBOBJECT_BASIC_ACCOUNTING_INFORMATION>(),
                out _))
        {
            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error(),
                "Unable to query Windows job CPU accounting.");
        }

        Kernel32.JOBOBJECT_EXTENDED_LIMIT_INFORMATION extended;
        if (!Kernel32.QueryInformationJobObject(
                _jobHandle,
                Kernel32.JobObjectExtendedLimitInformation,
                out extended,
                Marshal.SizeOf<Kernel32.JOBOBJECT_EXTENDED_LIMIT_INFORMATION>(),
                out _))
        {
            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error(),
                "Unable to query Windows job memory accounting.");
        }

        var cpu = TimeSpan.FromTicks(accounting.TotalKernelTime + accounting.TotalUserTime);
        var peakMemory = extended.PeakJobMemoryUsed.ToInt64();
        if (peakMemory < 0)
        {
            peakMemory = long.MaxValue;
        }
        return ValueTask.FromResult(new ProcessTreeUsage(cpu, peakMemory));
    }

    public void Terminate()
    {
        if (_jobHandle != IntPtr.Zero)
        {
            Kernel32.TerminateJobObject(_jobHandle, 1);
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_jobHandle != IntPtr.Zero)
        {
            Kernel32.CloseHandle(_jobHandle);
            _jobHandle = IntPtr.Zero;
        }

        return ValueTask.CompletedTask;
    }

    private static class Kernel32
    {
        internal const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
        internal const int JobObjectExtendedLimitInformation = 9;
        internal const int JobObjectBasicAccountingInformation = 1;

        [StructLayout(LayoutKind.Sequential)]
        internal struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public IntPtr MinimumWorkingSetSize;
            public IntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public IntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public IntPtr ProcessMemoryLimit;
            public IntPtr JobMemoryLimit;
            public IntPtr PeakProcessMemoryUsed;
            public IntPtr PeakJobMemoryUsed;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct JOBOBJECT_BASIC_ACCOUNTING_INFORMATION
        {
            public long TotalUserTime;
            public long TotalKernelTime;
            public long ThisPeriodTotalUserTime;
            public long ThisPeriodTotalKernelTime;
            public uint TotalProcessCount;
            public uint ActiveProcessCount;
            public uint TotalTerminatedProcessCount;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr CreateJobObjectW(IntPtr lpJobAttributes, string? lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetInformationJobObject(
            IntPtr hJob,
            int jobObjectInformationClass,
            ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION lpJobObjectInformation,
            int cbJobObjectInformationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool QueryInformationJobObject(
            IntPtr hJob,
            int jobObjectInformationClass,
            out JOBOBJECT_BASIC_ACCOUNTING_INFORMATION lpJobObjectInformation,
            int cbJobObjectInformationLength,
            out int lpReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool QueryInformationJobObject(
            IntPtr hJob,
            int jobObjectInformationClass,
            out JOBOBJECT_EXTENDED_LIMIT_INFORMATION lpJobObjectInformation,
            int cbJobObjectInformationLength,
            out int lpReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool TerminateJobObject(IntPtr hJob, uint uExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr hObject);
    }
}
