using System.Collections.Concurrent;
using System.Text.Json;
using Bukit.Config;
using Bukit.Engine.Tests.Infrastructure;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SiteEngineLoggerTests
{
    [Theory]
    [InlineData(false, "text", LogLevel.Warn)]
    [InlineData(false, "json", LogLevel.Error)]
    [InlineData(true, "json", LogLevel.Warn)]
    [InlineData(true, "json", LogLevel.Error)]
    public async Task MigratedVariantWarnings_UseCallerSinkAndCountOnceBeforeFiltering(bool multilingual, string format, LogLevel level)
    {
        using var site = CreateSite();
        var config = Configure(site.Config, multilingual);
        site.Sandbox.Write("outside.txt", "not published");
        string link = site.Sandbox.Link("static/link.txt", site.Sandbox.PathFor("outside.txt"), directory: false);
        using var stderr = new StringWriter();
        var original = Console.Error;
        var logger = new RecordingLogger(new ConsoleLogger(level, format));
        BuildResult result;
        try
        {
            Console.SetError(stderr);
            result = await new SiteEngine(logger).BuildAsync(config, site.Root, new ConfigOverrides { Incremental = false });
        }
        finally { Console.SetError(original); }
        int expected = multilingual ? 2 : 1;
        Assert.Equal(expected, logger.Warnings.Count);
        Assert.All(logger.Warnings, message => Assert.Equal("Skipping symlink: " + link, message));
        Assert.Equal(expected, result.Summary.WarningCount);
        Assert.Equal(0, result.Summary.ErrorCount);
        AssertReport(site, result);
        if (level == LogLevel.Error) Assert.Empty(stderr.ToString());
        else if (format == "text") Assert.Equal("[warn] Skipping symlink: " + link + Environment.NewLine, stderr.ToString());
        else
        {
            var lines = stderr.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(expected, lines.Length);
            foreach (var line in lines)
            {
                using var json = JsonDocument.Parse(line);
                Assert.Equal("Warn", json.RootElement.GetProperty("level").GetString());
                Assert.Equal("Skipping symlink: " + link, json.RootElement.GetProperty("msg").GetString());
            }
        }
        string page = Path.Combine(site.Output, multilingual ? "en/hello/index.html" : "hello/index.html");
        byte[] before = File.ReadAllBytes(page);
        File.Delete(link);
        logger.Warnings.Clear();
        var next = await new SiteEngine(logger).BuildAsync(config, site.Root, new ConfigOverrides { Incremental = false });
        Assert.Equal(0, next.Summary.WarningCount);
        Assert.Empty(logger.Warnings);
        Assert.Equal(before, File.ReadAllBytes(page));
        AssertReport(site, next);
    }

    [Fact]
    public async Task StartupOnlyWarning_IsNotBackfilledAndBuildCountersReset()
    {
        using var site = CreateSite();
        string path = site.Sandbox.PathFor("site.yaml");
        File.AppendAllText(path, "\nlogging:\n  level: error\n");
        File.WriteAllText(path, File.ReadAllText(path).Replace("build:", "build:\n  followSymlinks: true", StringComparison.Ordinal));
        var logger = new RecordingLogger(new ConsoleLogger(LogLevel.Error));
        var config = Configure(ConfigLoader.Load(path, logger), false);
        Assert.Single(logger.Warnings);
        var engine = new SiteEngine(logger);
        for (int i = 0; i < 2; i++)
        {
            var result = await engine.BuildAsync(config, site.Root, new ConfigOverrides { Incremental = false });
            Assert.Equal(0, result.Summary.WarningCount);
            AssertReport(site, result);
        }
        Assert.Single(logger.Warnings);
    }

    [Fact]
    public async Task PreTransactionManifestWarning_RemainsOutsideReportWhileLaterActualEventsCount()
    {
        using var site = CreateSite();
        site.Sandbox.Write(".cache/build-manifest.json", "{");
        Directory.CreateDirectory(site.Output);
        var logger = new RecordingLogger(new ConsoleLogger(LogLevel.Error));
        var result = await new SiteEngine(logger).BuildAsync(Configure(site.Config, false), site.Root,
            new ConfigOverrides { Incremental = false });
        Assert.True(logger.Warnings.Count > 1);
        Assert.All(logger.Warnings, message => Assert.StartsWith("Failed to load build manifest '", message));
        Assert.Equal(logger.Warnings.Count - 1, result.Summary.WarningCount);
        AssertReport(site, result);
    }

    private static SiteBuilder CreateSite()
    {
        var site = new SiteBuilder();
        site.Markdown("hello", "Hello", "Body");
        site.Sandbox.Write("layouts/theme.yaml", """
            name: logger-test
            version: 1.0.0
            engine: bukit
            templates:
              home:
                template: pages/index.html
                required: true
              page:
                template: pages/page.html
                accepts:
                  type: page
                  collection: page
              list:
                template: pages/list.html
                accepts:
                  kind: list
              taxonomyIndex:
                template: pages/taxonomy-index.html
                accepts:
                  kind: taxonomy-index
              taxonomyTerm:
                template: pages/taxonomy-term.html
                accepts:
                  kind: taxonomy-term
              pagination:
                template: pages/pagination.html
                accepts:
                  kind: pagination
              search:
                template: pages/search.html
                accepts:
                  kind: search
            """);
        foreach (var name in new[] { "list", "taxonomy-index", "taxonomy-term", "pagination", "search" })
            site.Sandbox.Write($"layouts/pages/{name}.html", "<html><head><title>Test</title></head><body>Test</body></html>");
        return site;
    }

    private static AppConfig Configure(AppConfig config, bool multilingual) => config with
    {
        Site = config.Site with
        {
            Seo = new SeoConfig { Enabled = false },
            Analytics = new AnalyticsConfig { Enabled = false },
            Languages = multilingual ? ["en", "zh"] : null,
            DefaultLanguage = multilingual ? "en" : null
        },
        Build = config.Build with { LanguageJobs = multilingual ? 2 : 1 }
    };

    private static void AssertReport(SiteBuilder site, BuildResult result)
    {
        using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(site.Output, ".bukit", "build-report.json")));
        var summary = report.RootElement.GetProperty("summary");
        Assert.Equal(result.Summary.WarningCount, summary.GetProperty("warningCount").GetInt32());
        Assert.Equal(result.Summary.ErrorCount, summary.GetProperty("errorCount").GetInt32());
        Assert.Equal("https://bukit.dev/schemas/build-report.v1.json", report.RootElement.GetProperty("schema").GetString());
    }

    private sealed class RecordingLogger(ILogger inner) : ILogger
    {
        internal ConcurrentQueue<string> Warnings { get; } = new();
        public void Debug(string message) => inner.Debug(message);
        public void Info(string message) => inner.Info(message);
        public void Warn(string message) { Warnings.Enqueue(message); inner.Warn(message); }
        public void Error(string message) => inner.Error(message);
    }
}
