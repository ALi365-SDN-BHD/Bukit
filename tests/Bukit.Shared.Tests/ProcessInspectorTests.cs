using System.Diagnostics;
using Bukit.Shared.Processes;
using Xunit;

namespace Bukit.Shared.Tests;

public sealed class ProcessInspectorTests
{
    [Fact]
    public void TryParseLinuxStat_HandlesParenthesesAndSpacesInComm()
    {
        var stat = "4242 (a (b) c) S 17 4242 4242 0 -1 4194560 100 0 0 0 7 3 0 0 20 0 1 0 98765 1000000 250 18446744073709551615";

        Assert.True(ProcessInspector.TryParseLinuxStat(stat, out var parsed));
        Assert.Equal(17, parsed.Ppid);
        Assert.Equal(4242, parsed.Pgrp);
        Assert.Equal(7, parsed.UtimeTicks);
        Assert.Equal(3, parsed.StimeTicks);
        Assert.Equal(98765, parsed.StartTimeTicks);
        Assert.Equal(250, parsed.RssPages);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12 (comm")]
    [InlineData("12 (comm) S 1 2")]
    [InlineData("12 (comm) S x 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20 21")]
    public void TryParseLinuxStat_RejectsMalformedText(string stat)
    {
        Assert.False(ProcessInspector.TryParseLinuxStat(stat, out _));
    }

    [Fact]
    public void TryGetIdentity_ReadsCurrentProcess()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.True(ProcessInspector.TryGetIdentity(Environment.ProcessId, out var self));
        Assert.Equal(Environment.ProcessId, self.Pid);
        Assert.True(self.ParentPid > 0);
        Assert.True(ProcessInspector.TryGetIdentity(Environment.ProcessId, out var again));
        Assert.Equal(self.StartTime, again.StartTime);
    }

    [Fact]
    public void TryGetIdentity_ChildPointsAtCurrentProcessAndStartsLater()
    {
        using var child = StartSleeper();
        try
        {
            Assert.True(ProcessInspector.TryGetIdentity(child.Id, out var identity));
            Assert.Equal(Environment.ProcessId, identity.ParentPid);
            Assert.True(ProcessInspector.TryGetIdentity(Environment.ProcessId, out var self));
            Assert.True(identity.StartTime >= self.StartTime);
        }
        finally
        {
            child.Kill(entireProcessTree: true);
            child.WaitForExit();
        }
    }

    [Fact]
    public void TryGetIdentity_ReturnsFalseForMissingProcess()
    {
        Assert.False(ProcessInspector.TryGetIdentity(0, out _));
        Assert.False(ProcessInspector.TryGetIdentity(-5, out _));
    }

    [Fact]
    public void TryGetExecutablePath_MatchesRealPathOfProcessPath()
    {
        var expected = ProcessInspector.TryRealPath(Environment.ProcessPath!);

        var actual = ProcessInspector.TryGetExecutablePath(Environment.ProcessId);

        Assert.NotNull(actual);
        Assert.Equal(expected, actual, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    }

    [Fact]
    public void TryGetArguments_ReadsChildArgumentVector()
    {
        using var child = StartSleeper();
        try
        {
            var args = ProcessInspector.TryGetArguments(child.Id);

            Assert.NotNull(args);
            Assert.Contains(args, a => a.Contains("bukit inspector probe", StringComparison.Ordinal));
        }
        finally
        {
            child.Kill(entireProcessTree: true);
            child.WaitForExit();
        }
    }

    [Fact]
    public void TryRealPath_ResolvesDirectoryAndFileSymlinks()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Directory.CreateTempSubdirectory("bukit-realpath-").FullName;
        try
        {
            var real = Directory.CreateDirectory(Path.Combine(root, "real")).FullName;
            File.WriteAllText(Path.Combine(real, "file"), "x");
            Directory.CreateSymbolicLink(Path.Combine(root, "dirlink"), "real");
            File.CreateSymbolicLink(Path.Combine(real, "filelink"), "file");

            var resolved = ProcessInspector.TryRealPath(Path.Combine(root, "dirlink", "filelink"));

            Assert.Equal(ProcessInspector.TryRealPath(Path.Combine(real, "file")), resolved);
            Assert.DoesNotContain("link", resolved, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryRealPath_ReturnsNullForSymlinkLoop()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Directory.CreateTempSubdirectory("bukit-realpath-").FullName;
        try
        {
            File.CreateSymbolicLink(Path.Combine(root, "a"), "b");
            File.CreateSymbolicLink(Path.Combine(root, "b"), "a");

            Assert.Null(ProcessInspector.TryRealPath(Path.Combine(root, "a")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryGetCurrentUserId_ReadsUserOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.False(ProcessInspector.TryGetCurrentUserId(out _));
            return;
        }

        Assert.True(ProcessInspector.TryGetCurrentUserId(out var uid));
        Assert.Equal(uid, ProcessInspector.TryGetCurrentUserId(out var again) ? again : uint.MaxValue);
    }

    private static Process StartSleeper()
    {
        var psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("powershell", ["-NoProfile", "-Command", "Start-Sleep 30; 'bukit inspector probe'"])
            : new ProcessStartInfo("/bin/sh", ["-c", "sleep 30; echo 'bukit inspector probe'"]);
        psi.UseShellExecute = false;
        return Process.Start(psi) ?? throw new InvalidOperationException("Failed to start probe process.");
    }
}
