using Xunit;

namespace Bukit.Theme.Tests;

public sealed class ThemeManifestYamlValidatorTests
{
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
