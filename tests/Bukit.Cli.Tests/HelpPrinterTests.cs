using Bukit.Cli.Commands;
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

            Assert.Contains("(audit, diff, insights, question-insights)", output, StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task Main_SeoInsightsHelp_ResolvesLeafAndDisplaysRequiredDefaults()
    {
        var result = await InvokeEntryPointAsync(["seo", "insights", "--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("bukit seo insights", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("--observations <value>", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("--rules <value>", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("[required]", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("[default: dist]", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("[default: <dir>/.bukit/seo-route-map.json]", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("[default: <dir>/.bukit/seo-insights-report.json]", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
    }

    [Fact]
    public async Task Main_SeoAuditAndDiffHelp_RemainLeafSpecific()
    {
        var audit = await InvokeEntryPointAsync(["seo", "audit", "--help"]);
        var diff = await InvokeEntryPointAsync(["seo", "diff", "--help"]);

        Assert.Equal(0, audit.ExitCode);
        Assert.StartsWith("bukit seo audit", audit.StdOut, StringComparison.Ordinal);
        Assert.Contains("--report", audit.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("--observations", audit.StdOut, StringComparison.Ordinal);
        Assert.Empty(audit.StdErr);

        Assert.Equal(0, diff.ExitCode);
        Assert.StartsWith("bukit seo diff", diff.StdOut, StringComparison.Ordinal);
        Assert.Contains("--baseline", diff.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("--observations", diff.StdOut, StringComparison.Ordinal);
        Assert.Empty(diff.StdErr);
    }

    [Fact]
    public async Task Main_SeoTopLevelHelp_DoesNotMixInsightsLeafOptions()
    {
        var result = await InvokeEntryPointAsync(["seo", "--help"]);

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("bukit seo", result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("--observations", result.StdOut, StringComparison.Ordinal);
        Assert.DoesNotContain("--rules", result.StdOut, StringComparison.Ordinal);
        Assert.Empty(result.StdErr);
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> InvokeEntryPointAsync(string[] args)
    {
        var entryPoint = typeof(HelpPrinter).Assembly.EntryPoint
            ?? throw new InvalidOperationException("Missing Bukit.Cli entry point.");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var invocation = entryPoint.Invoke(null, [args]);
            var exitCode = invocation switch
            {
                Task<int> task => await task,
                int code => code,
                _ => throw new InvalidOperationException("Unsupported Bukit.Cli entry point return type.")
            };

            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }
}
