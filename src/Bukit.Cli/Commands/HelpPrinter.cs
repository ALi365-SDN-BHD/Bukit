using Bukit.Cli.Cli.Rendering;

namespace Bukit.Cli.Commands;

public static class HelpPrinter
{
    public static void Print()
    {
        var registry = BukitCliSpecs.CreateRegistry();
        Console.WriteLine("bukit");
        Console.WriteLine();
        Console.WriteLine("Use `bukit <command> --help` for command-specific usage.");
    }
}
