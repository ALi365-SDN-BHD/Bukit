using Bukit.Labs.Cli.Commands;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

[Collection(IntentApplierCollection.Name)]
public sealed class IntentApplierTests : IDisposable
{
    private readonly string _rootDir;

    public IntentApplierTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-labs-intent-applier-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
    }

    [Fact]
    public void Apply_MarkdownIntentWithAbsoluteDirUnderSites_WritesRelativeSourcesAndThemeParams()
    {
        using var scope = new CurrentDirectoryScope(_rootDir);

        Directory.CreateDirectory(Path.Combine(_rootDir, "sites"));
        Directory.CreateDirectory(Path.Combine(_rootDir, "themes", "starter"));

        var contentDir = Path.Combine(_rootDir, "content", "blog");
        Directory.CreateDirectory(contentDir);

        var intentPath = WriteIntent(
            $$"""
            site:
              name: demo
              title: Demo Site
              base_url: /
              url: https://example.com
              language: en
            languages:
              default: en
              supported:
                - en
                - zh-CN
            content:
              kind: markdown
              markdown:
                dir: {{NormalizePath(contentDir)}}
            theme:
              name: starter
              params:
                feature_enabled: true
                max_items: 3
                pi: 3.14
                menu:
                  - home
                  - docs
                palette:
                  accent: teal
                  dark: false
            """);

        var outPath = Path.Combine(_rootDir, "sites", "demo.yaml");

        var (validation, _) = IntentApplier.Apply(intentPath, outPath);

        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        Assert.True(File.Exists(outPath));

        var root = LoadYamlRoot(outPath);
        var site = GetMapping(root, "site");
        Assert.Equal("demo", GetScalar(site, "name"));
        Assert.Equal("Demo Site", GetScalar(site, "title"));
        Assert.Equal("/", GetScalar(site, "baseUrl"));
        Assert.Equal("https://example.com", GetScalar(site, "url"));
        Assert.Equal("en", GetScalar(site, "language"));
        Assert.Equal("en", GetScalar(site, "defaultLanguage"));

        var languages = GetSequence(site, "languages");
        Assert.Equal(["en", "zh-CN"], languages.Children.Select(GetScalarValue));

        var content = GetMapping(root, "content");
        var sources = GetSequence(content, "sources");
        Assert.Equal(3, sources.Children.Count);

        foreach (var expectedName in new[] { "post", "page", "about" })
        {
            var source = GetSourceByName(sources, expectedName);
            Assert.Equal("markdown", GetScalar(source, "type"));
            Assert.Equal(expectedName, GetScalar(source, "collection"));
            Assert.Equal("content/blog", GetScalar(GetMapping(source, "markdown"), "dir"));
        }

        var theme = GetMapping(root, "theme");
        Assert.Equal("starter", GetScalar(theme, "name"));
        var themeParams = GetMapping(theme, "params");
        Assert.Equal("true", GetScalar(themeParams, "feature_enabled"));
        Assert.Equal("3", GetScalar(themeParams, "max_items"));
        Assert.Equal("3.14", GetScalar(themeParams, "pi"));
        Assert.Equal(["home", "docs"], GetSequence(themeParams, "menu").Children.Select(GetScalarValue));
        var palette = GetMapping(themeParams, "palette");
        Assert.Equal("teal", GetScalar(palette, "accent"));
        Assert.Equal("false", GetScalar(palette, "dark"));

        var build = GetMapping(root, "build");
        Assert.Equal("dist", GetScalar(build, "output"));
        Assert.Equal("true", GetScalar(build, "clean"));

        var logging = GetMapping(root, "logging");
        Assert.Equal("info", GetScalar(logging, "level"));
    }

    [Fact]
    public void Apply_WhenOutputOutsideSites_UsesOutputDirectoryAsRootDir()
    {
        using var scope = new CurrentDirectoryScope(_rootDir);

        var generatedDir = Path.Combine(_rootDir, "generated");
        Directory.CreateDirectory(Path.Combine(generatedDir, "themes", "starter"));

        var contentDir = Path.Combine(generatedDir, "content");
        Directory.CreateDirectory(contentDir);

        var intentPath = WriteIntent(
            $$"""
            site:
              name: demo
              title: Demo Site
              base_url: /
            content:
              kind: markdown
              markdown:
                dir: {{NormalizePath(contentDir)}}
            theme:
              name: starter
            """);

        var outPath = Path.Combine(generatedDir, "site.yaml");

        var (validation, _) = IntentApplier.Apply(intentPath, outPath);

        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        Assert.True(File.Exists(outPath));

        var root = LoadYamlRoot(outPath);
        var sources = GetSequence(GetMapping(root, "content"), "sources");
        var postSource = GetSourceByName(sources, "post");
        Assert.Equal("content", GetScalar(GetMapping(postSource, "markdown"), "dir"));
    }

    [Fact]
    public void Apply_NotionIntent_WritesNotionSourcesAndFieldPolicy()
    {
        using var scope = new CurrentDirectoryScope(_rootDir);
        using var token = new EnvironmentVariableScope("NOTION_TOKEN", "test-token");

        Directory.CreateDirectory(Path.Combine(_rootDir, "sites"));
        Directory.CreateDirectory(Path.Combine(_rootDir, "themes", "starter"));

        var intentPath = WriteIntent(
            """
            site:
              name: demo
              title: Demo Site
              base_url: /
            content:
              kind: notion
              notion:
                database_id: db-123
                field_policy:
                  mode: all
                  allowed:
                    - title
                    - slug
            theme:
              name: starter
            """);

        var outPath = Path.Combine(_rootDir, "sites", "notion.yaml");

        var (validation, _) = IntentApplier.Apply(intentPath, outPath);

        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        Assert.True(File.Exists(outPath));

        var root = LoadYamlRoot(outPath);
        var sources = GetSequence(GetMapping(root, "content"), "sources");
        Assert.Equal(3, sources.Children.Count);

        foreach (var expectedName in new[] { "post", "page", "about" })
        {
            var source = GetSourceByName(sources, expectedName);
            Assert.Equal("notion", GetScalar(source, "type"));
            Assert.Equal(expectedName, GetScalar(source, "collection"));

            var notion = GetMapping(source, "notion");
            Assert.Equal("db-123", GetScalar(notion, "databaseId"));

            var fieldPolicy = GetMapping(notion, "fieldPolicy");
            Assert.Equal("all", GetScalar(fieldPolicy, "mode"));
            Assert.Equal(["title", "slug"], GetSequence(fieldPolicy, "allowed").Children.Select(GetScalarValue));
        }
    }

    [Fact]
    public void Apply_WhenConfigValidationFails_DoesNotWriteFile()
    {
        using var scope = new CurrentDirectoryScope(_rootDir);

        Directory.CreateDirectory(Path.Combine(_rootDir, "sites"));
        Directory.CreateDirectory(Path.Combine(_rootDir, "content"));

        var intentPath = WriteIntent(
            """
            site:
              name: demo
              title: Demo Site
              base_url: /
            content:
              kind: markdown
              markdown:
                dir: content
            theme:
              name: ../escape
            """);

        var outPath = Path.Combine(_rootDir, "sites", "invalid.yaml");

        var (validation, _) = IntentApplier.Apply(intentPath, outPath);

        Assert.False(validation.IsValid);
        Assert.Contains("theme.name must not contain '..' path traversal segments.", validation.Errors);
        Assert.False(File.Exists(outPath));
    }

    private string WriteIntent(string content)
    {
        var path = Path.Combine(_rootDir, "intent.yaml");
        File.WriteAllText(path, content.Replace("\r\n", "\n"));
        return path;
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');

    private static YamlMappingNode LoadYamlRoot(string path)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(File.ReadAllText(path)));

        return Assert.IsType<YamlMappingNode>(Assert.Single(stream.Documents).RootNode);
    }

    private static YamlMappingNode GetMapping(YamlMappingNode node, string key)
    {
        Assert.True(node.Children.TryGetValue(new YamlScalarNode(key), out var child), $"Missing key: {key}");
        return Assert.IsType<YamlMappingNode>(child);
    }

    private static YamlSequenceNode GetSequence(YamlMappingNode node, string key)
    {
        Assert.True(node.Children.TryGetValue(new YamlScalarNode(key), out var child), $"Missing key: {key}");
        return Assert.IsType<YamlSequenceNode>(child);
    }

    private static string GetScalar(YamlMappingNode node, string key)
    {
        Assert.True(node.Children.TryGetValue(new YamlScalarNode(key), out var child), $"Missing key: {key}");
        return GetScalarValue(child);
    }

    private static string GetScalarValue(YamlNode node)
    {
        return Assert.IsType<YamlScalarNode>(node).Value ?? string.Empty;
    }

    private static YamlMappingNode GetSourceByName(YamlSequenceNode sources, string name)
    {
        var match = sources.Children
            .Select(node => Assert.IsType<YamlMappingNode>(node))
            .FirstOrDefault(node => string.Equals(GetScalar(node, "name"), name, StringComparison.Ordinal));

        return match ?? throw new Xunit.Sdk.XunitException($"Missing source named '{name}'.");
    }

    private sealed class CurrentDirectoryScope : IDisposable
    {
        private readonly string _original;

        public CurrentDirectoryScope(string directory)
        {
            _original = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(directory);
        }

        public void Dispose()
        {
            Directory.SetCurrentDirectory(_original);
        }
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _originalValue;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _originalValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _originalValue);
        }
    }
}
