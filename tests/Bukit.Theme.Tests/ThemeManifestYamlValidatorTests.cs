using Xunit;

namespace Bukit.Theme.Tests;

public sealed class ThemeManifestYamlValidatorTests
{
    [Fact, Trait("Category", "Integration")]
    public void Validate_StrictThemeYaml_ScalarNumericPath_IsTreatedAsString()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-theme-yaml-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(
                Path.Combine(root, "theme.yaml"),
                "name: test\nversion: 1.0.0\nengine: bukit\nlayouts:\n  base: 1\n");

            var issues = ThemeManifestYamlValidator.Validate(root);

            Assert.DoesNotContain(issues, issue => issue.Contains("layouts", StringComparison.Ordinal));
        }
        finally
        {
            TestCleanup.DeleteDirectory(root);
        }
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("capabilities: true", "theme.yaml.capabilities must be a mapping.")]
    [InlineData("sections:\n  hero:\n    template: sections/hero.html\n    data:\n      limit: nope", "theme.yaml.sections.hero.data.limit must be an integer when set.")]
    [InlineData("components:\n  card:\n    template: components/card.html\n    props: [x]", "theme.yaml.components.card.props must be a mapping.")]
    [InlineData("sections:\n  hero:\n    template: ../escape.html", "theme.yaml.sections.hero.template has path traversal characters.")]
    [InlineData("layouts:\n  base: [x]", "theme.yaml.layouts[base] must be a string path.")]
    [InlineData("templates:\n  home:\n    template: screens/home.html\n    required: nope", "theme.yaml.templates.home.required must be a boolean.")]
    [InlineData("assets:\n  css: {x: y}", "theme.yaml.assets.css must be a string or array.")]
    public void Validate_StrictThemeYaml_ReportsExpectedIssue(string fragment, string expected)
    {
        var yaml = $"name: test\nversion: 1.0.0\nengine: bukit\n{fragment}\n";
        var root = Path.Combine(Path.GetTempPath(), "bukit-theme-yaml-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "theme.yaml"), yaml);

            var issues = ThemeManifestYamlValidator.Validate(root);

            Assert.Contains(issues, issue => issue.Contains(expected, StringComparison.Ordinal));
        }
        finally
        {
            TestCleanup.DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData("", "BKT-0101: theme.yaml is empty or not a valid mapping.")]
    [InlineData("[]", "BKT-0101: theme.yaml is empty or not a valid mapping.")]
    [InlineData("{}", "BKT-0100: theme.yaml: 'name' is missing or empty (required in 1.0).\nBKT-0100: theme.yaml: 'version' is missing (required in 1.0).\nBKT-0100: theme.yaml: 'engine' is missing (required in 1.0). Set to 'bukit'.")]
    [InlineData("name: test\nversion: nope\nengine: other", "BKT-0100: theme.yaml: 'version' 'nope' is not valid semver.\nBKT-0100: theme.yaml: 'engine' must be 'bukit', got 'other'.")]
    public void Validate_PreservesExactDiagnosticsAndOrder(string yaml, string expected)
    {
        string root = Path.Combine(Path.GetTempPath(), "bukit-theme-yaml-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "theme.yaml"), yaml);
            Assert.Equal(expected.Split('\n'), ThemeManifestYamlValidator.Validate(root));
        }
        finally
        {
            TestCleanup.DeleteDirectory(root);
        }
    }
}
