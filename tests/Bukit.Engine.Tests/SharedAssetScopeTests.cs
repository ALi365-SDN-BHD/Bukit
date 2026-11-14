using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class SharedAssetScopeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"bukit-shared-assets-{Guid.NewGuid():N}");

    public SharedAssetScopeTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void DefaultScope_IncludesAssetsAndExcludesMedia()
    {
        var scope = Create(Config());

        Assert.NotNull(scope);
        Assert.True(scope.Contains("assets/css/a.css"));
        Assert.True(scope.Contains("assets/images/a.jpg"));
        Assert.False(scope.Contains("assets/uploads/x.jpg"));
        Assert.True(scope.Contains("assets/uploads2/x.jpg"));
        Assert.False(scope.Contains("fonts/a.woff2"));
    }

    [Fact]
    public void CustomPaths_ReplaceDefaults()
    {
        var scope = Create(Config(paths: ["fonts/"]));

        Assert.NotNull(scope);
        Assert.True(scope.Contains("fonts/a.woff2"));
        Assert.False(scope.Contains("assets/css/a.css"));
    }

    [Fact]
    public void CustomPaths_AreDeduplicatedAndOrderedForHash()
    {
        var scope = Create(Config(paths: ["b/", "a/", "b/"]));

        Assert.NotNull(scope);
        Assert.Equal(["a/", "assets/uploads/", "b/"], scope.CanonicalPrefixes);
    }

    [Fact]
    public void MediaUrlBase_MovesTheExcludedPrefix()
    {
        var scope = Create(Config(mediaUrlBase: "/media"));

        Assert.NotNull(scope);
        Assert.True(scope.Contains("assets/uploads/x.jpg"));
        Assert.False(scope.Contains("media/x.jpg"));
    }

    [Fact]
    public void ScssOutput_IsIncludedOnlyWhenScssIsEnabled()
    {
        var disabled = Create(Config(scssEnabled: false, scssOutput: "styles"));
        var enabled = Create(Config(scssEnabled: true, scssOutput: "styles"));

        Assert.NotNull(disabled);
        Assert.NotNull(enabled);
        Assert.False(disabled.Contains("styles/a.css"));
        Assert.True(enabled.Contains("styles/a.css"));
    }

    [Fact]
    public void ScssOutputAtRoot_IsOmittedAndWarnedOnce()
    {
        var logger = new ListLogger();
        var scope = Create(Config(scssEnabled: true, scssOutput: "."), logger);

        Assert.NotNull(scope);
        Assert.False(scope.Contains("a.css"));
        Assert.Single(logger.Warnings);
        Assert.Contains("shared_assets_path_ignored", logger.Warnings[0], StringComparison.Ordinal);

        var withoutLogger = Create(Config(scssEnabled: true, scssOutput: "."), logger: null);
        Assert.NotNull(withoutLogger);
    }

    [Fact]
    public void SingleLanguagePrefixedLayoutAndDisabled_ReturnNull()
    {
        Assert.Null(Create(Config(), languages: ["en"]));
        Assert.Null(Create(Config(defaultLanguageInSubdir: true)));
        Assert.Null(Create(Config(enabled: false)));
    }

    [Fact]
    public void ContainsSitePath_StripsQueryAndFragmentWithoutDecoding()
    {
        var scope = Create(Config());

        Assert.NotNull(scope);
        Assert.True(scope.ContainsSitePath("/assets/og.png?v=2"));
        Assert.True(scope.ContainsSitePath("/assets/og.png#hero"));
        Assert.False(scope.ContainsSitePath("/assets/uploads/x.jpg?v=1"));
    }

    [Fact]
    public void OutputRootAndRootBaseUrl_AreNormalized()
    {
        var scope = Create(Config(baseUrl: "/repo/"));

        Assert.NotNull(scope);
        Assert.Equal(Path.GetFullPath(_root), scope.OutputRoot);
        Assert.Equal("/repo", scope.RootBaseUrl);
    }

    [Fact]
    public void CaseDeduplication_UsesTheOutputComparer()
    {
        var scope = Create(Config(paths: ["assets/", "ASSETS/"]));

        Assert.NotNull(scope);
        var expected = scope.Comparer.Equals(StringComparer.OrdinalIgnoreCase) ? 2 : 3;
        Assert.Equal(expected, scope.CanonicalPrefixes.Count);
    }

    private SharedAssetScope? Create(AppConfig config, ILogger? logger = null, IReadOnlyList<string>? languages = null)
        => SharedAssetScope.TryCreate(
            config,
            languages ?? ["en", "zh-CN"],
            _root,
            logger);

    private static AppConfig Config(
        bool enabled = true,
        IReadOnlyList<string>? paths = null,
        string mediaUrlBase = "/assets/uploads",
        bool scssEnabled = false,
        string scssOutput = "assets",
        bool defaultLanguageInSubdir = false,
        string baseUrl = "/")
        => new()
        {
            Site = new SiteConfig
            {
                Name = "shared-assets",
                Title = "Shared Assets",
                BaseUrl = baseUrl,
                Languages = ["en", "zh-CN"],
                DefaultLanguageInSubdir = defaultLanguageInSubdir,
                SharedAssets = new SharedAssetsConfig { Enabled = enabled, Paths = paths }
            },
            Content = new ContentConfig { Media = new MediaConfig { UrlBase = mediaUrlBase } },
            Theme = new ThemeConfig
            {
                Scss = new ScssConfig { Enabled = scssEnabled, OutputDir = scssOutput }
            }
        };

    private sealed class ListLogger : ILogger
    {
        internal List<string> Warnings { get; } = [];

        public void Debug(string message)
        {
        }

        public void Info(string message)
        {
        }

        public void Warn(string message) => Warnings.Add(message);

        public void Error(string message)
        {
        }
    }
}
