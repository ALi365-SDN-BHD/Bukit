using System.Security.Cryptography;
using System.Text.Json;
using Bukit.Config;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed partial class SiteEngineIntegrationTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/docs")]
    public async Task SrbizTask4_FixedClockReplaysAllPublicBytes(string baseUrl)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "bukit-core.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var fixture = Path.Combine(directory.FullName, "tests/Bukit.Engine.Tests/Fixtures/SrbizTask4");
        var root = Path.Combine(Path.GetTempPath(), "bukit-srbiz-clock-" + Guid.NewGuid().ToString("N"));
        var now = new DateTimeOffset(2040, 12, 31, 16, 0, 0, TimeSpan.Zero);
        try
        {
            using var provenance = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture, "provenance.json")));
            foreach (var entry in provenance.RootElement.GetProperty("files").EnumerateObject())
            {
                var source = Path.Combine(fixture, entry.Name);
                Assert.Equal(entry.Value.GetString(), Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(source))));
                var target = Path.Combine(root, entry.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target);
                File.SetLastWriteTimeUtc(target, DateTimeOffset.FromUnixTimeSeconds(1784937600).UtcDateTime);
            }
            var config = ConfigLoader.Load(Path.Combine(root, "sites/wp2-task4-trust-fixture/site.yaml"));
            var output = Path.Combine(root, config.Build.Output);
            async Task<BuildResult> Build(bool clean, bool incremental, DateTimeOffset instant)
                => await new SiteEngine(new TestLogger(), new DefaultContentProviderFactory(),
                    new DefaultSearchIndexBuilder(), null, new LifecycleClock(instant))
                    .BuildAsync(config, root, new ConfigOverrides { BaseUrl = baseUrl, Clean = clean, Incremental = incremental });

            Assert.False(Directory.Exists(Path.Combine(root, ".cache")));
            string[]? expected = null;
            foreach (var mode in new[] { "cold", "warm", "incremental" })
            {
                var result = await Build(mode != "incremental", mode != "cold", now);
                Assert.Equal(now, result.StartedAt);
                Assert.Equal(mode != "cold", result.Incremental.Enabled);
                if (mode == "incremental") Assert.True(result.Incremental.CacheHitCount > 0);
                var hashes = PublicHashes(output);
                Assert.Contains(hashes, hash => hash.StartsWith("404.html ", StringComparison.Ordinal));
                Assert.Contains(hashes, hash => hash.StartsWith("content/insights/valid-v2/index.html.json ", StringComparison.Ordinal));
                if (expected is null) expected = hashes;
                else Assert.Equal(expected, hashes);
                foreach (var html in Directory.EnumerateFiles(output, "index.html", SearchOption.AllDirectories))
                    Assert.Contains("<span data-copyright-year>2041</span>", File.ReadAllText(html));
                var prefix = baseUrl == "/" ? "" : baseUrl;
                var post = File.ReadAllText(Path.Combine(output, "insights/valid-v2/index.html"));
                Assert.Contains($"href=\"{prefix}/companies/task4-trust-company/\"", post);
                Assert.Contains("完整 v2 fixture 正文。", post);
                var company = File.ReadAllText(Path.Combine(output, "companies/task4-trust-company/index.html"));
                Assert.Contains($"href=\"{prefix}/insights/valid-v2/\"", company);
                Assert.Contains("核验日期</dt><dd>2026-07-25", company);
                WriteSrbizEvidence(baseUrl, mode, new { instant = now, copyrightYear = 2041,
                    result.Incremental, publicHashes = hashes });
            }

            // Only the injected clock changes; the real footer must cross the local year boundary.
            await Build(false, true, now.AddHours(-1));
            var beforeBoundaryHashes = PublicHashes(output);
            var beforeBoundary = File.ReadAllText(Path.Combine(output, "insights/valid-v2/index.html"));
            Assert.Contains("<span data-copyright-year>2040</span>", beforeBoundary);
            await Build(true, false, now.AddHours(-1));
            Assert.Equal(beforeBoundaryHashes, PublicHashes(output));
            WriteSrbizEvidence(baseUrl, "clock-control", new { instant = now, copyrightYear = 2041,
                beforeBoundary = now.AddHours(-1), beforeBoundaryCopyrightYear = 2040,
                publicHashes = beforeBoundaryHashes, footer = beforeBoundary,
                note = "Unmodified effective inputs: clock-only incremental output equals clean output." });
        }
        finally { CleanupDir(root); }
    }

    private static void WriteSrbizEvidence(string baseUrl, string mode, object evidence)
    {
        var destination = Environment.GetEnvironmentVariable("BUKIT_SRBIZ_CLOCK_EVIDENCE");
        if (string.IsNullOrEmpty(destination)) return;
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(destination, (baseUrl == "/" ? "root" : "docs") + "-" + mode + ".json"),
            JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
    }
}
