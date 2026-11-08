using System.Collections.Concurrent;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class BuildDiagnosticLoggerSummaryTests
{
    [Theory]
    [InlineData("event=canonical.validation code=canonical_media_alt_missing field=x", "canonical.validation:canonical_media_alt_missing")]
    [InlineData("event=media.failed_reference url=x", "media.failed_reference")]
    [InlineData("seo.json_ld_missing route=/5/", "seo.json_ld_missing")]
    [InlineData("publish.audit severity=warning code=seo.description_too_long route=/5/", "publish.audit:seo.description_too_long")]
    [InlineData("Template directory hash limit exceeded; reuse disabled.", null)]
    [InlineData("", null)]
    public void WarningKind_GroupsByEventCodeOrRuleId(string message, string? expected)
        => Assert.Equal(expected, BuildDiagnosticLogger.WarningKind(message));

    [Fact]
    public void Warn_RepeatedKind_IsCappedAndSummarizedButFullyCounted()
    {
        var inner = new CaptureLogger();
        var logger = new BuildDiagnosticLogger(inner);
        for (var i = 0; i < 12; i++)
        {
            logger.Warn($"seo.json_ld_missing route=/{i}/");
        }

        logger.Warn("free-form warning one");
        logger.Warn("free-form warning one");
        logger.FlushWarningSummary();

        Assert.Equal(14, logger.WarningCount);
        Assert.Equal(BuildDiagnosticLogger.MaxWarningsPerKind, inner.Warnings.Count(w => w.StartsWith("seo.json_ld_missing", StringComparison.Ordinal)));
        Assert.Equal(2, inner.Warnings.Count(w => w == "free-form warning one"));
        Assert.Contains("event=diagnostics.summary kind=seo.json_ld_missing total=12 shown=5 suppressed=7", inner.Warnings);
    }

    [Fact]
    public void FlushWarningSummary_BelowCap_LogsNothingExtra()
    {
        var inner = new CaptureLogger();
        var logger = new BuildDiagnosticLogger(inner);
        logger.Warn("seo.json_ld_missing route=/1/");
        logger.FlushWarningSummary();
        Assert.Single(inner.Warnings);
    }

    private sealed class CaptureLogger : ILogger
    {
        public ConcurrentQueue<string> Warnings { get; } = new();
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) => Warnings.Enqueue(message);
        public void Error(string message) { }
    }
}
