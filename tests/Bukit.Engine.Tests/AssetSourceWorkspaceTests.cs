using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class AssetSourceWorkspaceTests
{
    [Fact]
    public async Task PrepareAsync_EnabledImagesWithEmptyAssetsDir_CreatesWorkspace()
    {
        var sourceAssets = Path.Combine(
            Path.GetTempPath(),
            "bukit-empty-assets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sourceAssets);
        try
        {
            using var workspace = await AssetSourceWorkspace.PrepareAsync(
                sourceAssets,
                scssConfig: null,
                new ImageOptimizationConfig { Enabled = true, Formats = Array.Empty<string>() },
                new ConsoleLogger(LogLevel.Error),
                publishDotFiles: false,
                followSymlinks: false);

            Assert.True(Directory.Exists(workspace.AssetsDir));
        }
        finally
        {
            Directory.Delete(sourceAssets, recursive: true);
        }
    }
}
