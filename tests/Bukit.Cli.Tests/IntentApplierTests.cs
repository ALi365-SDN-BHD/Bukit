using Bukit.Cli.Intent;
using Bukit.Config;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class IntentApplierTests : IDisposable
{
    private readonly string _rootDir;

    public IntentApplierTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-intent-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, recursive: true);
        }
    }

    [Fact]
    public void Apply_WritesDefaultCollections()
    {
        var intentPath = Path.Combine(_rootDir, "intent.yaml");
        var outPath = Path.Combine(_rootDir, "site.yaml");
        File.WriteAllText(intentPath, """
                                       site:
                                         name: test
                                         title: Test
                                         base_url: /
                                       content:
                                         provider: markdown
                                         markdown:
                                           dir: content
                                       theme:
                                         name: alt
                                       """);

        var (validation, _) = IntentApplier.Apply(intentPath, outPath);

        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        var config = ConfigLoader.Load(outPath);
        Assert.NotNull(config.Site.Collections);
        Assert.True(config.Site.Collections!.ContainsKey("post"));
        Assert.True(config.Site.Collections.ContainsKey("page"));
        Assert.Equal("/blog/{slug}/", config.Site.Collections["post"].Permalink);
        Assert.Null(config.Site.Collections["page"].Template);
    }
}
