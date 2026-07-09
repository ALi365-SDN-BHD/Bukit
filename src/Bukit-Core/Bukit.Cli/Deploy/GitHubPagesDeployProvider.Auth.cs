namespace Bukit.Cli.Deploy;

public sealed partial class GitHubPagesDeployProvider
{
    private const string AskpassTokenEnvironmentVariable = "BUKIT_GITHUB_TOKEN";

    private static string CreateAskpassScript(string tempDir, string token)
    {
        if (OperatingSystem.IsWindows())
        {
            var scriptPath = Path.Combine(tempDir, "git-askpass.bat");
            File.WriteAllText(scriptPath, $$"""
                @echo off
                powershell -NoProfile -ExecutionPolicy Bypass -Command "[Console]::Out.WriteLine($env:{{AskpassTokenEnvironmentVariable}})"
                """);
            return scriptPath;
        }

        var unixPath = Path.Combine(tempDir, "git-askpass");
        File.WriteAllText(unixPath, "#!/bin/sh\nprintf '%s\\n' \"${" + AskpassTokenEnvironmentVariable + ":-}\"\n");
        try
        {
            File.SetUnixFileMode(unixPath, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }
        catch (Exception modeEx)
        {
            Console.Error.WriteLine($"Deploy: failed to set askpass file mode: {modeEx.GetType().Name}");
        }

        return unixPath;
    }

    private static void CleanupAskpassScript(string? scriptPath)
    {
        if (scriptPath is null)
        {
            return;
        }

        try
        {
            File.Delete(scriptPath);
        }
        catch (Exception delEx)
        {
            Console.Error.WriteLine($"Deploy: failed to clean up askpass script: {delEx.GetType().Name}");
        }
    }
}
