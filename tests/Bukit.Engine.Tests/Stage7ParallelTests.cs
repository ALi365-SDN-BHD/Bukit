using Bukit.Engine.Tests.Infrastructure;
using Bukit.Rendering;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class Stage7ParallelTests
{
    [Fact, Trait("Category", "Integration")]
    public async Task ParallelRender_ActuallyOverlaps_AndMatchesSerialPublicBytes()
    {
        using var serial = new SiteBuilder();
        using var parallel = new SiteBuilder();
        foreach (var site in new[] { serial, parallel })
            foreach (var slug in new[] { "alpha", "beta", "gamma", "delta" })
                site.Markdown(slug, slug, "Body " + slug);

        using var serialRenderer = new OverlapRenderer(serial.Sandbox.PathFor("layouts"), rendezvous: false);
        using var parallelRenderer = new OverlapRenderer(parallel.Sandbox.PathFor("layouts"), rendezvous: true);
        var serialResult = await Build(serial, serialRenderer, jobs: 1);
        var parallelResult = await Build(parallel, parallelRenderer, jobs: 2);

        Assert.Equal(1, serialRenderer.Peak);
        Assert.True(parallelRenderer.Peak >= 2, "Parallel rendering did not overlap.");
        Assert.True(serialResult.Summary.PageCount > 1);
        Assert.Equal(serialResult.Summary.PageCount, parallelResult.Summary.PageCount);
        Assert.Equal(serialResult.Summary.RouteCount, parallelResult.Summary.RouteCount);
        OutputSnapshot.Public(serial.Output).AssertEqual(OutputSnapshot.Public(parallel.Output));
    }

    private static async Task<BuildResult> Build(SiteBuilder site, ITemplateRenderer renderer, int jobs)
    {
        var started = DateTimeOffset.UtcNow;
        var result = await site.BuildAsync(jobs: jobs, rendererFactory: _ => renderer);
        var ended = DateTimeOffset.UtcNow;
        ReportAssertions.Generated(site, result, started, ended);
        return result;
    }

    private sealed class OverlapRenderer(string layouts, bool rendezvous) : ITemplateRenderer, IDisposable
    {
        private readonly ITemplateRenderer _inner = new ScribanTemplateRendererAdapter(layouts);
        private readonly CountdownEvent _gate = new(2);
        private readonly object _lock = new();
        private int _active;
        private int _arrivals;
        public int Peak { get; private set; }

        public string RenderPage(string templateRelativePath, PageModel model)
            => Render(() => _inner.RenderPage(templateRelativePath, model));

        public string RenderList(string templateRelativePath, ListPageModel model)
            => Render(() => _inner.RenderList(templateRelativePath, model));

        private string Render(Func<string> render)
        {
            lock (_lock) { _active++; Peak = Math.Max(Peak, _active); }
            try
            {
                if (rendezvous && Interlocked.Increment(ref _arrivals) <= 2)
                {
                    _gate.Signal();
                    Assert.True(_gate.Wait(TimeSpan.FromSeconds(10)), "Parallel rendering did not overlap.");
                }
                return render();
            }
            finally { lock (_lock) _active--; }
        }

        public void Dispose() => _gate.Dispose();
    }
}
