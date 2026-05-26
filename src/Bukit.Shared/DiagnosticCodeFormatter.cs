namespace Bukit.Shared;

public static class DiagnosticCodeFormatter
{
    public static string Format(DiagnosticCode code)
    {
        return $"BKT-{(int)code:X4}";
    }

    public static string Describe(DiagnosticCode code)
    {
        return code switch
        {
            DiagnosticCode.ConfigRequiredFieldMissing => "Config: a required field is missing",
            DiagnosticCode.ConfigInvalidValue => "Config: a field has an invalid value",
            DiagnosticCode.ConfigYamlSyntaxError => "Config: YAML syntax error",
            DiagnosticCode.ConfigPathTraversal => "Config: path traversal detected",

            DiagnosticCode.ThemeManifestInvalid => "Theme: manifest (theme.yaml) is invalid",
            DiagnosticCode.ThemeComponentNotFound => "Theme: component template not found",
            DiagnosticCode.ThemeTemplatePathEscape => "Theme: template path escaped layouts root",
            DiagnosticCode.ThemeSourceUnavailable => "Theme: source unavailable",

            DiagnosticCode.RouteConflict => "Route: URL or output path conflict",
            DiagnosticCode.RouteDuplicateOutputPath => "Route: duplicate output path",
            DiagnosticCode.RouteInvalidPattern => "Route: invalid permalink pattern",
            DiagnosticCode.RouteListRouteInvalid => "Route: list route is invalid",

            DiagnosticCode.RenderTemplateNotFound => "Render: template not found",
            DiagnosticCode.RenderTemplateParseError => "Render: template parse error",
            DiagnosticCode.RenderLayoutNestingExceeded => "Render: layout nesting exceeded",
            DiagnosticCode.RenderComponentFailed => "Render: component render failed",
            DiagnosticCode.RenderFailed => "Render: general render failure",

            DiagnosticCode.SchemaValidationFailed => "Schema: validation failed",
            DiagnosticCode.SchemaStrictModeBlocked => "Schema: strict mode blocked build",

            DiagnosticCode.ContentLoadFailed => "Content: load failed",
            DiagnosticCode.ContentProviderUnavailable => "Content: provider unavailable",
            DiagnosticCode.ContentDraftFiltered => "Content: draft items filtered",

            DiagnosticCode.BuildOutputUnsafe => "Build: output directory is unsafe",
            DiagnosticCode.BuildOutputNoMarker => "Build: output directory missing marker",
            DiagnosticCode.BuildCleanRefused => "Build: clean refused",

            DiagnosticCode.PluginExecutionFailed => "Plugin: execution failed",
            DiagnosticCode.PluginTimeoutExceeded => "Plugin: timeout exceeded",
            DiagnosticCode.PluginOutputLimitExceeded => "Plugin: output limit exceeded",

            _ => $"Diagnostic code: {Format(code)}"
        };
    }
}
