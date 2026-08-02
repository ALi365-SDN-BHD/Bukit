using Bukit.Cli.Commands;
using Bukit.Cli.Shared.Cli.Rendering;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class HelpPrinterTests
{
    [Fact]
    public void Print_ProducesNonEmptyOutput()
    {
        var originalOut = Console.Out;
        var writer = new StringWriter();
        try
        {
            Console.SetOut(writer);
            HelpPrinter.Print();
            var output = writer.ToString();
            Assert.NotEmpty(output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Print_ContainsCommandList()
    {
        var originalOut = Console.Out;
        var writer = new StringWriter();
        try
        {
            Console.SetOut(writer);
            HelpPrinter.Print();
            var output = writer.ToString();
            Assert.Contains("bukit", output);
            Assert.Contains("seo", output, StringComparison.Ordinal);
            Assert.Contains("geo", output, StringComparison.Ordinal);
            Assert.Contains("publish", output, StringComparison.Ordinal);
            Assert.Contains("deploy", output, StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Print_ContainsHelpMessage()
    {
        var originalOut = Console.Out;
        var writer = new StringWriter();
        try
        {
            Console.SetOut(writer);
            HelpPrinter.Print();
            var output = writer.ToString();
            Assert.Contains("help", output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Print_UsesLiveReloadTermForDevCommand()
    {
        var originalOut = Console.Out;
        var writer = new StringWriter();
        try
        {
            Console.SetOut(writer);
            HelpPrinter.Print();
            var output = writer.ToString();

            Assert.Contains("LiveReload 实时预览开发服务器", output, StringComparison.Ordinal);
            Assert.DoesNotContain("HMR", output, StringComparison.Ordinal);
            Assert.DoesNotContain("Hot Module Replacement", output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Print_ExposesSeoInsightsAlongsideExistingSubcommands()
    {
        var originalOut = Console.Out;
        var writer = new StringWriter();
        try
        {
            Console.SetOut(writer);
            HelpPrinter.Print();
            var output = writer.ToString();

            Assert.Contains("(audit, diff, insights)", output, StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void SeoInsightsHelp_ExposesAllOfflineOptions()
    {
        var registry = BukitCliSpecs.CreateRegistry();
        var seo = registry.Resolve("seo")!;
        var insights = registry.ResolveSubcommand(seo, "insights")!;

        var output = CliHelpRenderer.Render(insights, "bukit seo insights");

        Assert.Contains("--dir", output, StringComparison.Ordinal);
        Assert.Contains("--routes", output, StringComparison.Ordinal);
        Assert.Contains("--observations", output, StringComparison.Ordinal);
        Assert.Contains("--rules", output, StringComparison.Ordinal);
        Assert.Contains("--out", output, StringComparison.Ordinal);
        Assert.Contains("--strict-join", output, StringComparison.Ordinal);
    }
}
