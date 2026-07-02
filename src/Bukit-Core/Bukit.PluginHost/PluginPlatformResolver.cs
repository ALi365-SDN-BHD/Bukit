using System.Runtime.InteropServices;

namespace Bukit.PluginHost;

public sealed class PluginPlatformResolver : IPluginPlatformResolver
{
    public static IReadOnlyList<string> SupportedRuntimeIdentifiers { get; } =
    [
        "win-x64",
        "win-arm64",
        "linux-x64",
        "linux-arm64",
        "osx-x64",
        "osx-arm64"
    ];

    public string GetCurrentRid()
    {
        string os = OperatingSystem.IsWindows()
            ? "win"
            : OperatingSystem.IsLinux()
                ? "linux"
                : OperatingSystem.IsMacOS()
                    ? "osx"
                    : throw new PlatformNotSupportedException("Unsupported operating system for Bukit plugins.");

        string architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException("Unsupported processor architecture for Bukit plugins.")
        };

        return $"{os}-{architecture}";
    }
}
