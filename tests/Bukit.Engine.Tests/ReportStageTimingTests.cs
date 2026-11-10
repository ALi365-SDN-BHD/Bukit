using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Bukit.Config;
using Bukit.Engine.Tests.Infrastructure;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed partial class ReportStageTimingTests
{
    private static readonly string[] VariantSteps = ["projections", "output-record", "seo-audit", "analytics-report", "total"];

    private static readonly string[] EnabledWriters =
    [
        "writer:BuildSummaryReportWriter",
        "writer:RoutesReportWriter",
        "writer:AssetsReportWriter",
        "writer:IncrementalManifestReportWriter",
        "writer:PublishUrlSnapshotReportWriter",
        "writer:ReleaseBundleChecksumsReportWriter",
        "writer:ArtifactManifestReportWriter",
        "writer:BuildManifestDigestReportWriter"
    ];

    private static readonly string[] DisabledWriters =
    [
        "writer:ReleaseBundleChecksumsReportWriter",
        "writer:ArtifactManifestReportWriter",
        "writer:BuildManifestDigestReportWriter"
    ];

    [Fact]
    public async Task SingleLanguageBuild_LogsVariantAndFinalizeStepsInOrder()
    {
        using var site = CreateSite();
        var logger = new RecordingLogger();

        await new SiteEngine(logger).BuildAsync(Configure(site.Config, multilingual: false, reports: true), site.Root,
            new ConfigOverrides { Incremental = false });

        var lines = Parse(logger);
        var variant = lines.Where(x => x.Scope == "variant").ToArray();
        Assert.Equal(VariantSteps, variant.Select(x => x.Step));
        Assert.All(variant, x => Assert.Null(x.Language));

        var finalize = lines.Where(x => x.Scope == "finalize").ToArray();
        Assert.Equal(
            ["collect-and-clean", "output-inventory", "security-data", "security-report", .. EnabledWriters, "security-gate", "total"],
            finalize.Select(x => x.Step));
        Assert.All(finalize, x => Assert.Null(x.Language));
        AssertStepsAddUpToTotal(variant);
        AssertStepsAddUpToTotal(finalize);
    }

    [Fact]
    public async Task MultiLanguageBuild_LogsEachVariantWithLanguageAndOneFinalizeScope()
    {
        using var site = CreateSite();
        var logger = new RecordingLogger();

        await new SiteEngine(logger).BuildAsync(Configure(site.Config, multilingual: true, reports: true), site.Root,
            new ConfigOverrides { Incremental = false });

        var lines = Parse(logger);
        foreach (var language in new[] { "en", "zh" })
        {
            var variant = lines.Where(x => x.Scope == "variant" && x.Language == language).ToArray();
            Assert.Equal(VariantSteps, variant.Select(x => x.Step));
            AssertStepsAddUpToTotal(variant);
        }

        Assert.DoesNotContain(lines, x => x.Scope == "variant" && x.Language is null);
        var finalize = lines.Where(x => x.Scope == "finalize").ToArray();
        Assert.Equal(
            ["collect-and-clean", "root-outputs", "seo-audit-merged", "output-inventory", "security-data", "security-report", .. EnabledWriters, "security-gate", "total"],
            finalize.Select(x => x.Step));
        Assert.All(finalize, x => Assert.Null(x.Language));
        AssertStepsAddUpToTotal(finalize);
    }

    [Fact]
    public async Task ReportsDisabled_LogsOnlyWritersThatRun()
    {
        using var site = CreateSite();
        var logger = new RecordingLogger();

        await new SiteEngine(logger).BuildAsync(Configure(site.Config, multilingual: false, reports: false), site.Root,
            new ConfigOverrides { Incremental = false });

        var finalize = Parse(logger).Where(x => x.Scope == "finalize").Select(x => x.Step).ToArray();
        Assert.Equal(
            ["collect-and-clean", "output-inventory", "security-data", "security-report", .. DisabledWriters, "security-gate", "total"],
            finalize);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MetricsRequested_LogsMetricsWriteInPathOrder(bool multilingual)
    {
        using var site = CreateSite();
        var logger = new RecordingLogger();
        var metricsPath = Path.Combine(site.Root, "metrics.json");

        await new SiteEngine(logger).BuildAsync(Configure(site.Config, multilingual, reports: true), site.Root,
            new ConfigOverrides { Incremental = false, MetricsPath = metricsPath });

        var finalize = Parse(logger).Where(x => x.Scope == "finalize").Select(x => x.Step).ToList();
        Assert.True(File.Exists(metricsPath));
        Assert.Single(finalize, step => step == "metrics-write");
        var previous = multilingual ? "seo-audit-merged" : null;
        var expectedIndex = previous is null ? 0 : finalize.IndexOf(previous) + 1;
        Assert.Equal(expectedIndex, finalize.IndexOf("metrics-write"));
    }

    [Fact]
    public async Task VariantStages_AccountForVariantTotal()
    {
        using var site = CreateSite();
        var metricsPath = Path.Combine(site.Root, "metrics.json");

        await new SiteEngine(new RecordingLogger()).BuildAsync(Configure(site.Config, multilingual: false, reports: true), site.Root,
            new ConfigOverrides { Incremental = false, MetricsPath = metricsPath });

        using var metrics = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(metricsPath));
        var durations = metrics.RootElement.GetProperty("variants")[0].GetProperty("stages").GetProperty("durationsMs")
            .EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetInt64());
        foreach (var stage in new[] { "variantSetup", "renderPlan", "assetPrepare", "seoPrepare", "renderDispatch" })
            Assert.True(durations.ContainsKey(stage), $"{stage} should be recorded");

        // Worker-cumulative render stages run inside renderDispatch and are not added again.
        string[] insideDispatch = ["pageRender", "listBuild", "staticRender", "renderCall", "htmlTransform", "renderWrite", "renderModelBind", "bodyLoad", "listBodyLoad"];
        var total = durations["variantTotal"];
        var sum = durations.Where(x => x.Key != "variantTotal" && !insideDispatch.Contains(x.Key)).Sum(x => x.Value);
        var tolerance = total < 500 ? 50 : total / 10;
        Assert.InRange(sum, total - tolerance, total + tolerance);
    }

    [Fact]
    public async Task FailingStep_PropagatesWithoutLoggingStepOrTotal()
    {
        var logger = new RecordingLogger();
        var timer = new ReportStageTimer(logger, ReportStageTimer.FinalizeScope);
        timer.Step("first", () => { });

        Assert.Throws<InvalidOperationException>(() => timer.Step("broken", () => throw new InvalidOperationException("boom")));
        await Assert.ThrowsAsync<IOException>(() => timer.StepAsync("broken-async", () => throw new IOException("boom")));

        Assert.Equal(["first"], Parse(logger).Select(x => x.Step));
    }

    private static void AssertStepsAddUpToTotal(IReadOnlyList<TimingLine> lines)
    {
        var total = lines.Single(x => x.Step == "total").DurationMs;
        var sum = lines.Where(x => x.Step != "total").Sum(x => x.DurationMs);
        Assert.All(lines, x => Assert.True(x.DurationMs >= 0));
        var tolerance = total < 500 ? 50 : total / 10;
        Assert.InRange(sum, total - tolerance, total + tolerance);
    }

    private static List<TimingLine> Parse(RecordingLogger logger) =>
        logger.Infos
            .Select(line => TimingPattern().Match(line))
            .Where(match => match.Success)
            .Select(match => new TimingLine(
                match.Groups["scope"].Value,
                match.Groups["language"].Success ? match.Groups["language"].Value : null,
                match.Groups["step"].Value,
                long.Parse(match.Groups["ms"].Value, System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();

    [GeneratedRegex(@"^event=build\.report\.timing scope=(?<scope>variant|finalize)(?: language=(?<language>\S+))? step=(?<step>\S+) duration_ms=(?<ms>\d+)$")]
    private static partial Regex TimingPattern();

    private sealed record TimingLine(string Scope, string? Language, string Step, long DurationMs);

    private static SiteBuilder CreateSite()
    {
        var site = new SiteBuilder();
        site.Markdown("hello", "Hello", "Body");
        site.Sandbox.Write("layouts/theme.yaml", """
            name: report-timing-test
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

    private static AppConfig Configure(AppConfig config, bool multilingual, bool reports) => config with
    {
        Site = config.Site with
        {
            Languages = multilingual ? ["en", "zh"] : null,
            DefaultLanguage = multilingual ? "en" : null
        },
        Build = config.Build with
        {
            LanguageJobs = multilingual ? 2 : 1,
            Report = config.Build.Report with { Enabled = reports }
        }
    };

    private sealed class RecordingLogger : ILogger
    {
        internal ConcurrentQueue<string> Infos { get; } = new();
        public void Debug(string message) { }
        public void Info(string message) => Infos.Enqueue(message);
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
