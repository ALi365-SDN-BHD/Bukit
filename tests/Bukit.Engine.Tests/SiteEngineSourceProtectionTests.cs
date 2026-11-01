using Bukit.Config;
using Bukit.Content;
using Bukit.Content.Markdown;
using Bukit.Content.Notion;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;
using Xunit;
using Xunit.Sdk;

namespace Bukit.Engine.Tests;

public sealed class SiteEngineSourceProtectionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bukit-source-protection-" + Guid.NewGuid().ToString("N"));
    private readonly ILogger _logger = new ConsoleLogger(LogLevel.Error);

    public SiteEngineSourceProtectionTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, true);

    private static string HashTree(string directory)
        => string.Join("\n", Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => Path.GetRelativePath(directory, path) + ":" +
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))));

    private BuildOptions Options(string outputDir) => new()
    {
        OutputDir = outputDir,
        SiteTitle = "Source protection",
        Clean = false
    };

    [Fact]
    public async Task PublicEntry_InMemoryProviderWithContentNamedOutputBuildsNormally()
    {
        var output = Path.Combine(_root, "content");
        Directory.CreateDirectory(Path.Combine(_root, "layouts", "pages"));
        File.WriteAllText(Path.Combine(_root, "layouts", "pages", "index.html"), "{{ page.content }}");
        File.WriteAllText(Path.Combine(_root, "layouts", "pages", "page.html"), "{{ page.content }}");
        await new SiteEngine(_logger).BuildAsync(new EmptyProvider(), Options(output), CancellationToken.None);
        Assert.True(File.Exists(Path.Combine(output, ".bukit-output-marker")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root, ".bukit-txn-*"));
    }

    [Fact]
    public async Task PublicEntry_InMemoryProviderWithContentNamedOutputIsNotRejected()
    {
        var output = Path.Combine(_root, "content");
        await Assert.ThrowsAsync<ProviderInvokedException>(() =>
            new SiteEngine(_logger).BuildAsync(new ThrowingProvider(), Options(output), CancellationToken.None));
        Assert.False(File.Exists(Path.Combine(output, ".bukit-output-marker")));
    }

    [Fact]
    public async Task PublicEntry_RejectsInjectedMarkdownRootEqualToOutputBeforeWrites()
    {
        var articles = Path.Combine(_root, "articles");
        Directory.CreateDirectory(articles);
        File.WriteAllText(Path.Combine(articles, "draft.md"), "---\ntitle: Draft\n---\nbody");
        var before = HashTree(articles);
        var provider = new MarkdownFolderProvider(new MarkdownFolderProviderOptions(articles));

        var error = await Assert.ThrowsAsync<ConfigException>(() =>
            new SiteEngine(_logger).BuildAsync(provider, Options(articles), CancellationToken.None));

        Assert.Equal(DiagnosticCode.BuildOutputUnsafe, error.Code);
        Assert.Equal(before, HashTree(articles));
        Assert.False(File.Exists(Path.Combine(articles, ".bukit-output-marker")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root, ".bukit-txn-*"));
    }

    [Fact]
    public async Task PublicEntry_RejectsNestedCompositeChildSourceConflict()
    {
        var conflict = Path.Combine(_root, "nested-output");
        Directory.CreateDirectory(conflict);
        var inner = new CompositeContentProvider(
        [
            ("inner", "content", new MarkdownFolderProvider(new MarkdownFolderProviderOptions(conflict)))
        ]);
        var composite = new CompositeContentProvider(
        [
            ("pages", "content", new MarkdownFolderProvider(new MarkdownFolderProviderOptions(Path.Combine(_root, "pages")))),
            ("wrapped", "content", inner)
        ]);

        var error = await Assert.ThrowsAsync<ConfigException>(() =>
            new SiteEngine(_logger).BuildAsync(composite, Options(conflict), CancellationToken.None));

        Assert.Equal(DiagnosticCode.BuildOutputUnsafe, error.Code);
    }

    [Fact]
    public async Task PublicEntry_RejectsInjectedNotionWritableCacheOverlappingMarkdownRoot()
    {
        var articles = Path.Combine(_root, "articles");
        Directory.CreateDirectory(articles);
        var composite = new CompositeContentProvider(
        [
            ("pages", "content", new MarkdownFolderProvider(new MarkdownFolderProviderOptions(articles))),
            ("notion", "content", new NotionContentSource(new NotionContentSourceOptions
            {
                DatabaseId = "database",
                Token = "token",
                CacheMode = "readwrite",
                CacheDir = Path.Combine(articles, "notion-cache")
            }))
        ]);

        var error = await Assert.ThrowsAsync<ConfigException>(() =>
            new SiteEngine(_logger).BuildAsync(composite, Options(Path.Combine(_root, "public")), CancellationToken.None));

        Assert.Equal(DiagnosticCode.BuildOutputUnsafe, error.Code);
        Assert.False(Directory.Exists(Path.Combine(articles, "notion-cache", "pages")));
        Assert.False(Directory.Exists(Path.Combine(articles, "notion-cache", "relations")));
    }

    [Theory]
    [InlineData("off", null)]
    [InlineData("readonly", "cache")]
    [InlineData("readwrite", null)]
    [InlineData("readwrite", "")]
    public void Inspection_DoesNotInventWritableCacheForNonWritableModes(string mode, string? cacheDir)
    {
        var source = new NotionContentSource(new NotionContentSourceOptions
        {
            DatabaseId = "database",
            Token = "token",
            CacheMode = mode,
            CacheDir = cacheDir
        });

        var (roots, caches) = BuildTransaction.InspectContentProvider(source);

        Assert.Empty(roots);
        Assert.Empty(caches);
    }

    [Fact]
    public void Inspection_ResolvesWritableCacheWithProviderProcessSemantics()
    {
        var absolute = Path.Combine(_root, "cache-main");
        var absoluteSource = new NotionContentSource(new NotionContentSourceOptions
        {
            DatabaseId = "database",
            Token = "token",
            CacheMode = "readwrite",
            CacheDir = absolute
        });
        var (_, absoluteCaches) = BuildTransaction.InspectContentProvider(absoluteSource);
        Assert.Equal([absolute, Path.Combine(absolute, "pages"), Path.Combine(absolute, "relations")], absoluteCaches);

        var relativeSource = new NotionContentSource(new NotionContentSourceOptions
        {
            DatabaseId = "database",
            Token = "token",
            CacheMode = "readwrite",
            CacheDir = "relative-notion-cache"
        });
        var (_, relativeCaches) = BuildTransaction.InspectContentProvider(relativeSource);
        var relativeRoot = Path.GetFullPath("relative-notion-cache");
        Assert.Equal([relativeRoot, Path.Combine(relativeRoot, "pages"), Path.Combine(relativeRoot, "relations")], relativeCaches);
    }

    [Fact]
    public void SharedCheck_RejectsAliasedNotionCacheAndAcceptsIndependentCache()
    {
        var content = Path.Combine(_root, "content");
        var independent = Path.Combine(_root, "cache", "notion");
        Directory.CreateDirectory(content);
        OutputDirectoryCleaner.EnsureNoSourceOverlap([content], [independent]);

        var error = Assert.Throws<ConfigException>(() =>
            OutputDirectoryCleaner.EnsureNoSourceOverlap([content], [Path.Combine(content, "notion-cache")]));
        Assert.Equal(DiagnosticCode.BuildOutputUnsafe, error.Code);
    }

    [Fact]
    public async Task InjectedNestedCompositeWithIndependentRootsBuildsAndKeepsSources()
    {
        Directory.CreateDirectory(Path.Combine(_root, "layouts", "pages"));
        File.WriteAllText(Path.Combine(_root, "layouts", "pages", "page.html"), "{{ page.content }}");
        File.WriteAllText(Path.Combine(_root, "layouts", "pages", "index.html"), "{{ page.content }}");
        var first = Path.Combine(_root, "articles-a");
        var second = Path.Combine(_root, "articles-b");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        File.WriteAllText(Path.Combine(first, "a.md"), "---\ntitle: A\ncollection: page\n---\nAlpha body");
        File.WriteAllText(Path.Combine(second, "b.md"), "---\ntitle: B\ncollection: page\n---\nBeta body");
        var nested = new CompositeContentProvider(
        [
            ("inner", "content", new MarkdownFolderProvider(new MarkdownFolderProviderOptions(second)))
        ]);
        var composite = new CompositeContentProvider(
        [
            ("first", "content", new MarkdownFolderProvider(new MarkdownFolderProviderOptions(first))),
            ("wrapped", "content", nested)
        ]);
        var config = new AppConfig
        {
            Site = new()
            {
                Name = "composite",
                Title = "Composite",
                BaseUrl = "/",
                Language = "en",
                Seo = new SeoConfig { Enabled = false },
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["page"] = new() { Permalink = "/{slug}/", Template = "pages/page.html" }
                }
            },
            Content = ContentConfigFactory.SingleMarkdown("articles-a"),
            Build = new() { Output = "public", Clean = false },
            Theme = new ThemeConfig { Layouts = "layouts" }
        };

        await new SiteEngine(_logger, new SingleProviderFactory(composite), new DefaultSearchIndexBuilder())
            .BuildAsync(config, _root, new ConfigOverrides { Incremental = false });

        Assert.True(File.Exists(Path.Combine(_root, "public", ".bukit-output-marker")));
        Assert.Contains("Alpha body", File.ReadAllText(Path.Combine(_root, "public", "a", "index.html")), StringComparison.Ordinal);
        Assert.Contains("Beta body", File.ReadAllText(Path.Combine(_root, "public", "b", "index.html")), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(first, "a.md")));
        Assert.True(File.Exists(Path.Combine(second, "b.md")));
    }

    [Fact]
    public async Task MultipleMarkdownSourcesWithIndependentOutputBuildAndKeepSources()
    {
        Directory.CreateDirectory(Path.Combine(_root, "layouts", "pages"));
        File.WriteAllText(Path.Combine(_root, "layouts", "pages", "page.html"), "{{ page.content }}");
        File.WriteAllText(Path.Combine(_root, "layouts", "pages", "index.html"), "{{ page.content }}");
        Directory.CreateDirectory(Path.Combine(_root, "content-a"));
        Directory.CreateDirectory(Path.Combine(_root, "content-b"));
        File.WriteAllText(Path.Combine(_root, "content-a", "a.md"), "---\ntitle: A\ncollection: page\n---\nAlpha body");
        File.WriteAllText(Path.Combine(_root, "content-b", "b.md"), "---\ntitle: B\ncollection: page\n---\nBeta body");
        var config = new AppConfig
        {
            Site = new()
            {
                Name = "multi",
                Title = "Multi",
                BaseUrl = "/",
                Language = "en",
                Seo = new SeoConfig { Enabled = false },
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["page"] = new() { Permalink = "/{slug}/", Template = "pages/page.html" }
                }
            },
            Content = ContentConfigFactory.FromSources(
            [
                new ContentSourceConfig { Type = "markdown", Name = "a", Collection = "page", Markdown = new() { Dir = "content-a" } },
                new ContentSourceConfig { Type = "markdown", Name = "b", Collection = "page", Markdown = new() { Dir = "content-b" } }
            ]),
            Build = new() { Output = "public", Clean = false },
            Theme = new ThemeConfig { Layouts = "layouts" }
        };

        await new SiteEngine(_logger).BuildAsync(config, _root, new ConfigOverrides { Incremental = false });

        Assert.Contains("Alpha body", File.ReadAllText(Path.Combine(_root, "public", "a", "index.html")), StringComparison.Ordinal);
        Assert.Contains("Beta body", File.ReadAllText(Path.Combine(_root, "public", "b", "index.html")), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_root, "content-a", "a.md")));
        Assert.True(File.Exists(Path.Combine(_root, "content-b", "b.md")));
    }

    [Fact]
    public async Task PublicEntry_SafeNestedCompositeIsNotRejected()
    {
        var output = Path.Combine(_root, "public");
        Directory.CreateDirectory(Path.Combine(_root, "layouts", "pages"));
        File.WriteAllText(Path.Combine(_root, "layouts", "pages", "index.html"), "{{ page.content }}");
        File.WriteAllText(Path.Combine(_root, "layouts", "pages", "page.html"), "{{ page.content }}");
        var first = Path.Combine(_root, "empty-a");
        var second = Path.Combine(_root, "empty-b");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        var nested = new CompositeContentProvider(
        [
            ("inner", "content", new MarkdownFolderProvider(new MarkdownFolderProviderOptions(second)))
        ]);
        var composite = new CompositeContentProvider(
        [
            ("first", "content", new MarkdownFolderProvider(new MarkdownFolderProviderOptions(first))),
            ("wrapped", "content", nested)
        ]);

        await new SiteEngine(_logger).BuildAsync(composite, Options(output), CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(output, ".bukit-output-marker")));
        Assert.True(Directory.Exists(first));
        Assert.True(Directory.Exists(second));
    }

    [Theory]
    [InlineData("equal")]
    [InlineData("cache-parent")]
    [InlineData("cache-child")]
    public async Task PublicEntry_RejectsNotionWritableCacheContainmentWithMarkdownRoot(string kind)
    {
        var articles = Path.Combine(_root, "articles");
        Directory.CreateDirectory(articles);
        File.WriteAllText(Path.Combine(articles, "draft.md"), "---\ntitle: Draft\n---\nbody");
        File.WriteAllBytes(Path.Combine(articles, "sentinel.bin"), [1, 2, 3]);
        var cacheRoot = kind switch
        {
            "equal" => articles,
            "cache-parent" => _root,
            _ => Path.Combine(articles, "notion-cache")
        };
        if (kind == "cache-child")
        {
            Directory.CreateDirectory(Path.Combine(cacheRoot, "pages"));
            File.WriteAllText(Path.Combine(cacheRoot, "pages", "existing.json"), "{\"version\":1}");
        }

        var before = HashTree(articles);
        var composite = new CompositeContentProvider(
        [
            ("pages", "content", new MarkdownFolderProvider(new MarkdownFolderProviderOptions(articles))),
            ("notion", "content", new NotionContentSource(new NotionContentSourceOptions
            {
                DatabaseId = "database",
                Token = "token",
                CacheMode = "readwrite",
                CacheDir = cacheRoot
            }))
        ]);

        var error = await Assert.ThrowsAsync<ConfigException>(() =>
            new SiteEngine(_logger).BuildAsync(composite, Options(Path.Combine(_root, "public")), CancellationToken.None));

        Assert.Equal(DiagnosticCode.BuildOutputUnsafe, error.Code);
        Assert.Equal(before, HashTree(articles));
        Assert.False(Directory.Exists(Path.Combine(articles, "public")));
        Assert.False(File.Exists(Path.Combine(_root, "public", ".bukit-output-marker")));
        if (kind == "cache-child")
        {
            Assert.Equal("{\"version\":1}", File.ReadAllText(Path.Combine(cacheRoot, "pages", "existing.json")));
        }
    }

    [Fact]
    public async Task PublicEntry_RejectsRelativeNotionCacheAliasIntoMarkdownRoot()
    {
        var articles = Path.Combine(_root, "articles");
        Directory.CreateDirectory(articles);
        File.WriteAllText(Path.Combine(articles, "draft.md"), "---\ntitle: Draft\n---\nbody");
        var relativeCache = Path.GetRelativePath(Environment.CurrentDirectory, articles);
        var before = HashTree(articles);
        var composite = new CompositeContentProvider(
        [
            ("pages", "content", new MarkdownFolderProvider(new MarkdownFolderProviderOptions(articles))),
            ("notion", "content", new NotionContentSource(new NotionContentSourceOptions
            {
                DatabaseId = "database",
                Token = "token",
                CacheMode = "readwrite",
                CacheDir = relativeCache
            }))
        ]);

        var error = await Assert.ThrowsAsync<ConfigException>(() =>
            new SiteEngine(_logger).BuildAsync(composite, Options(Path.Combine(_root, "public")), CancellationToken.None));

        Assert.Equal(DiagnosticCode.BuildOutputUnsafe, error.Code);
        Assert.Equal(before, HashTree(articles));
        Assert.False(Directory.Exists(Path.Combine(articles, "pages")));
        Assert.False(Directory.Exists(Path.Combine(articles, "relations")));
    }

    [Theory]
    [InlineData("pages")]
    [InlineData("relations")]
    public async Task PublicEntry_RejectsNotionCacheSubdirectorySymlinkIntoMarkdownRoot(string subdirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var articles = Path.Combine(_root, "articles");
        Directory.CreateDirectory(articles);
        File.WriteAllText(Path.Combine(articles, "draft.md"), "---\ntitle: Draft\n---\nbody");
        var cacheRoot = Path.Combine(_root, "safe-cache");
        Directory.CreateDirectory(cacheRoot);
        var link = Path.Combine(cacheRoot, subdirectory);
        try
        {
            Directory.CreateSymbolicLink(link, articles);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            throw SkipException.ForSkip($"Directory symlinks are unavailable: {ex.GetType().Name}");
        }

        var before = HashTree(articles);
        var composite = new CompositeContentProvider(
        [
            ("pages", "content", new MarkdownFolderProvider(new MarkdownFolderProviderOptions(articles))),
            ("notion", "content", new NotionContentSource(new NotionContentSourceOptions
            {
                DatabaseId = "database",
                Token = "token",
                CacheMode = "readwrite",
                CacheDir = cacheRoot
            }))
        ]);

        var error = await Assert.ThrowsAsync<ConfigException>(() =>
            new SiteEngine(_logger).BuildAsync(composite, Options(Path.Combine(_root, "public")), CancellationToken.None));

        Assert.Equal(DiagnosticCode.BuildOutputUnsafe, error.Code);
        Assert.Equal(before, HashTree(articles));
        Assert.Empty(Directory.EnumerateFiles(articles, "*.json", SearchOption.AllDirectories));
        Assert.False(File.Exists(Path.Combine(articles, ".bukit-output-marker")));
    }

    private sealed class SingleProviderFactory(IContentProvider provider) : IContentProviderFactory
    {
        public IContentProvider Create(AppConfig config, string rootDir, bool isCi, ILogger logger) => provider;

        public Task<RawContentLoadResult> LocalizeContentImagesAsync(
            RawContentLoadResult result, MediaConfig media, string rootDir, string cacheDir, ILogger logger, CancellationToken cancellationToken)
            => Task.FromResult(result);
    }

    private sealed class ThrowingProvider : IContentProvider
    {
        public Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
            => throw new ProviderInvokedException();
    }

    private sealed class EmptyProvider : IContentProvider
    {
        public Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new RawContentLoadResult([], new EmptyBodyStore()));
    }

    private sealed class EmptyBodyStore : IContentBodyStore
    {
        public Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
            => Task.FromResult(new ContentBody(string.Empty));
    }

    private sealed class ProviderInvokedException : Exception;
}
