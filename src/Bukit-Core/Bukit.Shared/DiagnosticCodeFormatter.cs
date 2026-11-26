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
            DiagnosticCode.RouteOutputPathRejected => "Route: outputPath front matter is removed in Bukit 1.0",

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
            DiagnosticCode.ContentCollectionMissing => "Content: required collection is missing",

            DiagnosticCode.BuildOutputUnsafe => "Build: output directory is unsafe",
            DiagnosticCode.BuildOutputNoMarker => "Build: output directory missing marker",
            DiagnosticCode.BuildCleanRefused => "Build: clean refused",
            DiagnosticCode.BuildAssetOutputCollision => "Build: asset output collision",

            DiagnosticCode.PluginExecutionFailed => "Plugin: execution failed",
            DiagnosticCode.PluginTimeoutExceeded => "Plugin: timeout exceeded",
            DiagnosticCode.PluginOutputLimitExceeded => "Plugin: output limit exceeded",

            DiagnosticCode.I18nRootOutputConflict => "I18n: a default language static file conflicts with a root-owned output",
            DiagnosticCode.I18nRouteReservedLanguagePrefix => "I18n: a default language route uses another language code as its first segment",

            DiagnosticCode.NotionUnauthorized => "Notion: unauthorized (HTTP 401)",
            DiagnosticCode.NotionForbidden => "Notion: forbidden (HTTP 403)",
            DiagnosticCode.NotionNotFound => "Notion: not found (HTTP 404)",
            DiagnosticCode.NotionRateLimited => "Notion: rate limited (HTTP 429)",
            DiagnosticCode.NotionPropertyMissing => "Notion: a mapped database property is missing",
            DiagnosticCode.NotionRequestFailed => "Notion: the request failed",
            DiagnosticCode.NotionNetwork => "Notion: network or timeout error",
            DiagnosticCode.NotionInvalidResponse => "Notion: invalid JSON response",

            DiagnosticCode.NotionSnapshotUnavailable => "Notion snapshot: unavailable or another format",
            DiagnosticCode.NotionSnapshotDatabaseMissing => "Content snapshot: the Notion database or CloudBase table is missing",
            DiagnosticCode.NotionSnapshotBodyMissing => "Notion snapshot: the rendered page body is missing",
            DiagnosticCode.NotionSnapshotQueryMismatch => "Content snapshot: the recorded queries do not match",
            DiagnosticCode.NotionSnapshotEntryMissing => "Content snapshot: an entry is missing or unreadable",
            DiagnosticCode.NotionSnapshotWriteFailed => "Notion snapshot: the snapshot could not be written",
            DiagnosticCode.NotionSnapshotOptionInvalid => "Notion snapshot: the directory option is invalid",
            DiagnosticCode.NotionSnapshotUnsupportedRequest => "Notion snapshot: the offline build does not support this request",
            DiagnosticCode.NotionPinInvalid => "Notion pin: the pin file is invalid",
            DiagnosticCode.NotionPinSourceMissing => "Notion pin: a Notion source is not in the pin file",

            DiagnosticCode.CloudBaseConfigInvalid => "CloudBase: the content source configuration is invalid",
            DiagnosticCode.CloudBaseCredentialMissing => "CloudBase: the required credential is missing",
            DiagnosticCode.CloudBaseAuthFailed => "CloudBase: authentication failed (HTTP 401 or 403)",
            DiagnosticCode.CloudBaseNotFound => "CloudBase: the environment, table, or view was not found (HTTP 404)",
            DiagnosticCode.CloudBaseRateLimited => "CloudBase: rate limited after retries (HTTP 429)",
            DiagnosticCode.CloudBaseRequestFailed => "CloudBase: the request failed",
            DiagnosticCode.CloudBaseResponseInvalid => "CloudBase: the response is invalid or too large",
            DiagnosticCode.CloudBaseFileResolveFailed => "CloudBase: a file object could not be resolved",
            DiagnosticCode.CloudBaseMediaLocalizationRequired => "CloudBase: media localization excludes the gateway or storage host",
            DiagnosticCode.CloudBaseAdminCredentialInUse => "CloudBase: the credential looks like an admin key (service_role or an unreadable role)",
            DiagnosticCode.CloudBaseUnsupportedFileReference => "CloudBase: an unsupported cloud:// file reference was dropped or degraded",

            _ => $"Diagnostic code: {Format(code)}"
        };
    }
}
