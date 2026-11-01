using System.Diagnostics;
using System.Text;

namespace Bukit.Cli.Tests;

/// <summary>
/// Creates a test-only executable instead of routing Git arguments through cmd.exe.
/// The executable forwards fake-Git calls to PowerShell or maps deployment transport
/// to explicitly named local bare repositories before invoking the real Git binary.
/// </summary>
internal static class WindowsGitShim
{
    private static readonly Lazy<string> ShimDirectory = new(BuildShim);

    internal static void Install(string directory)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The Windows Git shim is Windows-only.");
        }

        var source = ShimDirectory.Value;
        File.Copy(Path.Combine(source, "BukitGitShim.exe"), Path.Combine(directory, "git.exe"));
        foreach (var name in new[] { "BukitGitShim.dll", "BukitGitShim.deps.json", "BukitGitShim.runtimeconfig.json" })
        {
            File.Copy(Path.Combine(source, name), Path.Combine(directory, name));
        }
    }

    private static string BuildShim()
    {
        // Build once per test process outside the repository, with all package sources
        // disabled. An executable preserves the original argv without cmd.exe parsing.
        var root = Path.Combine(Path.GetTempPath(), "bukit-windows-git-shim-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(root, "out");
        Directory.CreateDirectory(root);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        };

        File.WriteAllText(Path.Combine(root, "BukitGitShim.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <UseAppHost>true</UseAppHost>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(root, "NuGet.Config"), """
            <configuration><packageSources><clear /></packageSources></configuration>
            """);
        File.WriteAllText(Path.Combine(root, "Program.cs"), ShimSource, new UTF8Encoding(false));

        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in new[]
                 {
                     "build", Path.Combine(root, "BukitGitShim.csproj"), "-c", "Release", "-o", output,
                     "--nologo", "-p:RestoreConfigFile=" + Path.Combine(root, "NuGet.Config")
                 })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the Git shim build.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120_000))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException("The Git shim build timed out.");
        }

        Task.WaitAll(stdout, stderr);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"The Git shim build failed: {stdout.Result}\n{stderr.Result}");
        }

        return output;
    }

    private const string ShimSource = """
        using System.Diagnostics;

        var script = Environment.GetEnvironmentVariable("BUKIT_TEST_GIT_SCRIPT");
        if (!string.IsNullOrEmpty(script))
        {
            return await ForwardAsync("powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, .. args]);
        }

        var git = Environment.GetEnvironmentVariable("BUKIT_DEPLOY_REAL_GIT");
        if (string.IsNullOrEmpty(git) || args.Length == 0)
        {
            Console.Error.WriteLine("Missing real Git or command for the test transport shim.");
            return 91;
        }

        if (args[0] is "ls-remote" or "fetch" or "push")
        {
            var remoteNames = args.Where(arg => arg.StartsWith("bukit-deploy-", StringComparison.Ordinal)).ToArray();
            if (remoteNames.Length != 1)
            {
                Console.Error.WriteLine("Expected exactly one private deployment remote.");
                return 92;
            }

            var remote = remoteNames[0];
            var query = CreateProcess(git, ["config", "--local", "--get", $"remote.{remote}.url"]);
            using var queryProcess = Process.Start(query) ?? throw new InvalidOperationException("Could not start real Git.");
            var queryOutput = queryProcess.StandardOutput.ReadToEndAsync();
            var queryError = queryProcess.StandardError.ReadToEndAsync();
            await queryProcess.WaitForExitAsync();
            var url = (await queryOutput).Trim();
            var error = await queryError;
            if (queryProcess.ExitCode != 0)
            {
                Console.Error.Write(error);
                return queryProcess.ExitCode;
            }

            var targetVariable = url switch
            {
                "https://github.com/owner/repo-a.git" => "BUKIT_DEPLOY_BARE_A",
                "https://github.com/owner/repo-b.git" => "BUKIT_DEPLOY_BARE_B",
                "https://github.com/owner/repo-included.git" => "BUKIT_DEPLOY_BARE_B",
                "https://github.com/owner/repo-worktree.git" => "BUKIT_DEPLOY_BARE_B",
                "https://github.com/test/local.git" => "BUKIT_DEPLOY_BARE_B",
                _ => null
            };
            var target = targetVariable is null ? null : Environment.GetEnvironmentVariable(targetVariable);
            if (string.IsNullOrEmpty(target) || !Directory.Exists(target))
            {
                Console.Error.WriteLine("The test transport URL has no allowed local bare repository.");
                return 93;
            }

            var askpass = Environment.GetEnvironmentVariable("GIT_ASKPASS");
            if (string.IsNullOrEmpty(askpass) || !File.Exists(askpass))
            {
                Console.Error.WriteLine("The test transport did not receive a live askpass script.");
                return 94;
            }

            var askpassLog = Environment.GetEnvironmentVariable("BUKIT_DEPLOY_ASKPASS_LOG");
            if (!string.IsNullOrEmpty(askpassLog))
            {
                await File.WriteAllTextAsync(askpassLog, askpass);
            }

            var accessLog = Environment.GetEnvironmentVariable("BUKIT_DEPLOY_ACCESS_LOG");
            if (string.IsNullOrEmpty(accessLog))
            {
                Console.Error.WriteLine("The test transport requires an access log.");
                return 95;
            }

            var line = Environment.GetEnvironmentVariable("BUKIT_DEPLOY_SIMPLE_LOG") == "1"
                ? $"ACCESS {target}"
                : $"ACCESS {url}|{target}|{string.Join(' ', args)}";
            await File.AppendAllTextAsync(accessLog, line + Environment.NewLine);
            if (args[0] == "push")
            {
                var behavior = Environment.GetEnvironmentVariable("BUKIT_DEPLOY_PUSH_BEHAVIOR");
                if (behavior == "reject")
                {
                    Console.Error.WriteLine("fixture push rejected");
                    return 97;
                }
                if (behavior == "wait")
                {
                    await File.WriteAllTextAsync(Environment.GetEnvironmentVariable("BUKIT_DEPLOY_PUSH_MARKER")!, "started");
                    await Task.Delay(TimeSpan.FromSeconds(60));
                    return 98;
                }
            }
            return await ForwardAsync(git, args.Select(arg => arg == remote ? target : arg).ToArray(), fileOnly: true);
        }

        if (args[0] is not ("config" or "init" or "remote" or "checkout" or "add" or "commit"))
        {
            Console.Error.WriteLine("Unexpected Git command in the test transport shim.");
            return 96;
        }

        if (args[0] == "remote" && (args.Length != 4 || args[1] != "add"))
        {
            Console.Error.WriteLine("Unexpected remote operation in the test transport shim.");
            return 96;
        }

        return await ForwardAsync(git, args);

        static ProcessStartInfo CreateProcess(string file, IEnumerable<string> arguments)
        {
            var start = new ProcessStartInfo(file)
            {
                WorkingDirectory = Environment.CurrentDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }
            return start;
        }

        static async Task<int> ForwardAsync(string file, IEnumerable<string> arguments, bool fileOnly = false)
        {
            var start = CreateProcess(file, arguments);
            if (fileOnly)
            {
                start.Environment["GIT_ALLOW_PROTOCOL"] = "file";
            }
            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("Could not start the test Git child process.");
            var stdout = process.StandardOutput.BaseStream.CopyToAsync(Console.OpenStandardOutput());
            var stderr = process.StandardError.BaseStream.CopyToAsync(Console.OpenStandardError());
            await process.WaitForExitAsync();
            await Task.WhenAll(stdout, stderr);
            return process.ExitCode;
        }
        """;
}
