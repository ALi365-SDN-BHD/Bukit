using Bukit.Engine.Incremental;
using Bukit.Engine.IO;
using Bukit.Shared;
using System.Security.Cryptography;
using Xunit;
using Xunit.Sdk;

namespace Bukit.Engine.Tests;

public sealed class BuildManifestTests
{
    [Fact]
    public void SyncMediaOutputs_WhenPathChangesAfterVerifiedOpen_UsesVerifiedBytesForCopyAndFingerprint()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-manifest-verified-" + Guid.NewGuid().ToString("N"));
        var mediaDir = Path.Combine(root, "media");
        var outputDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(mediaDir);
        var sourceFile = Path.Combine(mediaDir, "image.jpg");
        File.WriteAllText(sourceFile, "safe-bytes");
        try
        {
            var manifest = new BuildManifest();
            var opener = new ReplacingSourceOpener("safe-bytes", "attacker-bytes");

            BuildManifestTracker.SyncMediaOutputs(
                mediaDir,
                outputDir,
                manifest,
                incrementalEnabled: false,
                new ConsoleLogger(LogLevel.Error),
                fingerprintMode: "sha256",
                opener: opener);

            var outputFile = Path.Combine(outputDir, "assets", "uploads", "image.jpg");
            Assert.True(opener.WasCalled);
            Assert.Equal("safe-bytes", File.ReadAllText(outputFile));
            Assert.Equal(
                Convert.ToHexString(SHA256.HashData("safe-bytes"u8.ToArray())).ToLowerInvariant(),
                manifest.Media["assets/uploads/image.jpg"]);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void SyncMediaOutputs_WhenVerifiedOpenIsRejected_DoesNotPublishReplacementBytes()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-manifest-rejected-" + Guid.NewGuid().ToString("N"));
        var mediaDir = Path.Combine(root, "media");
        var outputDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(mediaDir);
        Directory.CreateDirectory(Path.Combine(outputDir, "assets", "uploads"));
        File.WriteAllText(Path.Combine(mediaDir, "image.jpg"), "attacker-bytes");
        var outputFile = Path.Combine(outputDir, "assets", "uploads", "image.jpg");
        File.WriteAllText(outputFile, "existing-safe");
        try
        {
            var manifest = new BuildManifest();

            Assert.Throws<IOException>(() => BuildManifestTracker.SyncMediaOutputs(
                mediaDir,
                outputDir,
                manifest,
                incrementalEnabled: false,
                new ConsoleLogger(LogLevel.Error),
                opener: new RejectingSourceOpener()));

            Assert.Equal("existing-safe", File.ReadAllText(outputFile));
            Assert.Empty(manifest.Media);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void BuildManifestTracker_DoesNotCopyOrTrackMediaThroughDirectorySymlink()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-manifest-symlink-" + Guid.NewGuid().ToString("N"));
        var mediaDir = Path.Combine(root, "media");
        var externalDir = Path.Combine(root, "external");
        var outputDir = Path.Combine(root, "dist");
        try
        {
            Directory.CreateDirectory(Path.Combine(mediaDir, "local"));
            Directory.CreateDirectory(externalDir);
            File.WriteAllText(Path.Combine(mediaDir, "local", "image.jpg"), "local");
            File.WriteAllText(Path.Combine(externalDir, "secret.jpg"), "secret");

            try
            {
                Directory.CreateSymbolicLink(Path.Combine(mediaDir, "linked-external"), externalDir);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                throw SkipException.ForSkip($"Directory symlinks are unavailable: {ex.GetType().Name}");
            }

            var manifest = new BuildManifest();
            BuildManifestTracker.SyncMediaOutputs(
                mediaDir,
                outputDir,
                manifest,
                incrementalEnabled: false,
                new ConsoleLogger(LogLevel.Error));

            Assert.True(File.Exists(Path.Combine(outputDir, "assets", "uploads", "local", "image.jpg")));
            Assert.Contains("assets/uploads/local/image.jpg", manifest.Media.Keys);
            Assert.DoesNotContain(manifest.Media.Keys, path => path.Contains("linked-external", StringComparison.Ordinal));
            Assert.False(File.Exists(Path.Combine(outputDir, "assets", "uploads", "linked-external", "secret.jpg")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
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

        Assert.Equal(2, loaded.Version);
        Assert.Equal("meta-v1", loaded.Entries["pages/hello/index.html"].MetadataHash);
    }

    private sealed class ReplacingSourceOpener(
        string expectedContent,
        string replacementContent) : ISafeSourceFileOpener
    {
        public bool WasCalled { get; private set; }

        public VerifiedSourceFile Open(string path, string sourceRoot)
        {
            WasCalled = true;
            Assert.Equal(expectedContent, File.ReadAllText(path));
            var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var displacedPath = path + ".displaced";
            File.Move(path, displacedPath);
            File.WriteAllText(path, replacementContent);
            return new VerifiedSourceFile(stream.SafeFileHandle, stream, displacedPath);
        }
    }

    private sealed class RejectingSourceOpener : ISafeSourceFileOpener
    {
        public VerifiedSourceFile Open(string path, string sourceRoot)
            => throw new IOException("verified open rejected");
    }
}
