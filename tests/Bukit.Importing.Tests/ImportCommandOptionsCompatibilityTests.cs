using Xunit;

namespace Bukit.Importing.Tests;

public sealed class ImportCommandOptionsCompatibilityTests
{
    [Fact]
    public void HtmlDemoOptions_DefaultsMatchCurrentLabsImportCommand()
    {
        var options = new ImportCommandOptions
        {
            Subcommand = "html-demo",
            RootDir = "/repo",
            WorkingDir = "/repo",
            DemoDir = "/repo/demo",
            ThemeName = "demo"
        };

        Assert.Equal("notion", options.ContentSource);
        Assert.Equal("markdown", options.BuildSource);
        Assert.Equal("zh", options.Language);
        Assert.Equal("NOTION_TOKEN", options.NotionTokenEnv);
        Assert.True(options.ExtractContent);
        Assert.True(options.GenerateSeed);
        Assert.True(options.PreserveHtml);
        Assert.True(options.GenerateReport);
        Assert.True(options.ValidateNotionSchema);
    }
}
