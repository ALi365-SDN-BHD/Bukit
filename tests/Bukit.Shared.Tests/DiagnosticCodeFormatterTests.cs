using Bukit.Shared;
using Xunit;

namespace Bukit.Shared.Tests;

public sealed class DiagnosticCodeFormatterTests
{
    [Theory]
    [InlineData(DiagnosticCode.ConfigRequiredFieldMissing, "BKT-0001")]
    [InlineData(DiagnosticCode.ConfigInvalidValue, "BKT-0002")]
    [InlineData(DiagnosticCode.ConfigYamlSyntaxError, "BKT-0003")]
    [InlineData(DiagnosticCode.ConfigPathTraversal, "BKT-0004")]
    [InlineData(DiagnosticCode.ThemeManifestInvalid, "BKT-0101")]
    [InlineData(DiagnosticCode.ThemeComponentNotFound, "BKT-0102")]
    [InlineData(DiagnosticCode.ThemeTemplatePathEscape, "BKT-0103")]
    [InlineData(DiagnosticCode.ThemeSourceUnavailable, "BKT-0104")]
    [InlineData(DiagnosticCode.RouteConflict, "BKT-0201")]
    [InlineData(DiagnosticCode.RouteDuplicateOutputPath, "BKT-0202")]
    [InlineData(DiagnosticCode.RouteInvalidPattern, "BKT-0203")]
    [InlineData(DiagnosticCode.RouteListRouteInvalid, "BKT-0204")]
    [InlineData(DiagnosticCode.RouteOutputPathRejected, "BKT-0209")]
    [InlineData(DiagnosticCode.RenderTemplateNotFound, "BKT-0301")]
    [InlineData(DiagnosticCode.RenderTemplateParseError, "BKT-0302")]
    [InlineData(DiagnosticCode.RenderLayoutNestingExceeded, "BKT-0303")]
    [InlineData(DiagnosticCode.RenderComponentFailed, "BKT-0304")]
    [InlineData(DiagnosticCode.RenderFailed, "BKT-0399")]
    [InlineData(DiagnosticCode.SchemaValidationFailed, "BKT-0401")]
    [InlineData(DiagnosticCode.SchemaStrictModeBlocked, "BKT-0402")]
    [InlineData(DiagnosticCode.ContentLoadFailed, "BKT-0501")]
    [InlineData(DiagnosticCode.ContentProviderUnavailable, "BKT-0502")]
    [InlineData(DiagnosticCode.ContentDraftFiltered, "BKT-0503")]
    [InlineData(DiagnosticCode.ContentCollectionMissing, "BKT-0504")]
    [InlineData(DiagnosticCode.BuildOutputUnsafe, "BKT-0601")]
    [InlineData(DiagnosticCode.BuildOutputNoMarker, "BKT-0602")]
    [InlineData(DiagnosticCode.BuildCleanRefused, "BKT-0603")]
    [InlineData(DiagnosticCode.BuildAssetOutputCollision, "BKT-0604")]
    [InlineData(DiagnosticCode.PluginExecutionFailed, "BKT-0701")]
    [InlineData(DiagnosticCode.PluginTimeoutExceeded, "BKT-0702")]
    [InlineData(DiagnosticCode.PluginOutputLimitExceeded, "BKT-0703")]
    [InlineData(DiagnosticCode.NotionUnauthorized, "BKT-0B01")]
    [InlineData(DiagnosticCode.NotionForbidden, "BKT-0B02")]
    [InlineData(DiagnosticCode.NotionNotFound, "BKT-0B03")]
    [InlineData(DiagnosticCode.NotionRateLimited, "BKT-0B04")]
    [InlineData(DiagnosticCode.NotionPropertyMissing, "BKT-0B05")]
    [InlineData(DiagnosticCode.NotionRequestFailed, "BKT-0B06")]
    [InlineData(DiagnosticCode.NotionNetwork, "BKT-0B07")]
    [InlineData(DiagnosticCode.NotionInvalidResponse, "BKT-0B08")]
    [InlineData(DiagnosticCode.NotionSnapshotUnavailable, "BKT-0B10")]
    [InlineData(DiagnosticCode.NotionSnapshotDatabaseMissing, "BKT-0B11")]
    [InlineData(DiagnosticCode.NotionSnapshotBodyMissing, "BKT-0B12")]
    [InlineData(DiagnosticCode.NotionSnapshotQueryMismatch, "BKT-0B13")]
    [InlineData(DiagnosticCode.NotionSnapshotEntryMissing, "BKT-0B14")]
    [InlineData(DiagnosticCode.NotionSnapshotWriteFailed, "BKT-0B15")]
    [InlineData(DiagnosticCode.NotionSnapshotOptionInvalid, "BKT-0B16")]
    [InlineData(DiagnosticCode.NotionSnapshotUnsupportedRequest, "BKT-0B17")]
    public void Format_AllCodes_ReturnsCorrectHexFormat(DiagnosticCode code, string expected)
    {
        var result = DiagnosticCodeFormatter.Format(code);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(DiagnosticCode.ConfigRequiredFieldMissing, "Config", "required")]
    [InlineData(DiagnosticCode.ConfigInvalidValue, "Config", "invalid")]
    [InlineData(DiagnosticCode.ConfigYamlSyntaxError, "Config", "YAML")]
    [InlineData(DiagnosticCode.ConfigPathTraversal, "Config", "path")]
    [InlineData(DiagnosticCode.ThemeManifestInvalid, "Theme", "manifest")]
    [InlineData(DiagnosticCode.ThemeComponentNotFound, "Theme", "component")]
    [InlineData(DiagnosticCode.ThemeTemplatePathEscape, "Theme", "template")]
    [InlineData(DiagnosticCode.ThemeSourceUnavailable, "Theme", "source")]
    [InlineData(DiagnosticCode.RouteConflict, "Route", "conflict")]
    [InlineData(DiagnosticCode.RouteDuplicateOutputPath, "Route", "duplicate")]
    [InlineData(DiagnosticCode.RouteInvalidPattern, "Route", "invalid")]
    [InlineData(DiagnosticCode.RouteListRouteInvalid, "Route", "list")]
    [InlineData(DiagnosticCode.RouteOutputPathRejected, "Route", "removed")]
    [InlineData(DiagnosticCode.RenderTemplateNotFound, "Render", "template")]
    [InlineData(DiagnosticCode.RenderTemplateParseError, "Render", "parse")]
    [InlineData(DiagnosticCode.RenderLayoutNestingExceeded, "Render", "layout")]
    [InlineData(DiagnosticCode.RenderComponentFailed, "Render", "component")]
    [InlineData(DiagnosticCode.RenderFailed, "Render", "failure")]
    [InlineData(DiagnosticCode.SchemaValidationFailed, "Schema", "validation")]
    [InlineData(DiagnosticCode.SchemaStrictModeBlocked, "Schema", "strict")]
    [InlineData(DiagnosticCode.ContentLoadFailed, "Content", "load")]
    [InlineData(DiagnosticCode.ContentProviderUnavailable, "Content", "provider")]
    [InlineData(DiagnosticCode.ContentDraftFiltered, "Content", "draft")]
    [InlineData(DiagnosticCode.ContentCollectionMissing, "Content", "collection")]
    [InlineData(DiagnosticCode.BuildOutputUnsafe, "Build", "unsafe")]
    [InlineData(DiagnosticCode.BuildOutputNoMarker, "Build", "marker")]
    [InlineData(DiagnosticCode.BuildCleanRefused, "Build", "clean")]
    [InlineData(DiagnosticCode.BuildAssetOutputCollision, "Build", "collision")]
    [InlineData(DiagnosticCode.PluginExecutionFailed, "Plugin", "execution")]
    [InlineData(DiagnosticCode.PluginTimeoutExceeded, "Plugin", "timeout")]
    [InlineData(DiagnosticCode.PluginOutputLimitExceeded, "Plugin", "limit")]
    [InlineData(DiagnosticCode.NotionUnauthorized, "Notion", "401")]
    [InlineData(DiagnosticCode.NotionForbidden, "Notion", "403")]
    [InlineData(DiagnosticCode.NotionNotFound, "Notion", "404")]
    [InlineData(DiagnosticCode.NotionRateLimited, "Notion", "429")]
    [InlineData(DiagnosticCode.NotionPropertyMissing, "Notion", "property")]
    [InlineData(DiagnosticCode.NotionRequestFailed, "Notion", "failed")]
    [InlineData(DiagnosticCode.NotionNetwork, "Notion", "network")]
    [InlineData(DiagnosticCode.NotionInvalidResponse, "Notion", "JSON")]
    [InlineData(DiagnosticCode.NotionSnapshotUnavailable, "Notion", "unavailable")]
    [InlineData(DiagnosticCode.NotionSnapshotDatabaseMissing, "Notion", "database")]
    [InlineData(DiagnosticCode.NotionSnapshotBodyMissing, "Notion", "body")]
    [InlineData(DiagnosticCode.NotionSnapshotQueryMismatch, "Notion", "queries")]
    [InlineData(DiagnosticCode.NotionSnapshotEntryMissing, "Notion", "entry")]
    [InlineData(DiagnosticCode.NotionSnapshotWriteFailed, "Notion", "written")]
    [InlineData(DiagnosticCode.NotionSnapshotOptionInvalid, "Notion", "directory")]
    [InlineData(DiagnosticCode.NotionSnapshotUnsupportedRequest, "Notion", "request")]
    public void Describe_AllCodes_ReturnsMeaningfulDescription(DiagnosticCode code, string expectedCategory, string expectedKeyword)
    {
        var result = DiagnosticCodeFormatter.Describe(code);
        Assert.Contains(expectedCategory, result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedKeyword, result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData((DiagnosticCode)0, "BKT-0000")]
    [InlineData((DiagnosticCode)0xABCD, "BKT-ABCD")]
    [InlineData(DiagnosticCode.ConfigDeprecatedField, "BKT-0005")]
    public void Describe_UnmappedCode_IncludesStableHexFallback(DiagnosticCode code, string expected)
    {
        Assert.Equal(expected, DiagnosticCodeFormatter.Format(code));
        Assert.Equal($"Diagnostic code: {expected}", DiagnosticCodeFormatter.Describe(code));
    }
}
