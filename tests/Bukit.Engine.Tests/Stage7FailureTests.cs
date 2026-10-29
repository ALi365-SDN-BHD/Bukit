using Bukit.Engine.Tests.Infrastructure;
using Bukit.Rendering;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class Stage7FailureTests
{
    [Fact, Trait("Category", "Integration")]
    public async Task MidRenderFailure_PreservesFormalOutputAndManagedCache()
    {
        using var site = new SiteBuilder();
        foreach (var slug in new[] { "alpha", "beta", "gamma" })
            site.Markdown(slug, slug, "Original " + slug);
        var started = DateTimeOffset.UtcNow;
        var baseline = await site.BuildAsync();
        var ended = DateTimeOffset.UtcNow;
        ReportAssertions.Generated(site, baseline, started, ended);

        var before = OutputSnapshot.Managed(("output", site.Output), ("cache", site.Cache));
        site.Markdown("alpha", "alpha", "Changed alpha");
        FailingRenderer? renderer = null;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => site.BuildAsync(clean: false,
            rendererFactory: layouts => renderer = new FailingRenderer(layouts, site.Output)));
        Assert.Equal("stage7 injected render failure", error.Message);
        Assert.NotNull(renderer);
        Assert.Equal(2, renderer.Calls);
        Assert.True(renderer.SawStagedWrite, "No rendered file was written to the transaction before failure.");
        before.AssertEqual(OutputSnapshot.Managed(("output", site.Output), ("cache", site.Cache)));
        Assert.Empty(Directory.EnumerateFileSystemEntries(site.Root, ".bukit-txn-*"));
    }

    private sealed class FailingRenderer(string layouts, string output) : ITemplateRenderer
    {
        private const string Marker = "<!--stage7-uncommitted-render-->";
        private readonly ITemplateRenderer _inner = new ScribanTemplateRendererAdapter(layouts);
        public int Calls { get; private set; }
        public bool SawStagedWrite { get; private set; }

        public string RenderPage(string templateRelativePath, PageModel model)
            => Render(() => _inner.RenderPage(templateRelativePath, model));

        public string RenderList(string templateRelativePath, ListPageModel model)
            => Render(() => _inner.RenderList(templateRelativePath, model));

        private string Render(Func<string> render)
        {
            if (++Calls == 2)
            {
                var stage = BuildTransaction.Physical(output);
                SawStagedWrite = stage != output && Directory.EnumerateFiles(stage, "*.html", SearchOption.AllDirectories)
                    .Any(path => File.ReadAllText(path).Contains(Marker, StringComparison.Ordinal));
                throw new InvalidOperationException("stage7 injected render failure");
            }
            return render() + Marker;
        }
    }
}
