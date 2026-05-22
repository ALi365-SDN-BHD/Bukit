using Bukit.Cli;

namespace Bukit.Cli.Commands;

public static class VersionCommand
{
    public static Task<int> RunAsync(ArgReader reader)
    {
        if (reader.HasFlag("--help") || reader.HasFlag("-h"))
        {
            Console.WriteLine("version — 显示 bukit 版本信息");
            Console.WriteLine();
            Console.WriteLine("Usage: bukit version");
            return Task.FromResult(0);
        }

        Console.WriteLine($"bukit {CliBuildInfo.Version}");
        Console.WriteLine("runtime: native-aot");
        return Task.FromResult(0);
    }
}
