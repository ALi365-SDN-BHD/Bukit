using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConfigEnvironmentOverrideCollection
{
    public const string Name = "Config environment overrides serial";
}

[Collection(ConfigEnvironmentOverrideCollection.Name)]
public sealed class ConfigEnvironmentOverrideTests : IDisposable
{
    private readonly string _dir;
    private readonly string _configPath;
    private readonly Dictionary<string, string?> _original = new(StringComparer.Ordinal);

    public ConfigEnvironmentOverrideTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "bukit-config-env-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _configPath = Path.Combine(_dir, "site.yaml");
    }

    public void Dispose()
    {
        foreach (var kv in _original)
        {
            Environment.SetEnvironmentVariable(kv.Key, kv.Value);
        }

        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Load_AppliesBukitEnvironmentOverridesToNestedScalarValues()
    {
        File.WriteAllText(_configPath, """
                                      site:
                                        name: starter
                                        title: Starter
                                        url: https://old.example
                                      content:
                                        sources:
                                          - type: markdown
                                            markdown:
                                              dir: content
                                      build:
                                        clean: true
                                      """);
        SetEnv("BUKIT_SITE__TITLE", "From Env");
        SetEnv("BUKIT_SITE__URL", "https://env.example");
        SetEnv("BUKIT_CONTENT__MEDIA__DOWNLOAD_DIR", "env-media");
        SetEnv("BUKIT_BUILD__CLEAN", "false");

        var config = ConfigLoader.Load(_configPath);

        Assert.Equal("From Env", config.Site.Title);
        Assert.Equal("https://env.example", config.Site.Url);
        Assert.Equal("env-media", config.Content.Media.DownloadDir);
        Assert.False(config.Build.Clean);
    }

    [Fact]
    public void Load_InvalidEnvironmentOverride_ShouldFailWithPathDiagnostic()
    {
        WriteMinimalConfig();
        SetEnv("BUKIT_BUILD__CLEAN", "maybe");

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(_configPath));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
        Assert.Contains("build.clean", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_UnrelatedDoubleUnderscoreVariable_ShouldFailStrictValidation()
    {
        WriteMinimalConfig();
        SetEnv("BUKIT_UNKNOWN__SETTING", "value");

        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.Load(_configPath));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
        Assert.Contains("root.unknown", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_BukitPrefixedVariableWithoutDoubleUnderscore_ShouldBeIgnored()
    {
        WriteMinimalConfig();
        SetEnv("BUKIT_NOISE", "value");

        var config = ConfigLoader.Load(_configPath);

        Assert.Equal("Starter", config.Site.Title);
    }

    private void WriteMinimalConfig()
        => File.WriteAllText(_configPath, """
            site:
              name: starter
              title: Starter
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """);

    private void SetEnv(string key, string? value)
    {
        if (!_original.ContainsKey(key))
        {
            _original[key] = Environment.GetEnvironmentVariable(key);
        }

        Environment.SetEnvironmentVariable(key, value);
    }
}
