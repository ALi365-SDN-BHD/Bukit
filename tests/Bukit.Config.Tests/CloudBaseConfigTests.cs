using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class CloudBaseConfigTests
{
    private const string MinimalBlock = """
        envId: prod-abc
        table: articles
        bodyField: content
        """;

    [Fact]
    public void Load_ValidConfig_AppliesDefaults()
    {
        var config = Load(MinimalBlock);
        ConfigValidator.Validate(config);

        var cloudBase = Assert.IsType<CloudBaseConfig>(Assert.Single(config.Content.Sources!).CloudBase);
        Assert.Equal("prod-abc", cloudBase.EnvId);
        Assert.Equal("cn", cloudBase.Region);
        Assert.Equal("articles", cloudBase.Table);
        Assert.Equal("id", cloudBase.IdField);
        Assert.Equal(100, cloudBase.PageSize);
        Assert.Equal("title", cloudBase.TitleField);
        Assert.Equal("slug", cloudBase.SlugField);
        Assert.Equal("publish_at", cloudBase.PublishAtField);
        Assert.Equal("updated_at", cloudBase.UpdatedAtField);
        Assert.Equal("content", cloudBase.BodyField);
        Assert.Equal("markdown", cloudBase.BodyFormat);
        Assert.Equal("whitelist", cloudBase.FieldPolicy.Mode);
        Assert.Empty(cloudBase.FileFields);
        Assert.Null(cloudBase.StorageBucket);
        Assert.Equal(3600, cloudBase.SignExpiresSeconds);
        Assert.Equal("off", cloudBase.CacheMode);
        Assert.Equal(5, cloudBase.MaxRps);
        Assert.Equal(3, cloudBase.MaxRetries);
    }

    [Fact]
    public void Load_ValidConfig_ReadsAllFields()
    {
        var config = Load("""
            envId: prod-abc
            region: intl
            table: articles
            idField: uid
            pageSize: 250
            maxItems: 500
            filter:
              field: status
              value: published
            sort:
              field: publish_at
              direction: desc
            titleField: headline
            slugField: permalink
            publishAtField: published_at
            updatedAtField: modified_at
            bodyField: body
            bodyFormat: markdown
            fieldPolicy:
              mode: all
              allowed:
                - cover
            fileFields:
              - cover
            storageBucket: media-bucket
            signExpiresSeconds: 720
            cacheMode: off
            maxRps: 9
            maxRetries: 4
            """, MediaBlock(downloadToLocal: true, fieldKeys: ["cover"], onFailure: "placeholder"));
        ConfigValidator.Validate(config);

        var cloudBase = Assert.IsType<CloudBaseConfig>(Assert.Single(config.Content.Sources!).CloudBase);
        Assert.Equal("intl", cloudBase.Region);
        Assert.Equal("uid", cloudBase.IdField);
        Assert.Equal(250, cloudBase.PageSize);
        Assert.Equal(500, cloudBase.MaxItems);
        Assert.Equal(new CloudBaseFilterConfig { Field = "status", Value = "published" }, cloudBase.Filter);
        Assert.Equal(new CloudBaseSortConfig { Field = "publish_at", Direction = "desc" }, cloudBase.Sort);
        Assert.Equal("headline", cloudBase.TitleField);
        Assert.Equal("permalink", cloudBase.SlugField);
        Assert.Equal("published_at", cloudBase.PublishAtField);
        Assert.Equal("modified_at", cloudBase.UpdatedAtField);
        Assert.Equal("body", cloudBase.BodyField);
        Assert.Equal("all", cloudBase.FieldPolicy.Mode);
        Assert.Equal(["cover"], cloudBase.FileFields);
        Assert.Equal("media-bucket", cloudBase.StorageBucket);
        Assert.Equal(720, cloudBase.SignExpiresSeconds);
        Assert.Equal(9, cloudBase.MaxRps);
        Assert.Equal(4, cloudBase.MaxRetries);
    }

    [Fact]
    public void Load_FilterValueEnvReference_PassesWithoutTheVariable()
    {
        var config = Load("""
            envId: prod-abc
            table: articles
            bodyField: content
            filter: { field: release_id, value: "${env:BUKIT_TEST_UNSET_RELEASE}" }
            """);

        Assert.Equal("${env:BUKIT_TEST_UNSET_RELEASE}", config.Content.Sources![0].CloudBase!.Filter!.Value);
    }

    [Fact]
    public void Load_FilterValueMalformedEnvReference_Fails()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            bodyField: content
            filter: { field: release_id, value: "${RELEASE_ID}" }
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("filter.value uses '${' outside a ${env:NAME} reference", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_MissingEnvId_Fails()
    {
        var ex = Fails("""
            table: articles
            bodyField: content
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("envId", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_MissingTable_Fails()
    {
        var ex = Fails("""
            envId: prod-abc
            bodyField: content
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("table", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_MissingBodyFieldForContentMode_Fails()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("bodyField", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void Load_PageSizeOutOfRange_Fails(int pageSize)
    {
        var ex = Fails($"""
            envId: prod-abc
            table: articles
            bodyField: content
            pageSize: {pageSize}
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("pageSize", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(59)]
    [InlineData(3601)]
    public void Load_SignExpiresSecondsOutOfRange_Fails(int seconds)
    {
        var ex = Fails($"""
            envId: prod-abc
            table: articles
            bodyField: content
            signExpiresSeconds: {seconds}
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("signExpiresSeconds", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_UnknownField_Fails()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            bodyField: content
            extraField: true
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("extraField", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_StrayMarkdownSectionOnCloudBaseSource_Fails()
    {
        var ex = Fails(
            """
            envId: prod-abc
            table: articles
            bodyField: content
            """,
            sourceExtras: "      markdown:\n        dir: content\n");
        AssertCloudBaseError(ex);
        Assert.Contains("content.sources[0].markdown is not used by a 'cloudbase' source", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_StrayCloudBaseSectionOnNotionSource_Fails()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bukit-cloudbase-config-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, """
            site:
              name: myblog
              title: My Blog
            content:
              sources:
                - type: notion
                  name: posts
                  notion:
                    databaseId: 0123456789abcdef0123456789abcdef
                  cloudbase:
                    envId: prod-abc
            """);

        ConfigException ex;
        try
        {
            ex = Assert.Throws<ConfigException>(() => ConfigValidator.Validate(ConfigLoader.Load(path)));
        }
        finally
        {
            File.Delete(path);
        }

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, ex.Code);
        Assert.Contains("content.sources[0].cloudbase is not used by a 'notion' source", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("kind", "model")]
    [InlineData("model", "article")]
    [InlineData("collectionName", "posts")]
    public void Load_LegacyFields_Fail(string field, string value)
    {
        var ex = Fails($"""
            envId: prod-abc
            table: articles
            bodyField: content
            {field}: {value}
            """);
        AssertCloudBaseError(ex);
        Assert.Contains(field, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("a.b")]
    [InlineData("a/b")]
    [InlineData("a?b")]
    [InlineData("a b")]
    [InlineData("1articles")]
    public void Load_InvalidTable_Fails(string table)
    {
        var ex = Fails($"""
            envId: prod-abc
            table: "{table}"
            bodyField: content
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("table", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_InvalidIdField_Fails()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            idField: "id asc"
            bodyField: content
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("idField", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_FileFieldsWithoutStorageBucket_Fails()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            bodyField: content
            fileFields:
              - cover
            """, MediaBlock(downloadToLocal: true, fieldKeys: ["cover"], onFailure: "placeholder"));
        AssertCloudBaseError(ex);
        Assert.Contains("storageBucket", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("MediaBucket")]
    [InlineData("-media")]
    [InlineData("media bucket")]
    public void Load_InvalidStorageBucket_Fails(string bucket)
    {
        var ex = Fails($"""
            envId: prod-abc
            table: articles
            bodyField: content
            fileFields:
              - cover
            storageBucket: "{bucket}"
            """, MediaBlock(downloadToLocal: true, fieldKeys: ["cover"], onFailure: "placeholder"));
        AssertCloudBaseError(ex);
        Assert.Contains("storageBucket", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("us")]
    [InlineData("CN")]
    public void Load_InvalidRegion_Fails(string region)
    {
        var ex = Fails($"""
            envId: prod-abc
            table: articles
            bodyField: content
            region: {region}
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("region", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("apiKey")]
    [InlineData("token")]
    [InlineData("secret")]
    public void Load_CredentialFieldInYaml_Fails(string field)
    {
        var ex = Fails($"""
            envId: prod-abc
            table: articles
            bodyField: content
            {field}: "secret-value"
            """);
        AssertCloudBaseError(ex);
        Assert.Contains(field, ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("readonly")]
    [InlineData("readwrite")]
    public void Load_UnsupportedCacheMode_Fails(string mode)
    {
        var ex = Fails($"""
            envId: prod-abc
            table: articles
            bodyField: content
            cacheMode: {mode}
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("cacheMode", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_UnsupportedBodyFormat_Fails()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            bodyField: content
            bodyFormat: html
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("bodyFormat", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("publish-at")]
    [InlineData("status code")]
    [InlineData("1status")]
    public void Load_InvalidFilterField_Fails(string field)
    {
        var ex = Fails($"""
            envId: prod-abc
            table: articles
            bodyField: content
            filter:
              field: "{field}"
              value: published
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("filter.field", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("publish-at")]
    [InlineData("publish_at desc")]
    public void Load_InvalidSortField_Fails(string field)
    {
        var ex = Fails($"""
            envId: prod-abc
            table: articles
            bodyField: content
            sort:
              field: "{field}"
              direction: desc
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("sort.field", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("envId: \"prod-abc\\n\"\ntable: articles", "envId")]
    [InlineData("envId: prod-abc\ntable: \"articles\\n\"", "table")]
    [InlineData("envId: prod-abc\ntable: articles\nidField: \"id\\n\"", "idField")]
    public void Load_TrailingNewlineInIdentifier_Fails(string head, string field)
    {
        var ex = Fails(head + "\nbodyField: content");
        AssertCloudBaseError(ex);
        Assert.Contains(field, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_FilterWithExtraKey_Fails()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            bodyField: content
            filter:
              field: status
              op: ne
              value: draft
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("op", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("bad/env")]
    [InlineData("bad@env")]
    [InlineData("bad#env")]
    [InlineData("bad?env")]
    [InlineData("bad.env")]
    [InlineData("BAD-ENV")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Load_InvalidEnvId_Fails(string envId)
    {
        var ex = Fails($"""
            envId: "{envId}"
            table: articles
            bodyField: content
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("envId", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_DownloadToLocalFalse_Fails()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            bodyField: content
            fileFields:
              - cover
            storageBucket: media-bucket
            """, MediaBlock(downloadToLocal: false, fieldKeys: ["cover"], onFailure: "placeholder"));
        AssertCloudBaseError(ex);
        Assert.Contains("downloadToLocal", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_FileFieldNotInMediaFieldKeys_Fails()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            bodyField: content
            fileFields:
              - cover
            storageBucket: media-bucket
            """, MediaBlock(downloadToLocal: true, fieldKeys: ["image"], onFailure: "placeholder"));
        AssertCloudBaseError(ex);
        Assert.Contains("cover", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_SkipHostsExcludesGateway_FailsMediaLocalization()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            bodyField: content
            fileFields:
              - cover
            storageBucket: media
            """, MediaWithHosts(skip: ["prod-abc.api.tcloudbasegateway.com"]));

        Assert.Equal(DiagnosticCode.CloudBaseMediaLocalizationRequired, ex.Code);
        Assert.Contains("prod-abc.api.tcloudbasegateway.com", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_DownloadHostsExcludeStorageCdn_FailsMediaLocalization()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            bodyField: content
            fileFields:
              - cover
            storageBucket: media
            """, MediaWithHosts(download: ["prod-abc.api.tcloudbasegateway.com"]));

        Assert.Equal(DiagnosticCode.CloudBaseMediaLocalizationRequired, ex.Code);
        Assert.Contains("prod-abc.storage.tcloudbase.com", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, "placeholder", "downloadToLocal")]
    [InlineData(true, "keep", "onFailure")]
    public void Load_ContentBucketWithoutFileFields_RequiresMediaLocalization(bool downloadToLocal, string onFailure, string expected)
    {
        // Body cloud:// images are signed whenever a bucket is set, so D8 applies without fileFields.
        var ex = Fails("""
            envId: prod-abc
            table: articles
            bodyField: content
            storageBucket: media
            """, MediaBlock(downloadToLocal, ["cover"], onFailure));

        AssertCloudBaseError(ex);
        Assert.Contains("content.sources[0].cloudbase.storageBucket requires", ex.Message, StringComparison.Ordinal);
        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_ContentBucketWithoutFileFields_RequiresCloudBaseHosts()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            bodyField: content
            storageBucket: media
            """, MediaWithHosts(skip: ["prod-abc.api.tcloudbasegateway.com"]));

        Assert.Equal(DiagnosticCode.CloudBaseMediaLocalizationRequired, ex.Code);
    }

    [Fact]
    public void Load_DataBucketWithoutFileFields_SkipsMediaLocalization()
    {
        // Data mode signs nothing without fileFields.
        var config = Load("""
            envId: prod-abc
            table: articles
            storageBucket: media
            """, MediaBlock(downloadToLocal: false, ["cover"], "keep"), mode: "data");

        Assert.Equal("media", Assert.IsType<CloudBaseConfig>(Assert.Single(config.Content.Sources!).CloudBase).StorageBucket);
    }

    [Fact]
    public void Load_DownloadHostsIncludeBothCloudBaseHosts_Passes()
    {
        var config = Load("""
            envId: prod-abc
            table: articles
            bodyField: content
            fileFields:
              - cover
            storageBucket: media
            """, MediaWithHosts(download: ["prod-abc.api.tcloudbasegateway.com", "prod-abc.storage.tcloudbase.com"]));

        Assert.Equal("media", Assert.IsType<CloudBaseConfig>(Assert.Single(config.Content.Sources!).CloudBase).StorageBucket);
    }

    [Fact]
    public void Load_DataModeWithFileFields_Passes()
    {
        var config = Load("""
            envId: prod-abc
            table: gallery
            fileFields:
              - photo
            storageBucket: media
            """, MediaWithHosts(download: ["prod-abc.api.tcloudbasegateway.com", "prod-abc.storage.tcloudbase.com"]).Replace("- cover", "- photo", StringComparison.Ordinal), mode: "data");

        Assert.Equal(["photo"], Assert.IsType<CloudBaseConfig>(Assert.Single(config.Content.Sources!).CloudBase).FileFields);
    }

    [Fact]
    public void Load_DataModeFileFieldsWithoutLocalization_Fails()
    {
        var ex = Fails("""
            envId: prod-abc
            table: gallery
            fileFields:
              - photo
            storageBucket: media
            """, mode: "data");

        AssertCloudBaseError(ex);
        Assert.Contains("cloudbase.fileFields", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_FileFieldsHardExcluded_Fails()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            bodyField: content
            fileFields:
              - user_id
            storageBucket: media
            """, MediaBlock(downloadToLocal: true, fieldKeys: ["user_id"], onFailure: "placeholder"));
        AssertCloudBaseError(ex);
        Assert.Contains("user_id", ex.Message, StringComparison.Ordinal);
    }

    private static string MediaWithHosts(string[]? download = null, string[]? skip = null)
    {
        var lines = new List<string>
        {
            "  media:",
            "    downloadToLocal: true",
            "    fieldKeys:",
            "      - cover",
            "    onFailure: placeholder"
        };
        if (download is not null)
        {
            lines.Add("    downloadHosts:");
            lines.AddRange(download.Select(host => $"      - {host}"));
        }

        if (skip is not null)
        {
            lines.Add("    skipHosts:");
            lines.AddRange(skip.Select(host => $"      - {host}"));
        }

        return string.Join(System.Environment.NewLine, lines) + System.Environment.NewLine;
    }

    [Fact]
    public void Load_OnFailureKeep_Fails()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            bodyField: content
            fileFields:
              - cover
            storageBucket: media-bucket
            """, MediaBlock(downloadToLocal: true, fieldKeys: ["cover"], onFailure: "keep"));
        AssertCloudBaseError(ex);
        Assert.Contains("onFailure", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_FieldPolicyInvalidMode_Fails()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            bodyField: content
            fieldPolicy:
              mode: bogus
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("fieldPolicy.mode", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_EmptyMappingField_Fails()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            bodyField: content
            titleField: ""
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("titleField", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_SourceLevelLegacyField_Fails()
    {
        var ex = Fails(MinimalBlock, sourceExtras: "      kind: model" + System.Environment.NewLine);
        AssertCloudBaseError(ex);
        Assert.Contains("kind", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_MaxItemsNotPositive_Fails()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            bodyField: content
            maxItems: 0
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("maxItems", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_MaxRpsNotPositive_Fails()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            bodyField: content
            maxRps: 0
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("maxRps", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_MaxRetriesNegative_Fails()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            bodyField: content
            maxRetries: -1
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("maxRetries", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_InvalidSortDirection_Fails()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            bodyField: content
            sort:
              field: publish_at
              direction: up
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("sort", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_FilterMissingValue_Fails()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            bodyField: content
            filter:
              field: status
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("filter", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("cacheMode: OFF")]
    [InlineData("bodyFormat: \" markdown \"")]
    public void Load_CaseOrSpaceVariants_Fail(string line)
    {
        var ex = Fails($"envId: prod-abc\ntable: articles\nbodyField: content\n{line}");
        AssertCloudBaseError(ex);
    }

    [Fact]
    public void Load_ModeDataWithoutBodyField_Passes()
    {
        var config = Load("""
            envId: prod-abc
            table: articles
            """, mode: "data");
        var cloudBase = Assert.IsType<CloudBaseConfig>(Assert.Single(config.Content.Sources!).CloudBase);
        Assert.Equal(string.Empty, cloudBase.BodyField);
    }

    [Fact]
    public void Load_SixtyThreeCharacterEnvId_Passes()
    {
        var envId = new string('a', 63);
        var config = Load($"""
            envId: {envId}
            table: articles
            bodyField: content
            """);
        Assert.Equal(envId, Assert.IsType<CloudBaseConfig>(Assert.Single(config.Content.Sources!).CloudBase).EnvId);
    }

    [Theory]
    [InlineData("titleField")]
    [InlineData("slugField")]
    [InlineData("publishAtField")]
    [InlineData("updatedAtField")]
    [InlineData("idField")]
    public void Load_MappedFieldUsesHardExcludedColumn_Fails(string field)
    {
        var ex = Fails($"""
            envId: prod-abc
            table: articles
            bodyField: content
            {field}: owner
            """);
        AssertCloudBaseError(ex);
        Assert.Contains(field, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_BodyFieldUsesHardExcludedColumn_Fails()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            bodyField: user_id
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("bodyField", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_NonIntegerPageSize_FailsWithCloudBaseCodeAndPath()
    {
        var ex = Fails("""
            envId: prod-abc
            table: articles
            bodyField: content
            pageSize: abc
            """);
        AssertCloudBaseError(ex);
        Assert.Contains("content.sources[0].cloudbase.pageSize", ex.Message, StringComparison.Ordinal);
    }

    private static void AssertCloudBaseError(ConfigException ex)
        => Assert.Equal(DiagnosticCode.CloudBaseConfigInvalid, ex.Code);

    private static string MediaBlock(bool downloadToLocal, string[] fieldKeys, string onFailure)
    {
        var keys = string.Join(System.Environment.NewLine, fieldKeys.Select(key => $"      - {key}"));
        return $"""
          media:
            downloadToLocal: {downloadToLocal.ToString().ToLowerInvariant()}
            fieldKeys:
        {keys}
            onFailure: {onFailure}
        """ + System.Environment.NewLine;
    }

    private static AppConfig Load(string cloudBaseBlock, string mediaBlock = "", string sourceExtras = "", string mode = "content")
    {
        var config = ConfigLoader.Load(WriteConfig(cloudBaseBlock, mediaBlock, sourceExtras, mode));
        ConfigValidator.Validate(config);
        return config;
    }

    private static ConfigException Fails(string cloudBaseBlock, string mediaBlock = "", string sourceExtras = "", string mode = "content")
    {
        var path = WriteConfig(cloudBaseBlock, mediaBlock, sourceExtras, mode);
        return Assert.Throws<ConfigException>(() =>
        {
            var config = ConfigLoader.Load(path);
            ConfigValidator.Validate(config);
        });
    }

    private static string WriteConfig(string cloudBaseBlock, string mediaBlock, string sourceExtras, string mode)
    {
        var yaml = $"""
            site:
              name: myblog
              title: My Blog
            content:
            {mediaBlock}  sources:
                - type: cloudbase
                  name: articles
                  mode: {mode}
            {sourceExtras}      cloudbase:
            {Indent(cloudBaseBlock, 8)}
            """;
        var path = Path.Combine(Path.GetTempPath(), $"bukit-cloudbase-config-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, yaml);
        return path;
    }

    private static string Indent(string block, int spaces)
    {
        var prefix = new string(' ', spaces);
        return string.Join(
            System.Environment.NewLine,
            block.Split('\n').Select(line => prefix + line.TrimEnd('\r')));
    }
}
