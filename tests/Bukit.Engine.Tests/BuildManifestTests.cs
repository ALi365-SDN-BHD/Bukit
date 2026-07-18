using Bukit.Engine.Incremental;
using Bukit.Shared;
using Xunit;
using Xunit.Sdk;

namespace Bukit.Engine.Tests;

public sealed class BuildManifestTests
{
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
}
