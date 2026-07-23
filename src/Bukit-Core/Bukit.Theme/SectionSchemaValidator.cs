using Bukit.Shared;

namespace Bukit.Theme;

public enum ValidationMode
{
    Off,
    Warn,
    Strict
}

public sealed class SectionSchemaValidator
{
    private readonly ILogger? _logger;
    private readonly ValidationMode _mode;
    private readonly string _themeRoot;

    public SectionSchemaValidator(ValidationMode mode, string themeRoot, ILogger? logger = null)
    {
        _mode = mode;
        _themeRoot = themeRoot;
        _logger = logger;
    }

    public List<SchemaValidationError> Validate(string sectionName, ThemeSectionDefinition sectionDef, IReadOnlyDictionary<string, object?>? props)
    {
        var errors = new List<SchemaValidationError>();

        if (_mode == ValidationMode.Off) return errors;

        var schema = LoadSchema(sectionDef);
        if (schema?.Props is null)
        {
            if (props is { Count: > 0 })
            {
                WarnOrError(sectionName, "Section has props but no schema defined", errors);
            }
            return errors;
        }

        if (props is null || props.Count == 0)
        {
            foreach (var (propName, propDef) in schema.Props)
            {
                if (propDef.Required)
                {
                    WarnOrError(sectionName, $"Missing required prop: {propName}", errors);
                }
            }
            return errors;
        }

        foreach (var (propName, propDef) in schema.Props)
        {
            if (propDef.Required && !props.ContainsKey(propName))
            {
                WarnOrError(sectionName, $"Missing required prop: {propName}", errors);
            }
        }

        foreach (var (propName, propValue) in props)
        {
            if (!schema.Props.TryGetValue(propName, out var propDef))
            {
                WarnOrError(sectionName, $"Unknown prop: {propName}", errors);
                continue;
            }

            ValidateValue(sectionName, propName, propDef, propValue, errors);
        }

        return errors;
    }

    private void ValidateValue(string sectionName, string propName, SchemaPropDefinition propDef, object? value, List<SchemaValidationError> errors)
    {
        if (value is null) return;

        switch (propDef.Type)
        {
            case "string":
                if (value is string strVal)
                {
                    if (propDef.MaxLength is { } max && strVal.Length > max)
                    {
                        WarnOrError(sectionName, $"Prop '{propName}' exceeds maxLength {max} (actual: {strVal.Length})", errors);
                    }
                }
                else
                {
                    WarnOrError(sectionName, $"Prop '{propName}' expected string, got {value.GetType().Name}", errors);
                }
                break;
            case "number":
                if (value is not (int or long or float or double or decimal))
                {
                    WarnOrError(sectionName, $"Prop '{propName}' expected number, got {value.GetType().Name}", errors);
                }
                break;
            case "boolean":
                if (value is not bool)
                {
                    WarnOrError(sectionName, $"Prop '{propName}' expected boolean, got {value.GetType().Name}", errors);
                }
                break;
            case "url":
                if (value is string urlStr)
                {
                    if (!urlStr.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                        !urlStr.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                        !urlStr.StartsWith('/'))
                    {
                        WarnOrError(sectionName, $"Prop '{propName}' is not a valid URL: {urlStr}", errors);
                    }
                }
                break;
            case "image":
                if (value is string imgStr)
                {
                    if (string.IsNullOrWhiteSpace(imgStr))
                    {
                        WarnOrError(sectionName, $"Prop '{propName}' image value is empty", errors);
                    }
                }
                break;
        }
    }

    private void WarnOrError(string sectionName, string message, List<SchemaValidationError> errors)
    {
        var error = new SchemaValidationError(sectionName, message);
        errors.Add(error);

        if (_mode == ValidationMode.Strict)
        {
            throw new SchemaValidationException(error.ToString());
        }

        if (_mode == ValidationMode.Warn)
        {
            _logger?.Warn($"[schema] {error}");
        }
    }

    private SectionSchema? LoadSchema(ThemeSectionDefinition sectionDef)
    {
        if (string.IsNullOrEmpty(sectionDef.Schema)) return null;

        var schemaPath = sectionDef.Schema;
        if (!Path.IsPathRooted(schemaPath))
        {
            schemaPath = Path.Combine(_themeRoot, schemaPath);
        }

        return SectionSchema.Load(schemaPath);
    }
}

public sealed record SchemaValidationError(string Section, string Message)
{
    public override string ToString() => $"{Section}: {Message}";
}

internal sealed class SchemaValidationException : Exception
{
    public SchemaValidationException(string message) : base(message) { }
}
