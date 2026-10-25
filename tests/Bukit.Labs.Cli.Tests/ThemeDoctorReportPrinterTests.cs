using Bukit.Labs.Cli.Commands;
using Bukit.Theme;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

[Collection("Console")]
public sealed class ThemeDoctorReportPrinterTests
{
    [Theory]
    [InlineData(true, true, "ERRORS FOUND")]
    [InlineData(false, true, "WARNINGS FOUND")]
    [InlineData(false, false, "ALL CLEAN")]
    public void Print_WritesExactTextAndPrioritizesErrors(bool errors, bool warnings, string summary)
    {
        var result = new ThemeDoctor.DoctorResult(
            HasErrors: errors,
            HasWarnings: warnings,
            Issues:
            [
                "✓ clean",
                "⚠ warning",
                "✗ error",
                "✘ other error",
                "◌ note"
            ]);
        using var writer = new StringWriter();
        TextWriter originalOut = Console.Out;

        try
        {
            Console.SetOut(writer);
            ThemeDoctorReportPrinter.Print(result);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        string expected = string.Join(
            Environment.NewLine,
            [
                "",
                "═══ Theme Doctor Report ═══",
                "",
                "  ✓ clean",
                "  ⚠ warning",
                "  ✗ error",
                "  ✘ other error",
                "  ◌ note",
                "",
                $"Summary: {summary}",
                ""
            ]);
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes(expected), System.Text.Encoding.UTF8.GetBytes(writer.ToString()));
    }
}
