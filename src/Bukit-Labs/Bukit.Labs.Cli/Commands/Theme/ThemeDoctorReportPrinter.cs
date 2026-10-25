using Bukit.Theme;

namespace Bukit.Labs.Cli.Commands;

internal static class ThemeDoctorReportPrinter
{
    internal static void Print(ThemeDoctor.DoctorResult result)
    {
        Console.WriteLine();
        Console.WriteLine("═══ Theme Doctor Report ═══");
        Console.WriteLine();

        foreach (var issue in result.Issues)
        {
            var color = issue.StartsWith("✗") || issue.StartsWith("✘") ? ConsoleColor.Red
                : issue.StartsWith("⚠") ? ConsoleColor.Yellow
                : issue.StartsWith("◌") ? ConsoleColor.DarkGray
                : ConsoleColor.Green;

            Console.ForegroundColor = color;
            Console.WriteLine($"  {issue}");
            Console.ResetColor();
        }

        Console.WriteLine();
        Console.Write("Summary: ");
        if (result.HasErrors)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERRORS FOUND");
        }
        else if (result.HasWarnings)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("WARNINGS FOUND");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("ALL CLEAN");
        }
        Console.ResetColor();
    }
}
