namespace Bukit.Shared;

public enum DiagnosticCode
{
    ConfigRequiredFieldMissing = 0x0001,
    ConfigInvalidValue = 0x0002,
    ConfigYamlSyntaxError = 0x0003,
    ConfigPathTraversal = 0x0004,

    ThemeManifestInvalid = 0x0101,
    ThemeComponentNotFound = 0x0102,
    ThemeTemplatePathEscape = 0x0103,
    ThemeSourceUnavailable = 0x0104,

    RouteConflict = 0x0201,
    RouteDuplicateOutputPath = 0x0202,
    RouteInvalidPattern = 0x0203,
    RouteListRouteInvalid = 0x0204,

    RenderTemplateNotFound = 0x0301,
    RenderTemplateParseError = 0x0302,
    RenderLayoutNestingExceeded = 0x0303,
    RenderComponentFailed = 0x0304,
    RenderFailed = 0x0399,

    SchemaValidationFailed = 0x0401,
    SchemaStrictModeBlocked = 0x0402,

    ContentLoadFailed = 0x0501,
    ContentProviderUnavailable = 0x0502,
    ContentDraftFiltered = 0x0503,

    BuildOutputUnsafe = 0x0601,
    BuildOutputNoMarker = 0x0602,
    BuildCleanRefused = 0x0603,

    PluginExecutionFailed = 0x0701,
    PluginTimeoutExceeded = 0x0702,
    PluginOutputLimitExceeded = 0x0703,

    SeoAuditFailed = 0x0801,
    SeoMetaMissing = 0x0802,
    SeoImageMissing = 0x0803,
    SeoSitemapIncomplete = 0x0804,

    GeoLlmsTxtMissing = 0x0810,
    GeoLlmsFullTxtMissing = 0x0811,
    GeoAuditScoreLow = 0x0812,

    MediaImageMissing = 0x0901,
    MediaImageLocalizeFailed = 0x0902,
    MediaImageFormatInvalid = 0x0903,
    MediaImageSizeExceeded = 0x0904,
}
