using Bukit.Config;
using Bukit.Engine.Incremental;
using Bukit.Shared;
using Xunit;
using Xunit.Sdk;

namespace Bukit.Engine.Tests;

public sealed class AssetPipelineTests
{
    [Fact]
    public async Task ExecuteAsync_StaticAndAssetsSameDestination_FailsBeforeWriting()
    {
        var root = CreateRoot();
        var staticDir = Path.Combine(root, "static");
        var assetsDir = Path.Combine(root, "assets");
        Directory.CreateDirectory(Path.Combine(staticDir, "assets", "css"));
        Directory.CreateDirectory(Path.Combine(assetsDir, "css"));
        File.WriteAllText(Path.Combine(staticDir, "assets", "css", "main.css"), "static");
        File.WriteAllText(Path.Combine(assetsDir, "css", "main.css"), "asset");
        var manifest = new BuildManifest();

        var exception = await Assert.ThrowsAsync<BukitException>(() => new AssetPipeline().ExecuteAsync(
            CreateContext(root, manifest, staticDir: staticDir, assetsDir: assetsDir)));
        var repeated = await Assert.ThrowsAsync<BukitException>(() => new AssetPipeline().ExecuteAsync(
            CreateContext(root, manifest, staticDir: staticDir, assetsDir: assetsDir)));

        Assert.Equal(DiagnosticCode.BuildAssetOutputCollision, exception.Code);
        Assert.Equal(exception.Code, repeated.Code);
        Assert.Equal(exception.Message, repeated.Message);
        Assert.Contains("assets/css/main.css", exception.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, "dist")));
        Assert.Empty(manifest.Static);
        Assert.Empty(manifest.Assets);
    }

    [Fact]
    public async Task ExecuteAsync_AssetsAndGeneratedTokensSameDestination_FailsBeforeWriting()
    {
        var root = CreateRoot();
        var assetsDir = Path.Combine(root, "assets");
        var themeRoot = Path.Combine(root, "theme");
        Directory.CreateDirectory(Path.Combine(assetsDir, "css"));
        Directory.CreateDirectory(themeRoot);
        File.WriteAllText(Path.Combine(assetsDir, "css", "theme-tokens.css"), "asset");
        File.WriteAllText(Path.Combine(themeRoot, "tokens.yaml"), "colors:\n  primary: '#000'\n");

        var exception = await Assert.ThrowsAsync<BukitException>(() => new AssetPipeline().ExecuteAsync(
            CreateContext(root, new BuildManifest(), assetsDir: assetsDir, themeRoot: themeRoot)));

        Assert.Equal(DiagnosticCode.BuildAssetOutputCollision, exception.Code);
        Assert.Contains("assets/css/theme-tokens.css", exception.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, "dist")));
    }

    [Fact]
    public async Task ExecuteAsync_StaticAndMediaSameDestination_FailsBeforeWriting()
    {
        var root = CreateRoot();
        var staticDir = Path.Combine(root, "static");
        var mediaDir = Path.Combine(root, "media");
        Directory.CreateDirectory(Path.Combine(staticDir, "assets", "uploads"));
        Directory.CreateDirectory(mediaDir);
        File.WriteAllText(Path.Combine(staticDir, "assets", "uploads", "a.jpg"), "static");
        File.WriteAllText(Path.Combine(mediaDir, "a.jpg"), "media");

        var exception = await Assert.ThrowsAsync<BukitException>(() => new AssetPipeline().ExecuteAsync(
            CreateContext(root, new BuildManifest(), staticDir: staticDir, mediaDir: mediaDir)));

        Assert.Equal(DiagnosticCode.BuildAssetOutputCollision, exception.Code);
        Assert.Contains("assets/uploads/a.jpg", exception.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, "dist")));
    }

    [Fact]
    public async Task ExecuteAsync_ParentAndSiteStaticSameDestination_PreservesSiteOverride()
    {
        var root = CreateRoot();
        var parentStaticDir = Path.Combine(root, "parent-static");
        var staticDir = Path.Combine(root, "static");
        Directory.CreateDirectory(parentStaticDir);
        Directory.CreateDirectory(staticDir);
        File.WriteAllText(Path.Combine(parentStaticDir, "shared.txt"), "parent");
        File.WriteAllText(Path.Combine(staticDir, "shared.txt"), "site");

        await new AssetPipeline().ExecuteAsync(CreateContext(
            root,
            new BuildManifest(),
            staticDir: staticDir,
            parentStaticDir: parentStaticDir));

        Assert.Equal("site", File.ReadAllText(Path.Combine(root, "dist", "shared.txt")));
    }

    [Fact]
    public async Task ExecuteAsync_ParentAndSiteAssetsSameDestination_PreservesSiteOverride()
    {
        var root = CreateRoot();
        var parentAssetsDir = Path.Combine(root, "parent-assets");
        var assetsDir = Path.Combine(root, "assets");
        Directory.CreateDirectory(Path.Combine(parentAssetsDir, "css"));
        Directory.CreateDirectory(Path.Combine(assetsDir, "css"));
        File.WriteAllText(Path.Combine(parentAssetsDir, "css", "main.css"), "parent");
        File.WriteAllText(Path.Combine(assetsDir, "css", "main.css"), "site");

        var manifest = new BuildManifest();
        await new AssetPipeline().ExecuteAsync(CreateContext(
            root,
            manifest,
            assetsDir: assetsDir,
            parentAssetsDir: parentAssetsDir));

        Assert.Equal("site", File.ReadAllText(Path.Combine(root, "dist", "assets", "css", "main.css")));
        Assert.Single(manifest.Assets);
        Assert.Contains("assets/css/main.css", manifest.Assets.Keys);
    }

    [Fact]
    public async Task ExecuteAsync_ThemeWithoutTokens_DoesNotCreateTokenClaim()
    {
        var root = CreateRoot();
        var assetsDir = Path.Combine(root, "assets");
        var themeRoot = Path.Combine(root, "theme");
        Directory.CreateDirectory(Path.Combine(assetsDir, "css"));
        Directory.CreateDirectory(themeRoot);
        File.WriteAllText(Path.Combine(assetsDir, "css", "theme-tokens.css"), "site-owned");

        await new AssetPipeline().ExecuteAsync(CreateContext(
            root,
            new BuildManifest(),
            assetsDir: assetsDir,
            themeRoot: themeRoot));

        Assert.Equal("site-owned", File.ReadAllText(Path.Combine(root, "dist", "assets", "css", "theme-tokens.css")));
    }

    [Fact]
    public async Task ExecuteAsync_SkippedDirectorySymlink_DoesNotCreateGhostClaim()
    {
        var root = CreateRoot();
        var staticDir = Path.Combine(root, "static");
        var assetsDir = Path.Combine(root, "assets");
        var externalDir = Path.Combine(root, "external");
        Directory.CreateDirectory(Path.Combine(staticDir, "assets"));
        Directory.CreateDirectory(Path.Combine(assetsDir, "css"));
        Directory.CreateDirectory(externalDir);
        File.WriteAllText(Path.Combine(assetsDir, "css", "main.css"), "asset");
        File.WriteAllText(Path.Combine(externalDir, "main.css"), "external");
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(staticDir, "assets", "css"), externalDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            throw SkipException.ForSkip($"Directory symlinks are unavailable: {ex.GetType().Name}");
        }

        await new AssetPipeline().ExecuteAsync(CreateContext(
            root,
            new BuildManifest(),
            staticDir: staticDir,
            assetsDir: assetsDir));

        Assert.Equal("asset", File.ReadAllText(Path.Combine(root, "dist", "assets", "css", "main.css")));
    }

    [Fact]
    public async Task ExecuteAsync_FollowedFileSymlinkCollision_IsDetectedBeforeWriting()
    {
        var root = CreateRoot();
        var assetsDir = Path.Combine(root, "assets");
        var themeRoot = Path.Combine(root, "theme");
        Directory.CreateDirectory(Path.Combine(assetsDir, "css"));
        Directory.CreateDirectory(themeRoot);
        var sourceFile = Path.Combine(assetsDir, "css", "source.css");
        File.WriteAllText(sourceFile, "asset");
        try
        {
            File.CreateSymbolicLink(Path.Combine(assetsDir, "css", "theme-tokens.css"), sourceFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            throw SkipException.ForSkip($"File symlinks are unavailable: {ex.GetType().Name}");
        }
        File.WriteAllText(Path.Combine(themeRoot, "tokens.yaml"), "colors:\n  primary: '#000'\n");

        var exception = await Assert.ThrowsAsync<BukitException>(() => new AssetPipeline().ExecuteAsync(CreateContext(
            root,
            new BuildManifest(),
            assetsDir: assetsDir,
            themeRoot: themeRoot,
            followSymlinks: true)));

        Assert.Equal(DiagnosticCode.BuildAssetOutputCollision, exception.Code);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, "dist")));
    }

    [Fact]
    public async Task ExecuteAsync_FollowedFileSymlinkWithoutCollision_IsCopiedAndTracked()
    {
        var root = CreateRoot();
        var assetsDir = Path.Combine(root, "assets");
        Directory.CreateDirectory(Path.Combine(assetsDir, "css"));
        var sourceFile = Path.Combine(assetsDir, "css", "source.css");
        File.WriteAllText(sourceFile, "asset");
        try
        {
            File.CreateSymbolicLink(Path.Combine(assetsDir, "css", "alias.css"), sourceFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            throw SkipException.ForSkip($"File symlinks are unavailable: {ex.GetType().Name}");
        }

        var manifest = new BuildManifest();
        await new AssetPipeline().ExecuteAsync(CreateContext(
            root,
            manifest,
            assetsDir: assetsDir,
            followSymlinks: true));

        Assert.Equal("asset", File.ReadAllText(Path.Combine(root, "dist", "assets", "css", "alias.css")));
        Assert.Contains("assets/css/source.css", manifest.Assets.Keys);
        Assert.Contains("assets/css/alias.css", manifest.Assets.Keys);
    }

    [Fact]
    public async Task ExecuteAsync_FollowedSymlinkToSensitiveFile_IsSkipped()
    {
        var root = CreateRoot();
        var assetsDir = Path.Combine(root, "assets");
        Directory.CreateDirectory(assetsDir);
        var sensitiveFile = Path.Combine(assetsDir, ".env");
        File.WriteAllText(sensitiveFile, "secret");
        try
        {
            File.CreateSymbolicLink(Path.Combine(assetsDir, "public.txt"), sensitiveFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            throw SkipException.ForSkip($"File symlinks are unavailable: {ex.GetType().Name}");
        }

        var manifest = new BuildManifest();
        await new AssetPipeline().ExecuteAsync(CreateContext(
            root,
            manifest,
            assetsDir: assetsDir,
            followSymlinks: true,
            publishDotFiles: true));

        Assert.False(File.Exists(Path.Combine(root, "dist", "assets", "public.txt")));
        Assert.Empty(manifest.Assets);
    }

    [Fact]
    public async Task ExecuteAsync_FollowedSymlinkThroughExternalIntermediate_IsSkipped()
    {
        var root = CreateRoot();
        var assetsDir = Path.Combine(root, "assets");
        var externalDir = Path.Combine(root, "external");
        Directory.CreateDirectory(assetsDir);
        Directory.CreateDirectory(externalDir);
        File.WriteAllText(Path.Combine(externalDir, "secret.css"), "secret");
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(assetsDir, "hop"), externalDir);
            File.CreateSymbolicLink(
                Path.Combine(assetsDir, "public.css"),
                Path.Combine("hop", "secret.css"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            throw SkipException.ForSkip($"Symbolic links are unavailable: {ex.GetType().Name}");
        }

        var manifest = new BuildManifest();
        await new AssetPipeline().ExecuteAsync(CreateContext(
            root,
            manifest,
            assetsDir: assetsDir,
            followSymlinks: true));

        Assert.False(File.Exists(Path.Combine(root, "dist", "assets", "public.css")));
        Assert.Empty(manifest.Assets);
    }

    [Fact]
    public async Task ExecuteAsync_FollowedSymlinkToCaseVariantExternalRoot_IsSkippedOnCaseSensitiveFileSystem()
    {
        var root = CreateRoot();
        var assetsDir = Path.Combine(root, "source");
        var externalDir = Path.Combine(root, "SOURCE");
        Directory.CreateDirectory(assetsDir);
        File.WriteAllText(Path.Combine(assetsDir, "source-marker"), "source");
        Directory.CreateDirectory(externalDir);
        if (File.Exists(Path.Combine(externalDir, "source-marker")))
        {
            return;
        }

        File.WriteAllText(Path.Combine(externalDir, "secret.css"), "secret");
        try
        {
            File.CreateSymbolicLink(
                Path.Combine(assetsDir, "public.css"),
                Path.Combine(externalDir, "secret.css"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            throw SkipException.ForSkip($"File symlinks are unavailable: {ex.GetType().Name}");
        }

        var manifest = new BuildManifest();
        await new AssetPipeline().ExecuteAsync(CreateContext(
            root,
            manifest,
            assetsDir: assetsDir,
            followSymlinks: true));

        Assert.False(File.Exists(Path.Combine(root, "dist", "assets", "public.css")));
        Assert.DoesNotContain("assets/public.css", manifest.Assets.Keys);
    }

    [Fact]
    public async Task ExecuteAsync_FollowedDirectorySymlinkCycle_IsSkipped()
    {
        var root = CreateRoot();
        var assetsDir = Path.Combine(root, "assets");
        Directory.CreateDirectory(assetsDir);
        File.WriteAllText(Path.Combine(assetsDir, "main.css"), "asset");
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(assetsDir, "loop"), assetsDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            throw SkipException.ForSkip($"Directory symlinks are unavailable: {ex.GetType().Name}");
        }

        var manifest = new BuildManifest();
        await new AssetPipeline().ExecuteAsync(CreateContext(
            root,
            manifest,
            assetsDir: assetsDir,
            followSymlinks: true));

        Assert.Equal("asset", File.ReadAllText(Path.Combine(root, "dist", "assets", "main.css")));
        Assert.DoesNotContain(manifest.Assets.Keys, path => path.Contains("loop", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_FileAndDirectoryPrefixCollision_FailsBeforeWriting()
    {
        var root = CreateRoot();
        var staticDir = Path.Combine(root, "static");
        var assetsDir = Path.Combine(root, "assets");
        Directory.CreateDirectory(staticDir);
        Directory.CreateDirectory(Path.Combine(assetsDir, "css"));
        File.WriteAllText(Path.Combine(staticDir, "assets"), "blocks-assets-directory");
        File.WriteAllText(Path.Combine(assetsDir, "css", "main.css"), "asset");

        var exception = await Assert.ThrowsAsync<BukitException>(() => new AssetPipeline().ExecuteAsync(
            CreateContext(root, new BuildManifest(), staticDir: staticDir, assetsDir: assetsDir)));

        Assert.Equal(DiagnosticCode.BuildAssetOutputCollision, exception.Code);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, "dist")));
    }

    [Fact]
    public async Task ExecuteAsync_DescendantAndFilePrefixCollision_FailsBeforeWriting()
    {
        var root = CreateRoot();
        var staticDir = Path.Combine(root, "static");
        var assetsDir = Path.Combine(root, "assets");
        Directory.CreateDirectory(Path.Combine(staticDir, "assets", "css"));
        Directory.CreateDirectory(assetsDir);
        File.WriteAllText(Path.Combine(staticDir, "assets", "css", "main.css"), "static");
        File.WriteAllText(Path.Combine(assetsDir, "css"), "blocks-css-directory");

        var exception = await Assert.ThrowsAsync<BukitException>(() => new AssetPipeline().ExecuteAsync(
            CreateContext(root, new BuildManifest(), staticDir: staticDir, assetsDir: assetsDir)));

        Assert.Equal(DiagnosticCode.BuildAssetOutputCollision, exception.Code);
        Assert.Contains("assets/css", exception.Message, StringComparison.Ordinal);
        Assert.Contains("assets/css/main.css", exception.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, "dist")));
    }

    [Fact]
    public void Create_CaseVariantCrossCategoryTargets_FollowPlatformPathSemantics()
    {
        var root = CreateRoot();
        var staticDir = Path.Combine(root, "static");
        var assetsDir = Path.Combine(root, "assets");
        Directory.CreateDirectory(Path.Combine(staticDir, "assets", "css"));
        Directory.CreateDirectory(Path.Combine(assetsDir, "css"));
        File.WriteAllText(Path.Combine(staticDir, "assets", "css", "Main.css"), "static");
        File.WriteAllText(Path.Combine(assetsDir, "css", "main.css"), "asset");

        var context = CreateContext(root, new BuildManifest(), staticDir: staticDir, assetsDir: assetsDir);
        var copyOptions = new DirectoryCopyOptions();

        if (OperatingSystem.IsWindows())
        {
            var exception = Assert.Throws<BukitException>(() => AssetOutputPlan.Create(context, copyOptions, tokens: null));

            Assert.Equal(DiagnosticCode.BuildAssetOutputCollision, exception.Code);
            return;
        }

        var plan = AssetOutputPlan.Create(context, copyOptions, tokens: null);

        Assert.Contains(plan.Items, item => item.Destination == "assets/css/Main.css");
        Assert.Contains(plan.Items, item => item.Destination == "assets/css/main.css");
    }

    [Fact]
    public async Task ExecuteAsync_CaseVariantCrossCategoryTargets_FollowFileSystemSemantics()
    {
        var root = CreateRoot();
        var staticDir = Path.Combine(root, "static");
        var assetsDir = Path.Combine(root, "assets");
        Directory.CreateDirectory(Path.Combine(staticDir, "assets", "css"));
        Directory.CreateDirectory(Path.Combine(assetsDir, "css"));
        File.WriteAllText(Path.Combine(staticDir, "assets", "css", "Main.css"), "static");
        File.WriteAllText(Path.Combine(assetsDir, "css", "main.css"), "asset");
        var manifest = new BuildManifest();

        if (OperatingSystem.IsWindows())
        {
            var exception = await Assert.ThrowsAsync<BukitException>(() => new AssetPipeline().ExecuteAsync(
                CreateContext(root, manifest, staticDir: staticDir, assetsDir: assetsDir)));

            Assert.Equal(DiagnosticCode.BuildAssetOutputCollision, exception.Code);
            Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, "dist")));
            return;
        }

        var probe = Path.Combine(root, "dist", "case-probe");
        File.WriteAllText(probe, string.Empty);
        var isCaseSensitive = !File.Exists(Path.Combine(root, "dist", "CASE-PROBE"));
        File.Delete(probe);
        if (!isCaseSensitive)
        {
            return;
        }

        await new AssetPipeline().ExecuteAsync(
            CreateContext(root, manifest, staticDir: staticDir, assetsDir: assetsDir));

        Assert.Equal("static", File.ReadAllText(Path.Combine(root, "dist", "assets", "css", "Main.css")));
        Assert.Equal("asset", File.ReadAllText(Path.Combine(root, "dist", "assets", "css", "main.css")));
        Assert.Contains("assets/css/Main.css", manifest.Static.Keys);
        Assert.Contains("assets/css/main.css", manifest.Assets.Keys);
    }

    [Fact]
    public void Create_ChildRenderedStaticHtml_SuppressesParentAndChildRawCopies()
    {
        var root = CreateRoot();
        var parentStaticDir = Path.Combine(root, "parent-static");
        var staticDir = Path.Combine(root, "static");
        Directory.CreateDirectory(parentStaticDir);
        Directory.CreateDirectory(staticDir);
        File.WriteAllText(Path.Combine(parentStaticDir, "about.html"), "parent");
        File.WriteAllText(Path.Combine(staticDir, "about.html"), "child");
        var renderedEntries = RenderEntry.ForStaticDir(
            staticDir,
            "pages/static.html",
            _ => { },
            publishDotFiles: false);

        var plan = AssetOutputPlan.Create(
            CreateContext(
                root,
                new BuildManifest(),
                staticDir: staticDir,
                parentStaticDir: parentStaticDir,
                renderedEntries: renderedEntries),
            new DirectoryCopyOptions(),
            tokens: null);

        Assert.DoesNotContain(plan.Items, item => item.Destination == "about.html");
        Assert.Contains(plan.Items, item =>
            item.Destination == "about/index.html" && item.Operation == AssetOutputOperation.Render);
    }

    [Fact]
    public async Task ExecuteAsync_PublishedDotfileStaticAndSkippedMediaPath_PreservesStaticOwner()
    {
        var root = CreateRoot();
        var staticDir = Path.Combine(root, "static");
        var mediaDir = Path.Combine(root, "media");
        Directory.CreateDirectory(Path.Combine(staticDir, "assets", "uploads", ".hidden"));
        Directory.CreateDirectory(Path.Combine(mediaDir, ".hidden"));
        File.WriteAllText(Path.Combine(staticDir, "assets", "uploads", ".hidden", "a.jpg"), "static");
        File.WriteAllText(Path.Combine(mediaDir, ".hidden", "a.jpg"), "media");

        var manifest = new BuildManifest();
        await new AssetPipeline().ExecuteAsync(CreateContext(
            root,
            manifest,
            staticDir: staticDir,
            mediaDir: mediaDir,
            publishDotFiles: true));

        Assert.Equal(
            "static",
            File.ReadAllText(Path.Combine(root, "dist", "assets", "uploads", ".hidden", "a.jpg")));
        Assert.Contains("assets/uploads/.hidden/a.jpg", manifest.Static.Keys);
        Assert.Empty(manifest.Media);
    }

    [Fact]
    public async Task ExecuteAsync_SkippedDotDirectory_DoesNotCreateManifestOwner()
    {
        var root = CreateRoot();
        var staticDir = Path.Combine(root, "static");
        Directory.CreateDirectory(Path.Combine(staticDir, ".hidden"));
        File.WriteAllText(Path.Combine(staticDir, ".hidden", "a.txt"), "hidden");
        var manifest = new BuildManifest();

        await new AssetPipeline().ExecuteAsync(CreateContext(root, manifest, staticDir: staticDir));

        Assert.False(File.Exists(Path.Combine(root, "dist", ".hidden", "a.txt")));
        Assert.Empty(manifest.Static);
    }

    [Fact]
    public async Task ExecuteAsync_IncrementalOwnerMovesFromStaticToTokens_DoesNotDeleteCurrentOutput()
    {
        var root = CreateRoot();
        var staticDir = Path.Combine(root, "static");
        var themeRoot = Path.Combine(root, "theme");
        Directory.CreateDirectory(staticDir);
        Directory.CreateDirectory(themeRoot);
        File.WriteAllText(Path.Combine(themeRoot, "tokens.yaml"), "colors:\n  primary: '#000'\n");
        var destination = Path.Combine(root, "dist", "assets", "css", "theme-tokens.css");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllText(destination, "stale-static");
        var manifest = new BuildManifest
        {
            Static = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["assets/css/theme-tokens.css"] = "old"
            }
        };

        await new AssetPipeline().ExecuteAsync(CreateContext(
            root,
            manifest,
            staticDir: staticDir,
            themeRoot: themeRoot,
            incrementalEnabled: true));

        Assert.True(File.Exists(destination));
        Assert.DoesNotContain("stale-static", File.ReadAllText(destination), StringComparison.Ordinal);
        Assert.Empty(manifest.Static);
    }

    [Fact]
    public async Task ExecuteAsync_IncrementalStaleFileBecomesCurrentDirectory_RemovesStructuralBlocker()
    {
        var root = CreateRoot();
        var assetsDir = Path.Combine(root, "assets");
        Directory.CreateDirectory(Path.Combine(assetsDir, "css"));
        File.WriteAllText(Path.Combine(assetsDir, "css", "main.css"), "asset");
        var stalePath = Path.Combine(root, "dist", "assets");
        File.WriteAllText(stalePath, "stale-file");
        var manifest = new BuildManifest
        {
            Static = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["assets"] = "old"
            }
        };

        await new AssetPipeline().ExecuteAsync(CreateContext(
            root,
            manifest,
            assetsDir: assetsDir,
            incrementalEnabled: true));

        Assert.Equal("asset", File.ReadAllText(Path.Combine(root, "dist", "assets", "css", "main.css")));
        Assert.Empty(manifest.Static);
        Assert.Contains("assets/css/main.css", manifest.Assets.Keys);
    }

    [Fact]
    public async Task ExecuteAsync_IncrementalStaleDescendantBecomesCurrentFile_RemovesStructuralBlocker()
    {
        var root = CreateRoot();
        var staticDir = Path.Combine(root, "static");
        Directory.CreateDirectory(staticDir);
        File.WriteAllText(Path.Combine(staticDir, "assets"), "current-file");
        var stalePath = Path.Combine(root, "dist", "assets", "css", "main.css");
        Directory.CreateDirectory(Path.GetDirectoryName(stalePath)!);
        File.WriteAllText(stalePath, "stale-asset");
        var manifest = new BuildManifest
        {
            Assets = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["assets/css/main.css"] = "old"
            }
        };

        await new AssetPipeline().ExecuteAsync(CreateContext(
            root,
            manifest,
            staticDir: staticDir,
            incrementalEnabled: true));

        Assert.Equal("current-file", File.ReadAllText(Path.Combine(root, "dist", "assets")));
        Assert.Empty(manifest.Assets);
        Assert.Contains("assets", manifest.Static.Keys);
    }

    [Fact]
    public async Task ExecuteAsync_IncrementalTokensRemoved_DeletesTrackedGeneratedOutput()
    {
        var root = CreateRoot();
        var themeRoot = Path.Combine(root, "theme");
        Directory.CreateDirectory(themeRoot);
        var tokensPath = Path.Combine(themeRoot, "tokens.yaml");
        File.WriteAllText(tokensPath, "colors:\n  primary: '#000'\n");
        var manifest = new BuildManifest();
        var context = CreateContext(
            root,
            manifest,
            themeRoot: themeRoot,
            incrementalEnabled: true);

        await new AssetPipeline().ExecuteAsync(context);
        var outputPath = Path.Combine(root, "dist", "assets", "css", "theme-tokens.css");
        Assert.True(File.Exists(outputPath));
        Assert.Contains("assets/css/theme-tokens.css", manifest.Assets.Keys);

        File.Delete(tokensPath);
        await new AssetPipeline().ExecuteAsync(context);

        Assert.False(File.Exists(outputPath));
        Assert.DoesNotContain("assets/css/theme-tokens.css", manifest.Assets.Keys);
    }

    [Fact]
    public async Task ExecuteAsync_NoCollisionRepeatedBuild_PreservesOutputAndManifestOwners()
    {
        var root = CreateRoot();
        var staticDir = Path.Combine(root, "static");
        var assetsDir = Path.Combine(root, "assets");
        var mediaDir = Path.Combine(root, "media");
        Directory.CreateDirectory(staticDir);
        Directory.CreateDirectory(Path.Combine(assetsDir, "css"));
        Directory.CreateDirectory(mediaDir);
        File.WriteAllText(Path.Combine(staticDir, "robots.txt"), "robots");
        File.WriteAllText(Path.Combine(assetsDir, "css", "main.css"), "asset");
        File.WriteAllText(Path.Combine(mediaDir, "photo.jpg"), "media");
        var manifest = new BuildManifest();
        var context = CreateContext(
            root,
            manifest,
            staticDir: staticDir,
            assetsDir: assetsDir,
            mediaDir: mediaDir,
            incrementalEnabled: true);

        await new AssetPipeline().ExecuteAsync(context);
        var firstStatic = manifest.Static.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var firstAssets = manifest.Assets.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var firstMedia = manifest.Media.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var firstContents = Directory.EnumerateFiles(Path.Combine(root, "dist"), "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => BuildPathUtils.NormalizeRelPath(Path.GetRelativePath(Path.Combine(root, "dist"), path)),
                File.ReadAllText,
                StringComparer.Ordinal);

        await new AssetPipeline().ExecuteAsync(context);

        Assert.Equal(firstStatic.OrderBy(pair => pair.Key), manifest.Static.OrderBy(pair => pair.Key));
        Assert.Equal(firstAssets.OrderBy(pair => pair.Key), manifest.Assets.OrderBy(pair => pair.Key));
        Assert.Equal(firstMedia.OrderBy(pair => pair.Key), manifest.Media.OrderBy(pair => pair.Key));
        var secondContents = Directory.EnumerateFiles(Path.Combine(root, "dist"), "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => BuildPathUtils.NormalizeRelPath(Path.GetRelativePath(Path.Combine(root, "dist"), path)),
                File.ReadAllText,
                StringComparer.Ordinal);
        Assert.Equal(firstContents.OrderBy(pair => pair.Key), secondContents.OrderBy(pair => pair.Key));
    }

    [Fact]
    public async Task ExecuteAsync_CopiesStaticAndAssetsAndGeneratesTokensAndAggregatesMetrics()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), "bukit-asset-pipeline-tests", Guid.NewGuid().ToString("N"));
        var outputDir = Path.Combine(rootDir, "dist");
        var staticDir = Path.Combine(rootDir, "static");
        var assetsDir = Path.Combine(rootDir, "assets");
        var themeRoot = Path.Combine(rootDir, "themes", "starter");
        var mediaDir = Path.Combine(rootDir, ".cache", "media");

        Directory.CreateDirectory(staticDir);
        Directory.CreateDirectory(assetsDir);
        Directory.CreateDirectory(themeRoot);
        Directory.CreateDirectory(mediaDir);
        Directory.CreateDirectory(outputDir);

        File.WriteAllText(Path.Combine(staticDir, "robots.txt"), "User-agent: *\nDisallow:");
        Directory.CreateDirectory(Path.Combine(assetsDir, "css"));
        File.WriteAllText(Path.Combine(assetsDir, "css", "main.css"), "body { color: red; }");
        File.WriteAllText(Path.Combine(mediaDir, "photo.jpg"), "fake-image");

        var tokensYaml = "colors:\n  primary: \"#000\"\nfont:\n  base: Arial\n";
        File.WriteAllText(Path.Combine(themeRoot, "tokens.yaml"), tokensYaml);

        var manifest = new BuildManifest();
        var logger = new RecordingLogger();
        var pipeline = new AssetPipeline();

        var result = await pipeline.ExecuteAsync(new AssetPipelineContext(
            StaticDir: staticDir,
            ParentStaticDir: null,
            AssetsDir: assetsDir,
            ParentAssetsDir: null,
            MediaDownloadDir: mediaDir,
            ThemeRoot: themeRoot,
            ParentThemeRoot: null,
            OutputDir: outputDir,
            Manifest: manifest,
            IncrementalEnabled: false,
            ScssConfig: null,
            ImageConfig: null,
            Logger: logger,
            PublishDotFiles: false,
            FollowSymlinks: false),
            CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(outputDir, "robots.txt")), "robots.txt not found");
        Assert.True(File.Exists(Path.Combine(outputDir, "assets", "css", "main.css")), "assets/css/main.css not found");
        Assert.True(File.Exists(Path.Combine(outputDir, "assets", "css", "theme-tokens.css")), "theme tokens not found");
        Assert.True(File.Exists(Path.Combine(outputDir, "assets", "uploads", "photo.jpg")), "media photo not found");
        Assert.Contains(logger.Infos, m => m.StartsWith("event=tokens.generated"));
        Assert.True(result.StageMetrics.DurationsMs.ContainsKey("staticSync"));
        Assert.True(result.StageMetrics.DurationsMs.ContainsKey("assetsSync"));
        Assert.True(result.StageMetrics.DurationsMs.ContainsKey("tokensGen"));
        Assert.True(result.StageMetrics.DurationsMs.ContainsKey("mediaCopy"));
        Assert.Empty(manifest.Static.Keys.Intersect(manifest.Assets.Keys, StringComparer.Ordinal));
        Assert.Empty(manifest.Static.Keys.Intersect(manifest.Media.Keys, StringComparer.Ordinal));
        Assert.Empty(manifest.Assets.Keys.Intersect(manifest.Media.Keys, StringComparer.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_WithParentStaticSyncsParentFirst()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), "bukit-asset-pipeline-tests", Guid.NewGuid().ToString("N"));
        var outputDir = Path.Combine(rootDir, "dist");
        var parentStaticDir = Path.Combine(rootDir, "themes", "parent", "static");

        Directory.CreateDirectory(parentStaticDir);
        Directory.CreateDirectory(outputDir);

        File.WriteAllText(Path.Combine(parentStaticDir, "favicon.ico"), "ico");

        var manifest = new BuildManifest();
        var logger = new RecordingLogger();
        var pipeline = new AssetPipeline();

        await pipeline.ExecuteAsync(new AssetPipelineContext(
            StaticDir: null,
            ParentStaticDir: parentStaticDir,
            AssetsDir: null,
            ParentAssetsDir: null,
            MediaDownloadDir: null,
            ThemeRoot: null,
            ParentThemeRoot: null,
            OutputDir: outputDir,
            Manifest: manifest,
            IncrementalEnabled: false,
            ScssConfig: null,
            ImageConfig: null,
            Logger: logger,
            PublishDotFiles: false,
            FollowSymlinks: false),
            CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(outputDir, "favicon.ico")));
    }

    [Fact]
    public async Task ExecuteAsync_WithCanceledToken_DoesNotRunAssetWork()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), "bukit-asset-pipeline-tests", Guid.NewGuid().ToString("N"));
        var outputDir = Path.Combine(rootDir, "dist");
        var staticDir = Path.Combine(rootDir, "static");

        Directory.CreateDirectory(staticDir);
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(staticDir, "robots.txt"), "User-agent: *");

        var pipeline = new AssetPipeline();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pipeline.ExecuteAsync(new AssetPipelineContext(
            StaticDir: staticDir,
            ParentStaticDir: null,
            AssetsDir: null,
            ParentAssetsDir: null,
            MediaDownloadDir: null,
            ThemeRoot: null,
            ParentThemeRoot: null,
            OutputDir: outputDir,
            Manifest: new BuildManifest(),
            IncrementalEnabled: false,
            ScssConfig: null,
            ImageConfig: null,
            Logger: new RecordingLogger(),
            PublishDotFiles: false,
            FollowSymlinks: false),
            cts.Token));

        Assert.False(File.Exists(Path.Combine(outputDir, "robots.txt")));
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Infos { get; } = new();

        public void Debug(string message) { }
        public void Info(string message) { Infos.Add(message); }
        public void Warn(string message) { }
        public void Error(string message) { }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-asset-collision-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "dist"));
        return root;
    }

    private static AssetPipelineContext CreateContext(
        string root,
        BuildManifest manifest,
        string? staticDir = null,
        string? parentStaticDir = null,
        string? assetsDir = null,
        string? parentAssetsDir = null,
        string? mediaDir = null,
        string? themeRoot = null,
        bool followSymlinks = false,
        bool publishDotFiles = false,
        bool incrementalEnabled = false,
        IReadOnlyList<RenderEntry>? renderedEntries = null)
        => new(
            StaticDir: staticDir,
            ParentStaticDir: parentStaticDir,
            AssetsDir: assetsDir,
            ParentAssetsDir: parentAssetsDir,
            MediaDownloadDir: mediaDir,
            ThemeRoot: themeRoot,
            ParentThemeRoot: null,
            OutputDir: Path.Combine(root, "dist"),
            Manifest: manifest,
            IncrementalEnabled: incrementalEnabled,
            ScssConfig: null,
            ImageConfig: null,
            Logger: new RecordingLogger(),
            PublishDotFiles: publishDotFiles,
            FollowSymlinks: followSymlinks,
            RenderEntries: renderedEntries);
}
