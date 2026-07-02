using Bukit.Cli.Shared.Cli.Rendering;

namespace Bukit.Cli.Commands;

public static class HelpPrinter
{
    public static void Print()
    {
        var registry = BukitCliSpecs.CreateRegistry();
        Console.WriteLine("bukit — static site generator");
        Console.WriteLine();
        Console.WriteLine("Usage: bukit <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        foreach (var spec in registry.Commands.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
        {
            var subs = spec.Subcommands is { Count: > 0 }
                ? $" ({string.Join(", ", spec.Subcommands.Select(sc => sc.Name))})"
                : "";
            Console.WriteLine($"  {spec.Name,-12} {spec.Description}{subs}");
        }
        Console.WriteLine();
        Console.WriteLine("Use `bukit <command> --help` for command-specific usage.");
    }
}
