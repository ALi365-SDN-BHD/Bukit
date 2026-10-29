using Bukit.Engine.Tests.Infrastructure;
using Bukit.Rendering;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class Stage7DiskFullTests
{
    [Fact]
    public void UnsafeVolume_IsRejectedBeforeAnyWrites()
    {
        var hostRoot = Path.GetPathRoot(Path.GetTempPath())!;
        Assert.Throws<InvalidOperationException>(() => ValidateVolume(hostRoot, ""));
        Assert.Throws<InvalidOperationException>(() => ValidateVolume(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), "wrong"));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task RealOutputVolumeFull_PreservesFormalOutputAndCache()
    {
        var volume = Environment.GetEnvironmentVariable("BUKIT_STAGE7_DISK_VOLUME")
            ?? throw new InvalidOperationException("A disposable volume is required.");
        var owner = Environment.GetEnvironmentVariable("BUKIT_STAGE7_DISK_OWNER") ?? "";
        ValidateVolume(volume, owner);
        Assert.Throws<InvalidOperationException>(() => ValidateVolume(volume, Guid.NewGuid().ToString("N")));

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(volume));
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        Assert.StartsWith(prefix, Path.GetFullPath(Path.GetTempPath()), comparison);

        using var site = new SiteBuilder();
        Assert.StartsWith(prefix, site.Root, comparison);
        foreach (var slug in new[] { "alpha", "beta", "gamma" })
            site.Markdown(slug, slug, "Original " + slug);
        var started = DateTimeOffset.UtcNow;
        var baseline = await site.BuildAsync();
        var ended = DateTimeOffset.UtcNow;
        ReportAssertions.Generated(site, baseline, started, ended);
        var before = OutputSnapshot.Managed(("output", site.Output), ("cache", site.Cache));
        site.Markdown("alpha", "alpha", "Changed alpha");
        var filler = Path.Combine(root, "stage7-fill-" + Guid.NewGuid().ToString("N"));
        FullVolumeRenderer? renderer = null;
        IOException failure;
        try
        {
            failure = await Assert.ThrowsAnyAsync<IOException>(() => site.BuildAsync(clean: false,
                rendererFactory: layouts => renderer = new FullVolumeRenderer(layouts, site.Output, filler)));
        }
        finally
        {
            if (File.Exists(filler)) File.Delete(filler);
        }
        Assert.NotNull(renderer);
        Assert.True(renderer.SawDiskFull, "Filling the disposable volume did not produce a native disk-full error.");
        Assert.True(IsDiskFull(failure), $"Unexpected write failure: {failure}");
        Assert.Contains("FileWriter.WriteUtf8", failure.ToString(), StringComparison.Ordinal);
        before.AssertEqual(OutputSnapshot.Managed(("output", site.Output), ("cache", site.Cache)));
        Assert.Empty(Directory.EnumerateFileSystemEntries(site.Root, ".bukit-txn-*"));
    }

    private static void ValidateVolume(string volume, string owner)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(volume));
        var drive = DriveInfo.GetDrives().SingleOrDefault(item =>
            string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(item.RootDirectory.FullName)), root, comparison));
        if (drive is null || !drive.IsReady || drive.TotalSize <= 0 || drive.TotalSize > 64L * 1024 * 1024 ||
            !Guid.TryParseExact(owner, "N", out _))
            throw new InvalidOperationException("A mounted, owned volume of at most 64 MiB is required.");

        var marker = Path.Combine(root, ".bukit-stage7-volume-owner");
        if (!File.Exists(marker) || (File.GetAttributes(marker) & FileAttributes.ReparsePoint) != 0 ||
            !string.Equals(File.ReadAllText(marker), owner, StringComparison.Ordinal))
            throw new InvalidOperationException("Disposable volume ownership does not match.");
    }

    private static bool IsNativeDiskFull(IOException error)
        => OperatingSystem.IsWindows() ? (error.HResult & 0xffff) is 39 or 112 : (error.HResult & 0xffff) == 28;

    private static bool IsDiskFull(IOException error)
        => IsNativeDiskFull(error) ||
           error.Message.Contains("disk was full", StringComparison.OrdinalIgnoreCase) ||
           error.Message.Contains("not enough space on the disk", StringComparison.OrdinalIgnoreCase) ||
           error.Message.Contains("no space left on device", StringComparison.OrdinalIgnoreCase);

    private sealed class FullVolumeRenderer(string layouts, string output, string filler) : ITemplateRenderer
    {
        private readonly ITemplateRenderer _inner = new ScribanTemplateRendererAdapter(layouts);
        public bool SawDiskFull { get; private set; }

        public string RenderPage(string templateRelativePath, PageModel model)
            => Render(() => _inner.RenderPage(templateRelativePath, model));

        public string RenderList(string templateRelativePath, ListPageModel model)
            => Render(() => _inner.RenderList(templateRelativePath, model));

        private string Render(Func<string> render)
        {
            Assert.NotEqual(output, BuildTransaction.Physical(output));
            try
            {
                using var stream = new FileStream(filler, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
                var buffer = new byte[64 * 1024];
                for (var total = 0; total < 64 * 1024 * 1024; total += buffer.Length) stream.Write(buffer);
                stream.Flush(flushToDisk: true);
            }
            catch (IOException error) when (IsNativeDiskFull(error))
            {
                SawDiskFull = true;
            }
            Assert.True(SawDiskFull, "The bounded volume did not produce an actual disk-full error.");
            return render() + new string('x', 2 * 1024 * 1024);
        }
    }
}
