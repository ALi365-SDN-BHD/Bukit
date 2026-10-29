using System.Text.Json;
using Bukit.Engine.Tests.Infrastructure;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class Stage7GoldenTests
{
    [Theory, Trait("Category", "Integration")]
    [InlineData("basic-markdown-site")]
    [InlineData("i18n-site")]
    [InlineData("taxonomy-site")]
    public async Task RealSite_MatchesGoldenPublicBytes_AndValidatesReports(string fixture)
    {
        using var site = new SiteBuilder(fixture);
        var started = DateTimeOffset.UtcNow;
        var result = await site.BuildAsync();
        var ended = DateTimeOffset.UtcNow;
        ReportAssertions.Generated(site, result, started, ended);

        var actual = OutputSnapshot.Public(site.Output, site.Config.Site.Languages?.ToArray() ?? []).Entries;
        Assert.NotEmpty(actual);
        Assert.All(actual.Values, entry => Assert.Equal("file", entry.Kind));
        var platform = OperatingSystem.IsWindows() ? "windows"
            : OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() ? "unix"
            : throw new PlatformNotSupportedException("No golden snapshots for this operating system.");
        var path = Path.Combine(SiteSandbox.RepositoryRoot, "tests/Bukit.Engine.Tests/Snapshots/Stage7", platform, fixture + ".golden.json");
        Assert.True(File.Exists(path), $"Missing golden baseline: {path}");
        var expected = JsonSerializer.Deserialize<Dictionary<string, byte[]>>(File.ReadAllText(path));
        Assert.NotNull(expected);
        AssertGolden(expected, actual);
    }

    [Fact, Trait("Category", "Unit")]
    public void GoldenComparison_RejectsChangedBytesAndMissingOrAdditionalPaths()
    {
        using var sandbox = new SiteSandbox();
        var path = sandbox.Write("one.txt", "before");
        var expected = new Dictionary<string, byte[]> { ["one.txt"] = File.ReadAllBytes(path) };
        void Check() => AssertGolden(expected, OutputSnapshot.Public(sandbox.Root).Entries);
        Check();

        File.WriteAllText(path, "after");
        Assert.NotNull(Record.Exception(Check));
        File.WriteAllBytes(path, expected["one.txt"]);
        sandbox.Write("extra.txt", "added");
        Assert.NotNull(Record.Exception(Check));
        File.Delete(sandbox.PathFor("extra.txt"));
        File.Delete(path);
        Assert.NotNull(Record.Exception(Check));
    }

    private static void AssertGolden(IReadOnlyDictionary<string, byte[]> expected,
        IReadOnlyDictionary<string, OutputSnapshot.SnapshotEntry> actual)
    {
        Assert.NotEmpty(expected);
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (var (name, bytes) in expected)
            Assert.True(bytes.AsSpan().SequenceEqual(actual[name].Bytes), $"Golden differs: {name}");
    }
}
