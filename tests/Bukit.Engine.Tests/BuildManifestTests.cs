using Bukit.Engine.Incremental;
using Bukit.Shared;
using System.Security.Cryptography;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class BuildManifestTests
{
    [Theory]
    [InlineData("")]
    [InlineData(",\"ownedOutputs\":null")]
    [InlineData(",\"ownedOutputs\":[\"a.json\",3]")]
    public void Load_CorruptOwnershipIsNotTrusted(string ownership)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "{\"version\":3,\"outputRoot\":\"/tmp/output\"" + ownership + "}");
            Assert.Empty(BuildManifest.Load(path).OutputRoot);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SaveAndLoad_EmptyOwnershipRetainsRoot()
    {
        var path = Path.GetTempFileName();
        try
        {
            new BuildManifest { OutputRoot = "/tmp/output" }.Save(path);
            var loaded = BuildManifest.Load(path);
            Assert.Equal("/tmp/output", loaded.OutputRoot);
            Assert.Empty(loaded.OwnedOutputs);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TrackAssetPlanOutputs_Sha256LargeFile_UsesBoundedAllocation()
    {
        const int largeFileSize = 16 * 1024 * 1024;
        const long allocationLimit = largeFileSize / 4;
        var root = Path.Combine(Path.GetTempPath(), "bukit-manifest-hash-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var warmupPath = Path.Combine(root, "warmup.bin");
        var largePath = Path.Combine(root, "large.bin");
        File.WriteAllText(warmupPath, "warmup");
        using (var file = new FileStream(largePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            file.SetLength(largeFileSize);
        }

        try
        {
            BuildManifestTracker.TrackAssetPlanOutputs(
                [new AssetOutputItem("warmup", "warmup.bin", AssetOutputCategory.Static)],
                root,
                StringComparer.Ordinal,
                new BuildManifest(),
                incrementalEnabled: false,
                new ConsoleLogger(LogLevel.Error),
                fingerprintMode: "sha256");

            var manifest = new BuildManifest();
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            BuildManifestTracker.TrackAssetPlanOutputs(
                [new AssetOutputItem("large", "large.bin", AssetOutputCategory.Static)],
                root,
                StringComparer.Ordinal,
                manifest,
                incrementalEnabled: false,
                new ConsoleLogger(LogLevel.Error),
                fingerprintMode: "sha256");
            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

            string expected;
            using (var expectedStream = File.OpenRead(largePath))
            {
                expected = Convert.ToHexString(SHA256.HashData(expectedStream)).ToLowerInvariant();
            }

            Assert.Equal(expected, manifest.Static["large.bin"]);
            Assert.True(
                allocatedBytes < allocationLimit,
                $"SHA-256 fingerprinting allocated {allocatedBytes:N0} bytes for a {largeFileSize:N0}-byte file.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ComputeSha256_WhenCanceledAfterFirstChunk_StopsBeforeConsumingStream()
    {
        using var cts = new CancellationTokenSource();
        using var stream = new CancelAfterFirstReadStream(256 * 1024, cts);

        Assert.ThrowsAny<OperationCanceledException>(
            () => BuildManifestTracker.ComputeSha256(stream, cts.Token));

        Assert.InRange(stream.BytesRead, 1, stream.Length - 1);
    }

    [Fact]
    public void SaveAndLoad_PreservesStructuredPluginOutputs()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var manifestPath = Path.Combine(tempDir, "build-manifest.json");

        var manifest = new BuildManifest
        {
            PluginOutputs = new Dictionary<string, PluginOutputManifestEntry>(StringComparer.Ordinal)
            {
                ["plugin-output.json"] = new()
                {
                    Plugin = "sample",
                    Hook = "after-build",
                    Path = "plugin-output.json",
                    Hash = "hash-v1"
                }
            }
        };

        manifest.Save(manifestPath);
        var loaded = BuildManifest.Load(manifestPath);

        Assert.Equal("sample", loaded.PluginOutputs["plugin-output.json"].Plugin);
        Assert.Equal("after-build", loaded.PluginOutputs["plugin-output.json"].Hook);
        Assert.Equal("plugin-output.json", loaded.PluginOutputs["plugin-output.json"].Path);
        Assert.Equal("hash-v1", loaded.PluginOutputs["plugin-output.json"].Hash);
    }

    [Fact]
    public void SaveAndLoad_PreservesMetadataHash()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var manifestPath = Path.Combine(tempDir, "build-manifest.json");

        var manifest = new BuildManifest
        {
            Entries = new Dictionary<string, BuildManifestEntry>(StringComparer.Ordinal)
            {
                ["pages/hello/index.html"] = new()
                {
                    OutputPath = "pages/hello/index.html",
                    Url = "/pages/hello/",
                    Template = "pages/page.html",
                    MetadataHash = "meta-v1",
                    ContentHash = "content-v1",
                    RouteHash = "route-v1",
                    TemplateHash = "template-v1"
                }
            }
        };

        manifest.Save(manifestPath);
        var loaded = BuildManifest.Load(manifestPath);

        Assert.Equal(3, loaded.Version);
        Assert.Equal("meta-v1", loaded.Entries["pages/hello/index.html"].MetadataHash);
    }

    private sealed class CancelAfterFirstReadStream : Stream
    {
        private const int MaxChunkSize = 1024;
        private readonly MemoryStream _inner;
        private readonly CancellationTokenSource _cancellationSource;
        private int _readCount;

        public CancelAfterFirstReadStream(int length, CancellationTokenSource cancellationSource)
        {
            _inner = new MemoryStream(new byte[length], writable: false);
            _cancellationSource = cancellationSource;
        }

        public long BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, Math.Min(count, MaxChunkSize));
            ObserveRead(read);
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = _inner.Read(buffer[..Math.Min(buffer.Length, MaxChunkSize)]);
            ObserveRead(read);
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private void ObserveRead(int read)
        {
            if (read <= 0)
            {
                return;
            }

            BytesRead += read;
            if (Interlocked.Increment(ref _readCount) == 1)
            {
                _cancellationSource.Cancel();
            }
        }
    }
}
