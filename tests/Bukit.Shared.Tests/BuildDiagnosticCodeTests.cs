using Bukit.Shared;
using Xunit;

namespace Bukit.Shared.Tests;

public sealed class BuildDiagnosticCodeTests
{
    [Fact]
    public void DiagnosticCode_Exists_ForAllCategories()
    {
        Assert.True(Enum.IsDefined(typeof(DiagnosticCode), DiagnosticCode.ConfigRequiredFieldMissing));
        Assert.True(Enum.IsDefined(typeof(DiagnosticCode), DiagnosticCode.ConfigInvalidValue));
        Assert.True(Enum.IsDefined(typeof(DiagnosticCode), DiagnosticCode.ThemeManifestInvalid));
        Assert.True(Enum.IsDefined(typeof(DiagnosticCode), DiagnosticCode.RouteConflict));
        Assert.True(Enum.IsDefined(typeof(DiagnosticCode), DiagnosticCode.RenderTemplateNotFound));
        Assert.True(Enum.IsDefined(typeof(DiagnosticCode), DiagnosticCode.SchemaValidationFailed));
        Assert.True(Enum.IsDefined(typeof(DiagnosticCode), DiagnosticCode.ContentLoadFailed));
        Assert.True(Enum.IsDefined(typeof(DiagnosticCode), DiagnosticCode.BuildOutputUnsafe));
        Assert.True(Enum.IsDefined(typeof(DiagnosticCode), DiagnosticCode.PluginExecutionFailed));
        Assert.True(Enum.IsDefined(typeof(DiagnosticCode), DiagnosticCode.NotionUnauthorized));
        Assert.True(Enum.IsDefined(typeof(DiagnosticCode), DiagnosticCode.NotionSnapshotUnavailable));
        Assert.True(Enum.IsDefined(typeof(DiagnosticCode), DiagnosticCode.CloudBaseConfigInvalid));
        Assert.True(Enum.IsDefined(typeof(DiagnosticCode), DiagnosticCode.CloudBaseUnsupportedFileReference));
    }

    [Fact]
    public void DiagnosticCode_CloudBaseCodes_HaveStableValues()
    {
        Assert.Equal(0x0C01, (int)DiagnosticCode.CloudBaseConfigInvalid);
        Assert.Equal(0x0C02, (int)DiagnosticCode.CloudBaseCredentialMissing);
        Assert.Equal(0x0C03, (int)DiagnosticCode.CloudBaseAuthFailed);
        Assert.Equal(0x0C04, (int)DiagnosticCode.CloudBaseNotFound);
        Assert.Equal(0x0C05, (int)DiagnosticCode.CloudBaseRateLimited);
        Assert.Equal(0x0C06, (int)DiagnosticCode.CloudBaseRequestFailed);
        Assert.Equal(0x0C07, (int)DiagnosticCode.CloudBaseResponseInvalid);
        Assert.Equal(0x0C08, (int)DiagnosticCode.CloudBaseFileResolveFailed);
        Assert.Equal(0x0C09, (int)DiagnosticCode.CloudBaseMediaLocalizationRequired);
        Assert.Equal(0x0C0A, (int)DiagnosticCode.CloudBaseAdminCredentialInUse);
        Assert.Equal(0x0C0B, (int)DiagnosticCode.CloudBaseUnsupportedFileReference);
    }

    [Fact]
    public void DiagnosticCode_FormatsWithHex()
    {
        var code = DiagnosticCode.ConfigRequiredFieldMissing;
        var formatted = DiagnosticCodeFormatter.Format(code);
        Assert.Equal($"BKT-{(int)code:X4}", formatted);
    }

    [Fact]
    public void DiagnosticCode_HexValue_IsStable()
    {
        Assert.Equal("BKT-0001", DiagnosticCodeFormatter.Format(DiagnosticCode.ConfigRequiredFieldMissing));
    }

    [Fact]
    public void DiagnosticCode_AllCodes_HaveUniqueHexValues()
    {
        var seen = new HashSet<string>();
        foreach (DiagnosticCode code in Enum.GetValues<DiagnosticCode>())
        {
            var hex = DiagnosticCodeFormatter.Format(code);
            Assert.True(seen.Add(hex), $"Duplicate code: {hex}");
        }
    }

    [Fact]
    public void DiagnosticCode_Categories_AreWellDefined()
    {
        Assert.StartsWith("Config", DiagnosticCode.ConfigRequiredFieldMissing.ToString());
        Assert.StartsWith("Theme", DiagnosticCode.ThemeManifestInvalid.ToString());
        Assert.StartsWith("Route", DiagnosticCode.RouteConflict.ToString());
        Assert.StartsWith("Render", DiagnosticCode.RenderTemplateNotFound.ToString());
        Assert.StartsWith("Schema", DiagnosticCode.SchemaValidationFailed.ToString());
        Assert.StartsWith("Content", DiagnosticCode.ContentLoadFailed.ToString());
        Assert.StartsWith("Build", DiagnosticCode.BuildOutputUnsafe.ToString());
        Assert.StartsWith("Plugin", DiagnosticCode.PluginExecutionFailed.ToString());
        Assert.StartsWith("Seo", DiagnosticCode.SeoAuditFailed.ToString());
        Assert.StartsWith("Geo", DiagnosticCode.GeoLlmsTxtMissing.ToString());
        Assert.StartsWith("Media", DiagnosticCode.MediaImageMissing.ToString());
        Assert.StartsWith("I18n", DiagnosticCode.I18nRootOutputConflict.ToString());
        Assert.StartsWith("Notion", DiagnosticCode.NotionUnauthorized.ToString());
        Assert.StartsWith("CloudBase", DiagnosticCode.CloudBaseConfigInvalid.ToString());
    }

    [Fact]
    public void DiagnosticCode_Category_Ranges_AreNonOverlapping()
    {
        var ranges = new Dictionary<string, (int Min, int Max)>
        {
            ["Config"] = (0x0001, 0x00FF),
            ["Theme"] = (0x0101, 0x01FF),
            ["Route"] = (0x0201, 0x02FF),
            ["Render"] = (0x0301, 0x03FF),
            ["Schema"] = (0x0401, 0x04FF),
            ["Content"] = (0x0501, 0x05FF),
            ["Build"] = (0x0601, 0x06FF),
            ["Plugin"] = (0x0701, 0x07FF),
            ["Seo"] = (0x0801, 0x080F),
            ["Geo"] = (0x0810, 0x081F),
            ["Media"] = (0x0901, 0x09FF),
            ["I18n"] = (0x0A01, 0x0AFF),
            ["Notion"] = (0x0B01, 0x0BFF),
            ["CloudBase"] = (0x0C01, 0x0CFF),
        };

        foreach (DiagnosticCode code in Enum.GetValues<DiagnosticCode>())
        {
            var val = (int)code;
            var name = code.ToString();
            var prefix = ranges.Keys.First(k => name.StartsWith(k));
            var range = ranges[prefix];
            Assert.True(val >= range.Min && val <= range.Max,
                $"{name} ({val:X4}) is outside range {prefix} [{range.Min:X4}, {range.Max:X4}]");
        }
    }
}
