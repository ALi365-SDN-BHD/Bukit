using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

[Collection("Console")]
public sealed class IntentWizardTests : IDisposable
{
    private readonly string _rootDir = Path.Combine(Path.GetTempPath(), "bukit-labs-intent-wizard-" + Guid.NewGuid().ToString("N"));

    public void Dispose() => TestCleanup.DeleteDirectory(_rootDir, recursive: true);

    [Fact]
    public void RunInteractive_SingleLanguage_WritesLanguagesSupportedWithoutSiteLanguage()
    {
        // Answers: name, title, base_url, url, multi-language (no), language, content.kind,
        // markdown dir, theme, sitemap, rss, search.
        var outPath = Path.Combine(_rootDir, "intent.yaml");
        using (new CommandTestSupport.ConsoleInputScope("\n\n\n\nn\nen\n\n\n\n\n\n\n"))
        {
            IntentWizard.RunInteractive(outPath);
        }

        var yaml = File.ReadAllText(outPath);
        Assert.DoesNotContain("language:", yaml.Replace("languages:", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.DoesNotContain("default:", yaml, StringComparison.Ordinal);

        var intent = IntentLoader.Load(outPath);
        Assert.NotNull(intent.Languages);
        Assert.Equal(["en"], intent.Languages.Supported);
        Assert.Equal("en", intent.Languages.ResolvedDefault);
    }
}
