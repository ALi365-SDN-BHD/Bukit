using Bukit.Engine.Incremental;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class BuildManifestTests
{
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
