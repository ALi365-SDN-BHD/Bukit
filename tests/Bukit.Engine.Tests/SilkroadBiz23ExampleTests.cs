using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SilkroadBiz23ExampleTests
{
    [Fact]
    public async Task BuildAsync_SilkroadBiz23_UsesBuildTimeListData()
    {
        var repoRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repoRoot, "examples", "silkroad_biz23");
        var root = Path.Combine(Path.GetTempPath(), "bukit-silkroad-biz23-test", Guid.NewGuid().ToString("N"));

        try
        {
            CopyDirectory(sourceRoot, root);

            var config = ConfigLoader.Load(Path.Combine(root, "site.yaml"));
            var logger = new TestLogger();
            var engine = new SiteEngine(logger);

            await engine.BuildAsync(config, root, new ConfigOverrides(), CancellationToken.None);

            var home = ReadOutput(root, "index.html");
            Assert.Equal(3, CountOccurrences(home, "data-business-card"));
            Assert.Equal(3, CountOccurrences(home, "data-company-card"));
            Assert.Contains("RCEP Digital Trade Corridor Opens", home, StringComparison.Ordinal);
            Assert.Contains("Nusantara Logistics", home, StringComparison.Ordinal);
            Assert.Contains("data-category-tags", home, StringComparison.Ordinal);
            Assert.Contains("href=\"/assets/style.css\"", home, StringComparison.Ordinal);
            Assert.DoesNotContain("href=\"//assets/style.css\"", home, StringComparison.Ordinal);
            AssertNoFrontendPaginationScript(home);

            var insights = ReadOutput(root, "insights", "index.html");
            Assert.Equal(2, CountOccurrences(insights, "data-business-card"));
            Assert.Contains("RCEP Digital Trade Corridor Opens", insights, StringComparison.Ordinal);
            Assert.Contains("Malaysia Halal Export Forum 2026", insights, StringComparison.Ordinal);
            Assert.DoesNotContain("ASEAN Green Logistics Incentives", insights, StringComparison.Ordinal);
            Assert.Contains("data-pagination", insights, StringComparison.Ordinal);
            Assert.Contains("href=\"/insights/page/2/\"", insights, StringComparison.Ordinal);
            AssertNoFrontendPaginationScript(insights);

            var insightsPage2 = ReadOutput(root, "insights", "page", "2", "index.html");
            Assert.Contains("<title>Insights - Page 2 | Silkroad Biz23</title>", insightsPage2, StringComparison.Ordinal);
            Assert.Contains("Browse page 2 of Insights from Silkroad Biz23, showing item 3 of 3.", insightsPage2, StringComparison.Ordinal);
            Assert.Equal(1, CountOccurrences(insightsPage2, "data-business-card"));
            Assert.Contains("ASEAN Green Logistics Incentives", insightsPage2, StringComparison.Ordinal);
            AssertNoFrontendPaginationScript(insightsPage2);

            var companies = ReadOutput(root, "companies", "index.html");
            Assert.Equal(2, CountOccurrences(companies, "data-company-card"));
            Assert.Contains("Nusantara Logistics", companies, StringComparison.Ordinal);
            Assert.Contains("Penang BioFoods", companies, StringComparison.Ordinal);
            Assert.DoesNotContain("Johor Solar Components", companies, StringComparison.Ordinal);
            Assert.Contains("href=\"/companies/page/2/\"", companies, StringComparison.Ordinal);
            AssertNoFrontendPaginationScript(companies);

            var companyPage2 = ReadOutput(root, "companies", "page", "2", "index.html");
            Assert.Contains("<title>Companies - Page 2 | Silkroad Biz23</title>", companyPage2, StringComparison.Ordinal);
            Assert.Contains("Browse page 2 of Companies from Silkroad Biz23, showing item 3 of 3.", companyPage2, StringComparison.Ordinal);
            Assert.Equal(1, CountOccurrences(companyPage2, "data-company-card"));
            Assert.Contains("Johor Solar Components", companyPage2, StringComparison.Ordinal);
            Assert.DoesNotContain("Nusantara Logistics", companyPage2, StringComparison.Ordinal);
            Assert.Contains("data-pagination", companyPage2, StringComparison.Ordinal);
            Assert.Contains("href=\"/companies/\"", companyPage2, StringComparison.Ordinal);
            AssertNoFrontendPaginationScript(companyPage2);

            var category = ReadOutput(root, "insights", "category", "market-watch", "index.html");
            Assert.Equal(2, CountOccurrences(category, "data-business-card"));
            Assert.Contains("Market Watch", category, StringComparison.Ordinal);
            Assert.Contains("RCEP Digital Trade Corridor Opens", category, StringComparison.Ordinal);
            Assert.Contains("ASEAN Green Logistics Incentives", category, StringComparison.Ordinal);
            AssertNoFrontendPaginationScript(category);

            var categoryIndex = ReadOutput(root, "insights", "category", "index.html");
            Assert.Contains("data-section=\"category-index\"", categoryIndex, StringComparison.Ordinal);
            Assert.Contains("href=\"/insights/category/market-watch/\"", categoryIndex, StringComparison.Ordinal);
            Assert.Contains("2 insights", categoryIndex, StringComparison.Ordinal);
            Assert.Contains("href=\"/insights/category/policy-signals/\"", categoryIndex, StringComparison.Ordinal);
            Assert.Contains("1 insights", categoryIndex, StringComparison.Ordinal);
            AssertNoFrontendPaginationScript(categoryIndex);

            var policySignals = ReadOutput(root, "insights", "category", "policy-signals", "index.html");
            Assert.Equal(1, CountOccurrences(policySignals, "data-business-card"));
            Assert.Contains("Policy Signals", policySignals, StringComparison.Ordinal);
            Assert.Contains("Malaysia Halal Export Forum 2026", policySignals, StringComparison.Ordinal);
            Assert.DoesNotContain("RCEP Digital Trade Corridor Opens", policySignals, StringComparison.Ordinal);
            AssertNoFrontendPaginationScript(policySignals);

            Assert.Empty(logger.Errors);
        }
        finally
        {
            TestCleanup.DeleteDirectory(root, recursive: true);
        }
    }

    private static string ReadOutput(string root, params string[] path)
        => File.ReadAllText(Path.Combine(new[] { root, "dist" }.Concat(path).ToArray()));

    private static void AssertNoFrontendPaginationScript(string html)
    {
        Assert.DoesNotContain("<script src", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("type=\"module\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("type=\"text/javascript\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-js-pagination", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-js-filter", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("client-side pagination", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("client-side filter", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("querySelector", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("addEventListener", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".filter(", html, StringComparison.OrdinalIgnoreCase);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "examples")) &&
                Directory.Exists(Path.Combine(dir.FullName, "src")) &&
                Directory.Exists(Path.Combine(dir.FullName, "tests")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root from test runtime path.");
    }

    private sealed class TestLogger : ILogger
    {
        public List<string> Errors { get; } = new();

        public void Debug(string message)
        {
        }

        public void Info(string message)
        {
        }

        public void Warn(string message)
        {
        }

        public void Error(string message) => Errors.Add(message);
    }
}
