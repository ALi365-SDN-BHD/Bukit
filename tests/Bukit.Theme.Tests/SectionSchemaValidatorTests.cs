using Xunit;
using Bukit.Theme;

namespace Bukit.Theme.Tests;

public sealed class SectionSchemaValidatorTests
{
    [Fact]
    public void Validate_OffMode_ReturnsNoErrors()
    {
        var validator = new SectionSchemaValidator(ValidationMode.Off);
        var sectionDef = new ThemeSectionDefinition { Schema = "nonexistent.json" };
        var props = new Dictionary<string, object?> { ["title"] = "Hello" };

        var errors = validator.Validate("hero", sectionDef, props);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_RequiredFieldMissing_Warns()
    {
        var validator = new SectionSchemaValidator(ValidationMode.Warn);
        var sectionDef = new ThemeSectionDefinition();

        var errors = validator.Validate("hero", sectionDef, null);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_StrictMode_ThrowsOnError()
    {
        var validator = new SectionSchemaValidator(ValidationMode.Strict);
        var sectionDef = new ThemeSectionDefinition();

        var props = new Dictionary<string, object?> { ["unknown_prop"] = "value" };
        Assert.Throws<SchemaValidationException>(() => validator.Validate("hero", sectionDef, props));
    }

    [Fact]
    public void Validate_UnknownProps_Warns()
    {
        var validator = new SectionSchemaValidator(ValidationMode.Warn);
        var sectionDef = new ThemeSectionDefinition();
        var props = new Dictionary<string, object?> { ["unknown"] = "value" };

        var errors = validator.Validate("hero", sectionDef, props);
        Assert.NotEmpty(errors);
    }
}
