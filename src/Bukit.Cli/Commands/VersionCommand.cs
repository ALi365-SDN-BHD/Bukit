using Bukit.Cli;

namespace Bukit.Cli.Commands;

public static class VersionCommand
{
    public static Task<int> RunAsync(ArgReader reader)
    {
        Console.WriteLine($"bukit {CliBuildInfo.Version}");
#if AOT
        Console.WriteLine("runtime: native-aot");
#else
        Console.WriteLine("runtime: jit");
#endif
        return Task.FromResult(0);
    }
}
