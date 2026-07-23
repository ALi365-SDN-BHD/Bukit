using Xunit;
using Bukit.Shared;
using Bukit.Theme;

namespace Bukit.Theme.Tests;

public sealed class SectionSchemaValidatorTests : IDisposable
{
    private readonly string _themeRoot;

    public SectionSchemaValidatorTests()
    {
        _themeRoot = Path.Combine(
            Path.GetTempPath(),
            "bukit-section-schema-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_themeRoot);
        WriteSchema();
    }

    public void Dispose()
    {
        if (Directory.Exists(_themeRoot))
        {
            Directory.Delete(_themeRoot, recursive: true);
        }
    }

    [Fact]
    public void Validate_OffMode_ReturnsNoErrors()
    {
        var validator = new SectionSchemaValidator(
            ValidationMode.Off,
            _themeRoot);
        var sectionDef = new ThemeSectionDefinition
        {
            Schema = "nonexistent.json"
        };
        var props = new Dictionary<string, object?> { ["title"] = "Hello" };

        var errors = validator.Validate("hero", sectionDef, props);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WarnMode_ReturnsErrorsAndLogsInTraversalOrder()
    {
        var logger = new RecordingLogger();
        var validator = new SectionSchemaValidator(
            ValidationMode.Warn,
            _themeRoot,
            logger);
        var props = InvalidProps();

        List<SchemaValidationError> errors = validator.Validate(
            "hero",
            SchemaDefinition(),
            props);

        SchemaValidationError[] expected =
        [
            new("hero", "Missing required prop: required_first"),
            new("hero", "Missing required prop: required_second"),
            new("hero", "Unknown prop: unknown"),
            new(
                "hero",
                "Prop 'title' exceeds maxLength 3 (actual: 4)"),
            new("hero", "Prop 'count' expected number, got String"),
            new("hero", "Prop 'enabled' expected boolean, got String"),
            new(
                "hero",
                "Prop 'url' is not a valid URL: relative/path"),
            new("hero", "Prop 'image' image value is empty")
        ];

        Assert.Equal(expected, errors);
        Assert.Equal(
            expected.Select(error => error.ToString()),
            errors.Select(error => error.ToString()));
        Assert.Equal(
            expected.Select(error => $"[schema] {error}"),
            logger.Warnings);
    }

    [Fact]
    public void Validate_NullProps_ReturnsRequiredErrorsInSchemaOrder()
    {
        var validator = new SectionSchemaValidator(
            ValidationMode.Warn,
            _themeRoot);

        List<SchemaValidationError> errors = validator.Validate(
            "hero",
            SchemaDefinition(),
            null);

        Assert.Equal(
            [
                new SchemaValidationError(
                    "hero",
                    "Missing required prop: required_first"),
                new SchemaValidationError(
                    "hero",
                    "Missing required prop: required_second")
            ],
            errors);
    }

    [Fact]
    public void Validate_StrictMode_ThrowsInternalExceptionAtFirstError()
    {
        var logger = new RecordingLogger();
        var validator = new SectionSchemaValidator(
            ValidationMode.Strict,
            _themeRoot,
            logger);

        Exception exception = Assert.ThrowsAny<Exception>(() =>
            validator.Validate("hero", SchemaDefinition(), InvalidProps()));

        Assert.Equal(
            "Bukit.Theme.SchemaValidationException",
            exception.GetType().FullName);
        Assert.Equal(
            "hero: Missing required prop: required_first",
            exception.Message);
        Assert.Null(exception.InnerException);
        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public void Validate_MissingSchema_ReturnsCurrentFallbackError()
    {
        var validator = new SectionSchemaValidator(
            ValidationMode.Warn,
            _themeRoot);
        var sectionDef = new ThemeSectionDefinition();
        var props = new Dictionary<string, object?> { ["unknown"] = "value" };

        List<SchemaValidationError> errors = validator.Validate(
            "hero",
            sectionDef,
            props);

        SchemaValidationError error = Assert.Single(errors);
        Assert.Equal("hero", error.Section);
        Assert.Equal(
            "Section has props but no schema defined",
            error.Message);
        Assert.Equal(
            "hero: Section has props but no schema defined",
            error.ToString());
    }

    [Fact]
    public void Validate_ValidValues_ReturnsNoErrorsOrWarnings()
    {
        var logger = new RecordingLogger();
        var validator = new SectionSchemaValidator(
            ValidationMode.Warn,
            _themeRoot,
            logger);
        var props = new Dictionary<string, object?>
        {
            ["required_first"] = "first",
            ["required_second"] = "second",
            ["title"] = "ok",
            ["count"] = 42,
            ["enabled"] = true,
            ["url"] = "/valid/path",
            ["image"] = "/images/hero.png"
        };

        List<SchemaValidationError> errors = validator.Validate(
            "hero",
            SchemaDefinition(),
            props);

        Assert.Empty(errors);
        Assert.Empty(logger.Warnings);
    }

    private ThemeSectionDefinition SchemaDefinition() =>
        new() { Schema = "schema.json" };

    private static Dictionary<string, object?> InvalidProps() =>
        new()
        {
            ["unknown"] = "value",
            ["title"] = "long",
            ["count"] = "many",
            ["enabled"] = "yes",
            ["url"] = "relative/path",
            ["image"] = " "
        };

    private void WriteSchema()
    {
        File.WriteAllText(
            Path.Combine(_themeRoot, "schema.json"),
            """
            {
              "Name": "hero",
              "Props": {
                "required_first": {
                  "Type": "string",
                  "Required": true
                },
                "required_second": {
                  "Type": "string",
                  "Required": true
                },
                "title": {
                  "Type": "string",
                  "MaxLength": 3
                },
                "count": {
                  "Type": "number"
                },
                "enabled": {
                  "Type": "boolean"
                },
                "url": {
                  "Type": "url"
                },
                "image": {
                  "Type": "image"
                }
              }
            }
            """);
    }

    private sealed class RecordingLogger : ILogger
    {
        internal List<string> Warnings { get; } = [];

        public void Debug(string message)
        {
        }

        public void Info(string message)
        {
        }

        public void Warn(string message) => Warnings.Add(message);

        public void Error(string message)
        {
        }
    }
}
