using Xunit;

namespace Bukit.Config.Tests;

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
                                        provider: markdown
                                        markdown:
                                          dir: content
                                      build:
                                        clean: true
                                      """);
        SetEnv("BUKIT_SITE__TITLE", "From Env");
        SetEnv("BUKIT_SITE__URL", "https://env.example");
        SetEnv("BUKIT_CONTENT__MARKDOWN__DIR", "posts");
        SetEnv("BUKIT_BUILD__CLEAN", "false");

        var config = ConfigLoader.Load(_configPath);

        Assert.Equal("From Env", config.Site.Title);
        Assert.Equal("https://env.example", config.Site.Url);
        Assert.Equal("posts", config.Content.Markdown!.Dir);
        Assert.False(config.Build.Clean);
    }

    private void SetEnv(string key, string? value)
    {
        if (!_original.ContainsKey(key))
        {
            _original[key] = Environment.GetEnvironmentVariable(key);
        }

        Environment.SetEnvironmentVariable(key, value);
    }
}
