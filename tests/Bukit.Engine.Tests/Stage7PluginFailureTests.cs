using Bukit.Engine.Tests.Infrastructure;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class Stage7PluginFailureTests
{
    [Fact, Trait("Category", "Integration")]
    public async Task StrictBuiltInPluginFailure_PreservesFormalOutputAndManagedCache()
    {
        using var site = new SiteBuilder();
        site.Markdown("alpha", "Alpha", "Original body");
        var started = DateTimeOffset.UtcNow;
        var baseline = await site.BuildAsync();
        var ended = DateTimeOffset.UtcNow;
        ReportAssertions.Generated(site, baseline, started, ended);
        Assert.Equal("strict", site.Config.Site.PluginFailMode);
        var before = OutputSnapshot.Managed(("output", site.Output), ("cache", site.Cache));

        site.Sandbox.Write("data/fault.toml", "invalid-for-bukit = true\n");
        var error = await Assert.ThrowsAsync<ConfigException>(() => site.BuildAsync(clean: false));
        Assert.Equal(DiagnosticCode.ConfigInvalidValue, error.Code);
        Assert.Contains("Unsupported data file format: data/fault.toml", error.Message, StringComparison.Ordinal);
        before.AssertEqual(OutputSnapshot.Managed(("output", site.Output), ("cache", site.Cache)));
        Assert.Empty(Directory.EnumerateFileSystemEntries(site.Root, ".bukit-txn-*"));
    }
}
