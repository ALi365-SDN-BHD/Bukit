using Bukit.Cli.Intent;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class IntentWizardTests : IDisposable
{
    private readonly string _tempDir;

    public IntentWizardTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-wizard-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Fact]
    public void RunInteractive_AllDefaults_WritesValidIntentFile()
    {
        var outPath = Path.Combine(_tempDir, "intent.yaml");
        var originalIn = Console.In;
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            var input = string.Join("\n", new[]
            {
                "", "", "", "", "n", "", "", "", "", "", "", ""
            }) + "\n";
            Console.SetIn(new StringReader(input));

            IntentWizard.RunInteractive(outPath);

            Assert.True(File.Exists(outPath));
            var yaml = File.ReadAllText(outPath);
            Assert.Contains("name: my-site", yaml);
            Assert.Contains("title: My Site", yaml);
            Assert.Contains("kind: markdown", yaml);
            Assert.Contains("name: starter", yaml);
            Assert.Contains("sitemap: true", yaml);
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void RunInteractive_CustomSiteName_ReflectedInYaml()
    {
        var outPath = Path.Combine(_tempDir, "intent.yaml");
        var originalIn = Console.In;
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            var input = string.Join("\n", new[]
            {
                "custom-site", "Custom Title", "/blog/", "", "n", "", "", "", "", "", "", ""
            }) + "\n";
            Console.SetIn(new StringReader(input));

            IntentWizard.RunInteractive(outPath);

            Assert.True(File.Exists(outPath));
            var yaml = File.ReadAllText(outPath);
            Assert.Contains("name: custom-site", yaml);
            Assert.Contains("title: Custom Title", yaml);
            Assert.Contains("base_url: /blog/", yaml);
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void RunInteractive_NotionContentKind_WritesNotionConfig()
    {
        var outPath = Path.Combine(_tempDir, "intent.yaml");
        var originalIn = Console.In;
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            var input = string.Join("\n", new[]
            {
                "", "", "", "", "n", "", "notion", "my-db-id", "whitelist", "cover,tags", "", "", "", ""
            }) + "\n";
            Console.SetIn(new StringReader(input));

            IntentWizard.RunInteractive(outPath);

            Assert.True(File.Exists(outPath));
            var yaml = File.ReadAllText(outPath);
            Assert.Contains("kind: notion", yaml);
            Assert.Contains("database_id: my-db-id", yaml);
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void RunInteractive_MultiLanguage_WritesLanguagesSection()
    {
        var outPath = Path.Combine(_tempDir, "intent.yaml");
        var originalIn = Console.In;
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            var input = string.Join("\n", new[]
            {
                "", "", "", "", "y", "zh-CN", "zh-CN,en-US", "", "", "", "", "", ""
            }) + "\n";
            Console.SetIn(new StringReader(input));

            IntentWizard.RunInteractive(outPath);

            Assert.True(File.Exists(outPath));
            var yaml = File.ReadAllText(outPath);
            Assert.Contains("languages:", yaml);
            Assert.Contains("default: zh-CN", yaml);
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void RunInteractive_DisabledFeatures_WritesFalseFlags()
    {
        var outPath = Path.Combine(_tempDir, "intent.yaml");
        var originalIn = Console.In;
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(TextWriter.Null);
            var input = string.Join("\n", new[]
            {
                "", "", "", "", "n", "", "", "", "", "n", "n", "n"
            }) + "\n";
            Console.SetIn(new StringReader(input));

            IntentWizard.RunInteractive(outPath);

            Assert.True(File.Exists(outPath));
            var yaml = File.ReadAllText(outPath);
            Assert.Contains("sitemap: false", yaml);
            Assert.Contains("rss: false", yaml);
            Assert.Contains("search: false", yaml);
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }
}
