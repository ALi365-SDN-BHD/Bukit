using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Bukit.Engine;

/// <summary>
/// Owns process-group/tree creation and termination for external tool invocations.
/// On Unix the tool runs as the leader of its own process group (monitored shell job)
/// so descendants can be terminated as a unit; on Windows the process tree kill is used.
/// Resource accounting is intentionally not duplicated here (PluginHost owns it).
/// </summary>
internal static partial class ExternalToolProcessTree
{
    private const int Sigkill = 9;

    /// <summary>
    /// Rewrites the start info so the tool runs as the leader of its own process group.
    /// Returns the path the wrapper uses to publish the group pgid, or null on Windows.
    /// </summary>
    internal static string? PrepareStartInfo(ProcessStartInfo startInfo)
    {
        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        var pgidPath = Path.Combine(Path.GetTempPath(), $"bukit-tool-pgid-{Guid.NewGuid():N}");
        var originalFileName = startInfo.FileName;
        var originalArguments = new List<string>();
        if (!string.IsNullOrWhiteSpace(startInfo.Arguments))
        {
            originalArguments.AddRange(TokenizeArguments(startInfo.Arguments));
            startInfo.Arguments = string.Empty;
        }

        originalArguments.AddRange(startInfo.ArgumentList);
        startInfo.ArgumentList.Clear();
        startInfo.ArgumentList.Add("-c");
        // Monitor mode gives the job its own process group (pgid == job pid). The job
        // publishes its pgid to a file so the group can be terminated even after the
        // wrapper and the job leader have exited. <&0 keeps the inherited stdin that
        // job control would otherwise redirect from /dev/null.
        // set +m before wait suppresses the shell's job-completion notification, which
        // would otherwise pollute the captured stderr stream.
        startInfo.ArgumentList.Add(
            $"set -m; \"$@\" <&0 & job=$!; printf \'%s\' \"$job\" > \'{pgidPath}\'; set +m; wait \"$job\"; exit \"$?\"");
        startInfo.ArgumentList.Add("bukit-tool-tree");
        startInfo.ArgumentList.Add(originalFileName);
        foreach (var argument in originalArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.FileName = "/bin/sh";
        return pgidPath;
    }

    /// <summary>
    /// Splits a ProcessStartInfo.Arguments string into individual arguments. Internal
    /// tool invocations use simple whitespace separation with optional double quotes.
    /// </summary>
    private static IEnumerable<string> TokenizeArguments(string arguments)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        var hasToken = false;
        foreach (var c in arguments)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                hasToken = true;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(c))
            {
                if (hasToken)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    hasToken = false;
                }

                continue;
            }

            current.Append(c);
            hasToken = true;
        }

        if (hasToken)
        {
            result.Add(current.ToString());
        }

        return result;
    }

    internal static void RemovePgidFile(string? pgidPath)
    {
        if (pgidPath is null)
        {
            return;
        }

        try
        {
            if (File.Exists(pgidPath))
            {
                File.Delete(pgidPath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Reads the process-group id published by the wrapper job. Falls back to child
    /// enumeration while the wrapper is still alive.
    /// </summary>
    internal static int ResolveGroupLeader(Process wrapperProcess, string? pgidPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return 0;
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (pgidPath is not null)
            {
                try
                {
                    if (File.Exists(pgidPath))
                    {
                        var text = File.ReadAllText(pgidPath).Trim();
                        if (int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var published) && published > 0)
                        {
                            return published;
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }

            var leaderPid = FirstChildPid(wrapperProcess.Id);
            if (leaderPid > 0)
            {
                return leaderPid;
            }

            try
            {
                if (wrapperProcess.HasExited && pgidPath is null)
                {
                    break;
                }
            }
            catch (InvalidOperationException)
            {
                break;
            }

            Thread.Sleep(5);
        }

        return 0;
    }

    internal static void Terminate(Process wrapperProcess, int groupLeaderPid)
    {
        try
        {
            if (!wrapperProcess.HasExited)
            {
                wrapperProcess.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }

        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var leaderPid = groupLeaderPid > 0 ? groupLeaderPid : FirstChildPid(wrapperProcess.Id);
        if (leaderPid > 0)
        {
            // Negative pid targets the entire process group of the tool job; this works
            // even when the leader already exited but descendants still hold the group.
            Libc.kill(-leaderPid, Sigkill);
        }
    }

    private static int FirstChildPid(int parentPid)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                var buffer = new int[64];
                var count = Libc.proc_listchildpids(parentPid, buffer, buffer.Length * sizeof(int));
                for (var i = 0; i < Math.Max(0, Math.Min(count, buffer.Length)); i++)
                {
                    if (buffer[i] > 0)
                    {
                        return buffer[i];
                    }
                }

                return 0;
            }

            if (OperatingSystem.IsLinux() && Directory.Exists("/proc"))
            {
                foreach (var entry in Directory.EnumerateDirectories("/proc"))
                {
                    var name = Path.GetFileName(entry);
                    if (!int.TryParse(name, out var pid))
                    {
                        continue;
                    }

                    try
                    {
                        var stat = File.ReadAllText($"/proc/{pid}/stat");
                        var closeParen = stat.LastIndexOf(')');
                        if (closeParen < 0)
                        {
                            continue;
                        }

                        var fields = stat[(closeParen + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (fields.Length > 1 &&
                            int.TryParse(fields[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var ppid) &&
                            ppid == parentPid)
                        {
                            return pid;
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
                    {
                    }
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
        }

        return 0;
    }

    private static partial class Libc
    {
        [LibraryImport("libc")]
        public static partial int kill(int pid, int sig);

        [LibraryImport("libc")]
        public static partial int proc_listchildpids(int ppid, int[] buffer, int size);
    }
}
