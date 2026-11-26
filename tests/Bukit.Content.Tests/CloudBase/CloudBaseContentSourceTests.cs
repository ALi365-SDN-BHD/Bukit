using System.Net;
using System.Text;
using Bukit.Content.CloudBase;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;
using Xunit;

namespace Bukit.Content.Tests.CloudBase;

public sealed class CloudBaseContentSourceTests
{
    [Fact]
    public async Task LoadRawAsync_PagesProjectsAndRendersUntrustedBodies()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"One","slug":"one","publish_at":"2026-01-02T03:04:05Z","updated_at":"2026-02-03T04:05:06Z","content":"# Hi\n\n[x](javascript:alert(1))","tags":["t1"],"user_id":"u-secret"},{"id":"a2","title":"Two","slug":"two","content":"Body two"}]""";
        api.Pages[2] = """[{"id":"a3","title":"Three","slug":"three","content":"Body three"}]""";
        var source = Create(api, options => options with
        {
            PageSize = 2,
            FilterField = "status",
            FilterValue = "published",
            SortField = "publish_at",
            SortDirection = "desc",
            FieldPolicyMode = "whitelist",
            AllowedFields = ["title", "tags"]
        });

        var result = await source.LoadRawAsync();

        Assert.Equal(3, result.Documents.Count);
        Assert.Equal(2, api.Requests.Count);
        Assert.Contains("status=eq.published", Uri.UnescapeDataString(api.Requests[0].Query), StringComparison.Ordinal);
        Assert.Contains("order=publish_at.desc,id.asc", Uri.UnescapeDataString(api.Requests[0].Query), StringComparison.Ordinal);

        var first = result.Documents[0];
        Assert.Equal("One", first.Title);
        Assert.Equal("one", first.Slug);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), first.PublishedAt);
        Assert.NotNull(first.CustomFields);
        Assert.Equal(["last_edited_time", "source", "tags", "title"], first.CustomFields!.Keys.Order(StringComparer.Ordinal));
        Assert.DoesNotContain("user_id", first.CustomFields.Keys);
        Assert.Equal("date", first.CustomFields["last_edited_time"].Type);

        var body = await result.BodyStore.GetAsync(first);
        Assert.Contains("<h1", body.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript:", body.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadRawAsync_Whitelist_SelectsOnlyConfiguredColumns()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"A","slug":"a","content":"x","tags":["t"],"user_id":"u"}]""";
        api.Total = 1;
        var source = Create(api, options => options with
        {
            FieldPolicyMode = "whitelist",
            AllowedFields = ["tags"]
        });

        await source.LoadRawAsync();

        var query = Uri.UnescapeDataString(api.Requests[0].Query);
        var parts = query.Split('&');
        var select = parts.FirstOrDefault(part => part.TrimStart('?').StartsWith("select=", StringComparison.Ordinal));
        Assert.True(select is not null, query);
        Assert.Contains("id", select, StringComparison.Ordinal);
        Assert.Contains("title", select, StringComparison.Ordinal);
        Assert.Contains("content", select, StringComparison.Ordinal);
        Assert.Contains("tags", select, StringComparison.Ordinal);
        Assert.DoesNotContain("user_id", select, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadRawAsync_AllMode_DoesNotSendSelect()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"A","slug":"a","content":"x"}]""";
        api.Total = 1;
        var source = Create(api, options => options with { FieldPolicyMode = "all" });

        await source.LoadRawAsync();

        Assert.DoesNotContain("select=", Uri.UnescapeDataString(api.Requests[0].Query), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadRawAsync_MaxItems_StopsPaging()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"One","slug":"one","content":"x"},{"id":"a2","title":"Two","slug":"two","content":"y"}]""";
        api.Total = 3;
        var source = Create(api, options => options with { PageSize = 2, MaxItems = 2 });

        var result = await source.LoadRawAsync();

        Assert.Equal(2, result.Documents.Count);
        Assert.Single(api.Requests);
    }

    [Fact]
    public async Task LoadRawAsync_EmptyRlsResult_ProducesNoDocuments()
    {
        var api = new FakeCloudBaseApi { Total = 0 };
        var source = Create(api, options => options);

        var result = await source.LoadRawAsync();

        Assert.Empty(result.Documents);
    }

    [Fact]
    public async Task LoadRawAsync_ModeData_HasNoBody()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"One","slug":"one","content":"ignored"}]""";
        api.Total = 1;
        var source = Create(api, options => options with { Mode = "data" });

        var result = await source.LoadRawAsync();

        var document = Assert.Single(result.Documents);
        Assert.Null(document.Body.BodyKey);
        Assert.Null(document.Body.Markdown);
        Assert.Equal(string.Empty, (await result.BodyStore.GetAsync(document)).Html);
    }

    [Fact]
    public async Task LoadRawAsync_BlankSlug_IsDerivedFromTitle()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"Hello World","slug":"   ","content":"x"}]""";
        api.Total = 1;
        var messages = new List<string>();
        var source = Create(api, options => options, new ListLogger(messages));

        var document = Assert.Single((await source.LoadRawAsync()).Documents);

        Assert.Equal("Hello World", document.Title);
        Assert.Equal("hello-world", document.Slug);
        Assert.DoesNotContain(messages, message => message.Contains("missing_title_or_slug", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadRawAsync_BlankTitleAndSlug_FallsBackToIdWithWarning()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"  ","slug":"   ","content":"x"}]""";
        api.Total = 1;
        var messages = new List<string>();
        var source = Create(api, options => options, new ListLogger(messages));

        var document = Assert.Single((await source.LoadRawAsync()).Documents);

        Assert.Equal("a1", document.Title);
        Assert.Equal("a1", document.Slug);
        Assert.Contains(messages, message =>
            message.Contains("event=cloudbase.missing_title_or_slug", StringComparison.Ordinal) &&
            message.Contains("slugFromId=1", StringComparison.Ordinal) &&
            message.Contains("untitled=1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadRawAsync_DataMode_KeepsIdFallbackWithoutWarning()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"m1","title":"Team Member"}]""";
        api.Total = 1;
        var messages = new List<string>();
        var source = Create(api, options => options with { Mode = "data" }, new ListLogger(messages));

        var document = Assert.Single((await source.LoadRawAsync()).Documents);

        Assert.Equal("m1", document.Slug);
        Assert.Equal("Team Member", document.Title);
        Assert.DoesNotContain(messages, message => message.Contains("missing_title_or_slug", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadRawAsync_BlankTitle_UsesSlug()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":null,"slug":"my-post","content":"x"}]""";
        api.Total = 1;
        var source = Create(api, options => options);

        var document = Assert.Single((await source.LoadRawAsync()).Documents);

        Assert.Equal("my-post", document.Title);
        Assert.Equal("my-post", document.Slug);
    }

    [Fact]
    public async Task LoadRawAsync_EpochDatesAndNumericId_AreAccepted()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":101,"title":"Epoch","slug":"epoch","publish_at":0,"updated_at":1767323045,"content":"x"}]""";
        api.Total = 1;
        var source = Create(api, options => options);

        var document = Assert.Single((await source.LoadRawAsync()).Documents);

        Assert.Equal("101", document.SourceId);
        Assert.Equal(DateTimeOffset.UnixEpoch, document.PublishedAt);
        Assert.NotNull(document.CustomFields);
        Assert.Equal(
            DateTimeOffset.FromUnixTimeSeconds(1767323045),
            document.CustomFields!["last_edited_time"].Value);
    }

    [Fact]
    public async Task LoadRawAsync_MissingIdField_ThrowsResponseInvalid()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"title":"One"}]""";
        api.Total = 1;
        var source = Create(api, options => options);

        var ex = await Assert.ThrowsAsync<ContentException>(() => source.LoadRawAsync());

        Assert.Equal(DiagnosticCode.CloudBaseResponseInvalid, ex.Code);
    }

    [Fact]
    public async Task LoadRawAsync_Unauthorized_MapsToAuthFailed()
    {
        var api = new FakeCloudBaseApi { Unauthorized = true };
        var source = Create(api, options => options);

        var ex = await Assert.ThrowsAsync<ContentException>(() => source.LoadRawAsync());

        Assert.Equal(DiagnosticCode.CloudBaseAuthFailed, ex.Code);
        Assert.DoesNotContain("pk_test", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadRawAsync_SignsFileFieldsAndBodyCloudImages()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"A","slug":"a","cover":"posts/a.png","content":"![x](cloud://env-abc.media/posts/b.png)","other":"cloud://env-abc.media/posts/c.png"}]""";
        api.Total = 1;
        var source = Create(api, options => options with
        {
            FileFields = ["cover"],
            StorageBucket = "media",
            DefaultImageUrl = "/ph.jpg",
            FieldPolicyMode = "all"
        });

        var result = await source.LoadRawAsync();

        var document = Assert.Single(result.Documents);
        Assert.Equal("cloudbase", document.CustomFields!["source"].Value);
        var cover = Assert.IsType<string>(document.CustomFields["cover"].Value);
        Assert.Equal(
            "https://env-abc.api.tcloudbasegateway.com/v1/storages/object/sign/media/posts/a.png?token=x",
            cover);
        var html = (await result.BodyStore.GetAsync(document)).Html;
        Assert.Contains("/v1/storages/object/sign/media/posts/b.png?token=x", html, StringComparison.Ordinal);
        Assert.DoesNotContain("cloud://", html, StringComparison.Ordinal);
        Assert.DoesNotContain("other", document.CustomFields.Keys);
        Assert.Contains(api.Requests, uri => uri.AbsolutePath == "/v1/storages/object/sign/media");
    }

    [Fact]
    public async Task LoadRawAsync_DataModeFileFields_AreSignedAndOtherReferencesDropped()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"p1","title":"Photo","photo":"cloud://env-abc.media/gallery/p1.jpg","other":"cloud://env-abc.media/x.png"}]""";
        api.Total = 1;
        var source = Create(api, options => options with
        {
            Mode = "data",
            FileFields = ["photo"],
            StorageBucket = "media",
            DefaultImageUrl = "/ph.jpg",
            FieldPolicyMode = "all"
        });

        var document = Assert.Single((await source.LoadRawAsync()).Documents);

        Assert.Equal(
            "https://env-abc.api.tcloudbasegateway.com/v1/storages/object/sign/media/gallery/p1.jpg?token=x",
            document.CustomFields!["photo"].Value);
        Assert.DoesNotContain("other", document.CustomFields.Keys);
        Assert.Null(document.Body.Markdown);
    }

    [Fact]
    public async Task LoadRawAsync_FileSignError_FallsBackToPlaceholder()
    {
        var api = new FakeCloudBaseApi { SignError = "denied" };
        api.Pages[0] = """[{"id":"a1","title":"A","slug":"a","cover":"posts/a.png","content":"x"}]""";
        api.Total = 1;
        var source = Create(api, options => options with
        {
            FileFields = ["cover"],
            StorageBucket = "media",
            DefaultImageUrl = "/ph.jpg"
        });

        var document = Assert.Single((await source.LoadRawAsync()).Documents);

        Assert.Equal("/ph.jpg", document.CustomFields!["cover"].Value);
    }

    [Theory]
    [InlineData("all", """["posts/a.png","posts/b.png"]""")]
    [InlineData("whitelist", """["posts/a.png"]""")]
    [InlineData("whitelist", "[]")]
    [InlineData("whitelist", """{"fileID":"cloud://env-abc.media/a.png"}""")]
    [InlineData("all", "42")]
    public async Task LoadRawAsync_NonStringFileField_UsesPlaceholderWithoutSigning(string mode, string gallery)
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = $$"""[{"id":"a1","title":"A","slug":"a","gallery":{{gallery}},"content":"x"}]""";
        api.Total = 1;
        var messages = new List<string>();
        var source = Create(api, options => options with
        {
            FieldPolicyMode = mode,
            FileFields = ["gallery"],
            StorageBucket = "media",
            DefaultImageUrl = "/ph.jpg"
        }, new ListLogger(messages));

        var document = Assert.Single((await source.LoadRawAsync()).Documents);

        Assert.Equal("/ph.jpg", document.CustomFields!["gallery"].Value);
        Assert.DoesNotContain(api.Requests, uri => uri.AbsolutePath.StartsWith("/v1/storages/", StringComparison.Ordinal));
        Assert.Contains(messages, message =>
            message.Contains("event=cloudbase.unsupported_file_reference", StringComparison.Ordinal) &&
            message.Contains("count=1", StringComparison.Ordinal));
        Assert.DoesNotContain(messages, message => message.Contains("file_resolve_failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadRawAsync_SignRequestForbidden_LogsEntryAndStatus()
    {
        var api = new FakeCloudBaseApi { SignStatus = HttpStatusCode.Forbidden };
        api.Pages[0] = """[{"id":"a1","title":"A","slug":"a","cover":"posts/a.png","content":"x"}]""";
        api.Total = 1;
        var messages = new List<string>();
        var source = Create(api, options => options with
        {
            FileFields = ["cover"],
            StorageBucket = "media",
            DefaultImageUrl = "/ph.jpg"
        }, new ListLogger(messages));

        var document = Assert.Single((await source.LoadRawAsync()).Documents);

        Assert.Equal("/ph.jpg", document.CustomFields!["cover"].Value);
        Assert.Contains(messages, message =>
            message.Contains("event=cloudbase.file_resolve_failed", StringComparison.Ordinal) &&
            message.Contains("count=1", StringComparison.Ordinal) &&
            message.Contains($"entries=\"{CloudBaseContentSource.LogEntry("a1", "cover")}: sign-request-failed Forbidden 403\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadRawAsync_ItemSignError_LogsEveryReferencingEntry()
    {
        var api = new FakeCloudBaseApi { SignError = "denied" };
        api.Pages[0] = """[{"id":"a1","title":"A","slug":"a","cover":"posts/a.png","content":"x"},{"id":"a2","title":"B","slug":"b","cover":"posts/a.png","content":"y"}]""";
        api.Total = 2;
        var messages = new List<string>();
        var source = Create(api, options => options with
        {
            FileFields = ["cover"],
            StorageBucket = "media",
            DefaultImageUrl = "/ph.jpg"
        }, new ListLogger(messages));

        await source.LoadRawAsync();

        var warning = Assert.Single(messages, message => message.Contains("file_resolve_failed", StringComparison.Ordinal));
        Assert.Contains("count=2", warning, StringComparison.Ordinal);
        Assert.Contains($"{CloudBaseContentSource.LogEntry("a1", "cover")}: object-unavailable", warning, StringComparison.Ordinal);
        Assert.Contains($"{CloudBaseContentSource.LogEntry("a2", "cover")}: object-unavailable", warning, StringComparison.Ordinal);
        Assert.DoesNotContain("posts/a.png", warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadRawAsync_RepeatedFailures_AreLoggedOnceWithHashedId()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a\"1;x","title":"A","slug":"a","content":"![a](cloud://other-env.media/a.png) ![b](cloud://other-env.media/b.png)"}]""";
        api.Total = 1;
        var messages = new List<string>();
        var source = Create(api, options => options with
        {
            StorageBucket = "media",
            DefaultImageUrl = "/ph.jpg"
        }, new ListLogger(messages));

        await source.LoadRawAsync();

        var warning = Assert.Single(messages, message => message.Contains("file_resolve_failed", StringComparison.Ordinal));
        Assert.Contains("count=1", warning, StringComparison.Ordinal);
        Assert.Contains($"entries=\"{CloudBaseContentSource.LogEntry("a\"1;x", "body")}: reference-mismatch\"", warning, StringComparison.Ordinal);
        Assert.DoesNotContain("a\"1;x", warning, StringComparison.Ordinal);
        Assert.Matches("row:[0-9a-f]{12}/body", warning);
    }

    [Theory]
    [InlineData("cloud://other-env.media/posts/a.png")]
    [InlineData("posts/../a.png")]
    public async Task LoadRawAsync_InvalidFileField_FallsBackToPlaceholder(string value)
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = $$"""[{"id":"a1","title":"A","slug":"a","cover":"{{value}}","content":"x"}]""";
        api.Total = 1;
        var source = Create(api, options => options with
        {
            FileFields = ["cover"],
            StorageBucket = "media",
            DefaultImageUrl = "/ph.jpg"
        });

        var document = Assert.Single((await source.LoadRawAsync()).Documents);

        Assert.Equal("/ph.jpg", document.CustomFields!["cover"].Value);
    }

    [Theory]
    [InlineData("![a](cloud&#58;//env-abc.media/posts/a.png)", true)]
    [InlineData("![a](cloud\\://env-abc.media/posts/a.png)", true)]
    [InlineData("![a](cloud&#58;//env-abc.media/posts/a.png)", false)]
    [InlineData("![a](cloud\\:\\/\\/env-abc.media/posts/a.png)", false)]
    [InlineData("![a](cloud&colon;&sol;&sol;env-abc.media/posts/a.png)", false)]
    [InlineData("![a](&#99;loud://env-abc.media/posts/a.png)", false)]
    public async Task LoadRawAsync_EscapedBodyCloudImage_IsSigned(string body, bool withFileFields)
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = $$"""[{"id":"a1","title":"A","slug":"a","content":{{System.Text.Json.JsonSerializer.Serialize(body)}}}]""";
        api.Total = 1;
        var source = Create(api, options => options with
        {
            FileFields = withFileFields ? ["cover"] : [],
            StorageBucket = "media",
            DefaultImageUrl = "/ph.jpg"
        });

        var result = await source.LoadRawAsync();

        var document = Assert.Single(result.Documents);
        var html = (await result.BodyStore.GetAsync(document)).Html;
        Assert.Contains("https://env-abc.api.tcloudbasegateway.com/v1/storages/object/sign/media/posts/a.png?token=x", html, StringComparison.Ordinal);
        Assert.DoesNotContain("cloud://", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("plain text: no links", false)]
    [InlineData("Tom &amp; Jerry & friends", false)]
    [InlineData("see cloud://env-abc.media/a.png", true)]
    [InlineData("CLOUD\\:\\/\\/x", true)]
    [InlineData("cloud&#58;//x", true)]
    [InlineData("cloud&COLON;//x", true)]
    [InlineData("", false)]
    public void MayHoldCloudReference_MatchesDecodableSchemes(string markdown, bool expected)
    {
        Assert.Equal(expected, CloudBaseFileResolver.MayHoldCloudReference(markdown));
    }

    [Fact]
    public async Task LoadRawAsync_InvalidBodyCloudImage_UsesPlaceholder()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"A","slug":"a","content":"![x](cloud://other-env.media/posts/a.png)"}]""";
        api.Total = 1;
        var source = Create(api, options => options with { DefaultImageUrl = "/ph.jpg" });

        var result = await source.LoadRawAsync();
        var document = Assert.Single(result.Documents);

        var html = (await result.BodyStore.GetAsync(document)).Html;
        Assert.Contains("/ph.jpg", html, StringComparison.Ordinal);
        Assert.DoesNotContain("cloud://", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadRawAsync_NonImageCloudReference_DegradesToText()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"A","slug":"a","content":"[pdf][r]\n\n[r]: cloud://env-abc.media/posts/a.pdf"}]""";
        api.Total = 1;
        var source = Create(api, options => options with { DefaultImageUrl = "/ph.jpg" });

        var result = await source.LoadRawAsync();
        var document = Assert.Single(result.Documents);

        var html = (await result.BodyStore.GetAsync(document)).Html;
        Assert.Equal("<p>pdf</p>", html);
    }

    [Theory]
    [InlineData("[Download the PDF](cloud://env-abc.media/posts/a.pdf)", "<p>Download the PDF</p>")]
    [InlineData("see <cloud://env-abc.media/posts/a.pdf> now", "<p>see  now</p>")]
    [InlineData("[**bold** text](javascript:alert(1))", "<p><strong>bold</strong> text</p>")]
    public async Task LoadRawAsync_RefusedLink_RendersAsPlainText(string markdown, string expected)
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new Dictionary<string, string> { ["id"] = "a1", ["title"] = "A", ["slug"] = "a", ["content"] = markdown }
        });
        api.Total = 1;
        var source = Create(api, options => options with { DefaultImageUrl = "/ph.jpg" });

        var result = await source.LoadRawAsync();
        var document = Assert.Single(result.Documents);

        Assert.Equal(expected, (await result.BodyStore.GetAsync(document)).Html);
    }

    [Fact]
    public async Task LoadRawAsync_WhitelistSelectRejected_RetriesWithoutSelect()
    {
        // A table without the default slug/publish_at/updated_at columns rejects the select with 400.
        var api = new FakeCloudBaseApi { RejectSelect = true };
        api.Pages[0] = """[{"id":"a1","title":"A","content":"x","secret":"s"}]""";
        api.Total = 1;
        var source = Create(api, options => options with { FieldPolicyMode = "whitelist" });

        var document = Assert.Single((await source.LoadRawAsync()).Documents);

        Assert.Equal(2, api.Requests.Count);
        Assert.Contains("select=", Uri.UnescapeDataString(api.Requests[0].Query), StringComparison.Ordinal);
        Assert.DoesNotContain("select=", Uri.UnescapeDataString(api.Requests[1].Query), StringComparison.Ordinal);
        Assert.DoesNotContain("secret", document.CustomFields!.Keys);
    }

    [Fact]
    public async Task LoadRawAsync_DuplicateId_ThrowsResponseInvalid()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"One","content":"x"},{"id":"a1","title":"Two","content":"y"}]""";
        api.Total = 2;
        var source = Create(api, options => options);

        var ex = await Assert.ThrowsAsync<ContentException>(() => source.LoadRawAsync());

        Assert.Equal(DiagnosticCode.CloudBaseResponseInvalid, ex.Code);
    }

    [Theory]
    [InlineData("1732406400000000")]
    [InlineData("9223372036854775807")]
    [InlineData("-62135596801")]
    public async Task LoadRawAsync_OutOfRangeEpoch_IsTreatedAsMissing(string epoch)
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = $$"""[{"id":"a1","title":"A","publish_at":{{epoch}},"updated_at":{{epoch}},"content":"x"}]""";
        api.Total = 1;
        var source = Create(api, options => options);

        var document = Assert.Single((await source.LoadRawAsync()).Documents);

        Assert.Null(document.PublishedAt);
        Assert.DoesNotContain("last_edited_time", document.CustomFields!.Keys);
    }

    [Fact]
    public async Task LoadRawAsync_ModeData_DropsCloudReferenceFields()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"A","note":"see cloud://env-abc.media/a.png","tag":"kept"}]""";
        api.Total = 1;
        var source = Create(api, options => options with
        {
            Mode = "data",
            FieldPolicyMode = "whitelist",
            AllowedFields = ["note", "tag"]
        });

        var document = Assert.Single((await source.LoadRawAsync()).Documents);

        Assert.DoesNotContain("note", document.CustomFields!.Keys);
        Assert.Equal("kept", document.CustomFields["tag"].Value);
        Assert.DoesNotContain(api.Requests, uri => uri.AbsolutePath.StartsWith("/v1/storages/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadRawAsync_EmptyBucketCloudImage_UsesPlaceholder()
    {
        // No fileFields and no storageBucket: an empty-bucket reference must not reach the sign call.
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"A","content":"![x](cloud://env-abc./a.png)"}]""";
        api.Total = 1;
        var source = Create(api, options => options with { DefaultImageUrl = "/ph.jpg" });

        var result = await source.LoadRawAsync();
        var document = Assert.Single(result.Documents);

        var html = (await result.BodyStore.GetAsync(document)).Html;
        Assert.Contains("/ph.jpg", html, StringComparison.Ordinal);
        Assert.DoesNotContain("cloud://", html, StringComparison.Ordinal);
        Assert.DoesNotContain(api.Requests, uri => uri.AbsolutePath.StartsWith("/v1/storages/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadRawAsync_CodeFenceCloudReference_IsUntouched()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"A","slug":"a","content":"```\n![x](cloud://env-abc.media/posts/b.png)\n```"}]""";
        api.Total = 1;
        var source = Create(api, options => options with
        {
            StorageBucket = "media",
            DefaultImageUrl = "/ph.jpg"
        });

        var result = await source.LoadRawAsync();
        var document = Assert.Single(result.Documents);
        var html = (await result.BodyStore.GetAsync(document)).Html;

        Assert.Contains("cloud://env-abc.media/posts/b.png", html, StringComparison.Ordinal);
        Assert.DoesNotContain("token=", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadRawAsync_ListFieldWithCloudReference_IsDropped()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"A","slug":"a","content":"x","gallery":["cloud://env-abc.media/posts/a.png"]}]""";
        api.Total = 1;
        var source = Create(api, options => options with { FieldPolicyMode = "all" });

        var document = Assert.Single((await source.LoadRawAsync()).Documents);

        Assert.DoesNotContain("gallery", document.CustomFields!.Keys);
    }

    [Theory]
    [InlineData("content")]
    [InlineData("data")]
    public async Task LoadRawAsync_NestedCloudReference_DropsWholeFieldWithWarning(string mode)
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"A","slug":"a","content":"x","days":[{"title":"D1","photos":[{"src":"cloud://env-abc.media/a.png"}]}],"keyed":{"cloud://x":1},"months":[3,4]}]""";
        api.Total = 1;
        var messages = new List<string>();
        var source = Create(
            api,
            options => options with { Mode = mode, FieldPolicyMode = "whitelist", AllowedFields = ["days", "keyed", "months"] },
            new ListLogger(messages));

        var document = Assert.Single((await source.LoadRawAsync()).Documents);

        Assert.DoesNotContain("days", document.CustomFields!.Keys);
        Assert.DoesNotContain("keyed", document.CustomFields.Keys);
        Assert.Equal([3L, 4L], Assert.IsType<List<object?>>(document.CustomFields["months"].Value));
        Assert.Contains(messages, message =>
            message.Contains("event=cloudbase.unsupported_file_reference", StringComparison.Ordinal) &&
            message.Contains("count=2", StringComparison.Ordinal));
        Assert.DoesNotContain(api.Requests, uri => uri.AbsolutePath.StartsWith("/v1/storages/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadRawAsync_RejectedStructureInFileField_UsesPlaceholderWithoutDiagnostic()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"A","slug":"a","content":"x","cover":{"a":[[[[[[[[1]]]]]]]]}}]""";
        api.Total = 1;
        var source = Create(api, options => options with
        {
            FileFields = ["cover"],
            StorageBucket = "media",
            DefaultImageUrl = "/ph.jpg"
        });

        var document = Assert.Single((await source.LoadRawAsync()).Documents);

        Assert.Equal("/ph.jpg", document.CustomFields!["cover"].Value);
        Assert.Empty(document.Diagnostics);
        Assert.DoesNotContain(api.Requests, uri => uri.AbsolutePath.StartsWith("/v1/storages/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadRawAsync_RejectedStructuredField_IsDroppedWithErrorDiagnostic()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"A","slug":"a","content":"x","deep":{"a":[[[[[[[[1]]]]]]]]},"ok":{"k":"v"}}]""";
        api.Total = 1;
        var source = Create(api, options => options with { FieldPolicyMode = "whitelist", AllowedFields = ["deep", "ok"] });

        var document = Assert.Single((await source.LoadRawAsync()).Documents);

        Assert.DoesNotContain("deep", document.CustomFields!.Keys);
        Assert.Contains("ok", document.CustomFields.Keys);
        var diagnostic = Assert.Single(document.Diagnostics);
        Assert.Equal(CloudBaseContentSource.StructuredFieldInvalidCode, diagnostic.Code);
        Assert.Equal("error", diagnostic.Severity);
        Assert.Equal("deep", diagnostic.Field);
        Assert.DoesNotContain("a1", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadRawAsync_MixedBodyImages_KeepsOrdinaryImages()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"A","slug":"a","content":"![a](https://example.com/a.png)\n![b](cloud://env-abc.media/b.png)"}]""";
        api.Total = 1;
        var source = Create(api, options => options with
        {
            StorageBucket = "media",
            DefaultImageUrl = "/ph.jpg"
        });

        var result = await source.LoadRawAsync();
        var document = Assert.Single(result.Documents);

        var html = (await result.BodyStore.GetAsync(document)).Html;
        Assert.Contains("https://example.com/a.png", html, StringComparison.Ordinal);
        Assert.DoesNotContain("cloud://", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadRawAsync_DuplicateReferenceDefinitions_DoNotThrow()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"A","slug":"a","content":"![x][r]\n\n[r]: cloud://env-abc.media/a.png\n[r]: cloud://env-abc.media/b.png"}]""";
        api.Total = 1;
        var source = Create(api, options => options with
        {
            StorageBucket = "media",
            DefaultImageUrl = "/ph.jpg"
        });

        var result = await source.LoadRawAsync();
        var document = Assert.Single(result.Documents);
        Assert.DoesNotContain("cloud://", (await result.BodyStore.GetAsync(document)).Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Snapshot_RecordsPublicRowsAndReplaysThemOfflineWithoutKeyOrNetwork()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-cloudbase-snapshot-" + Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, "snapshot");
        try
        {
            var api = new FakeCloudBaseApi { Total = 1 };
            api.Pages[0] = """[{"id":"a1","title":"A","slug":"a","cover":"posts/a.png","content":"![x](cloud://env-abc.media/posts/b.png)","tags":["t"],"user_id":"u-secret","meta":{"owner":"o-secret","city":"KL"}}]""";
            Func<CloudBaseContentSourceOptions, CloudBaseContentSourceOptions> configure = options => options with
            {
                FileFields = ["cover"],
                StorageBucket = "media",
                DefaultImageUrl = "/ph.jpg",
                FieldPolicyMode = "all",
                FilterField = "release_id",
                FilterValue = "r1"
            };

            using (var recording = Bukit.Content.Notion.Snapshot.NotionSnapshotSession.BeginWrite(directory))
            {
                using (Bukit.Config.NotionSnapshotScope.Enter(recording))
                {
                    await Create(api, configure).LoadRawAsync();
                }

                recording.Commit();
            }

            var entry = File.ReadAllText(Assert.Single(Directory.GetFiles(Path.Combine(directory, "cloudbase"), "*.json", SearchOption.AllDirectories)));
            Assert.DoesNotContain("secret", entry, StringComparison.Ordinal);
            Assert.DoesNotContain("token=", entry, StringComparison.Ordinal);
            Assert.Contains("KL", entry, StringComparison.Ordinal);

            var offlineApi = new FakeCloudBaseApi();
            using var offline = Bukit.Content.Notion.Snapshot.NotionSnapshotSession.OpenOffline(directory);
            using (Bukit.Config.NotionSnapshotScope.Enter(offline))
            {
                var result = await Create(offlineApi, options => configure(options) with { PublishableKey = string.Empty }).LoadRawAsync();

                var document = Assert.Single(result.Documents);
                Assert.Equal("A", document.Title);
                Assert.Equal(
                    "https://env-abc.api.tcloudbasegateway.com/v1/storages/object/sign/media/posts/a.png",
                    document.CustomFields!["cover"].Value);
                Assert.DoesNotContain("user_id", document.CustomFields.Keys);
                var html = (await result.BodyStore.GetAsync(document)).Html;
                Assert.Contains("/v1/storages/object/sign/media/posts/b.png", html, StringComparison.Ordinal);
                Assert.Empty(offlineApi.Requests);

                var otherRelease = await Assert.ThrowsAsync<Bukit.Content.Notion.Snapshot.NotionSnapshotException>(
                    () => Create(offlineApi, options => configure(options) with { FilterValue = "r2" }).LoadRawAsync());
                Assert.Equal(DiagnosticCode.NotionSnapshotQueryMismatch, otherRelease.Code);
                var otherTable = await Assert.ThrowsAsync<Bukit.Content.Notion.Snapshot.NotionSnapshotException>(
                    () => Create(offlineApi, options => configure(options) with { Table = "pages" }).LoadRawAsync());
                Assert.Equal(DiagnosticCode.NotionSnapshotDatabaseMissing, otherTable.Code);
                Assert.Contains("CloudBase table 'pages'", otherTable.Message, StringComparison.Ordinal);
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Snapshot_WhitelistRecordsOnlyPublishedColumns()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-cloudbase-snapshot-" + Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, "snapshot");
        try
        {
            var api = new FakeCloudBaseApi { Total = 1 };
            api.Pages[0] = """[{"id":"a1","title":"A","slug":"a","content":"x","tags":["t"],"internal_note":"hidden-note"}]""";
            using var recording = Bukit.Content.Notion.Snapshot.NotionSnapshotSession.BeginWrite(directory);
            using (Bukit.Config.NotionSnapshotScope.Enter(recording))
            {
                await Create(api, options => options with { FieldPolicyMode = "whitelist", AllowedFields = ["tags"] }).LoadRawAsync();
            }

            recording.Commit();
            var entry = File.ReadAllText(Assert.Single(Directory.GetFiles(Path.Combine(directory, "cloudbase"), "*.json", SearchOption.AllDirectories)));

            Assert.Contains("\"tags\"", entry, StringComparison.Ordinal);
            Assert.DoesNotContain("hidden-note", entry, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Snapshot_SourcesOnOneTableKeepTheirOwnRowsAndAChangedSourceFails()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-cloudbase-snapshot-" + Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, "snapshot");
        try
        {
            var api = new FakeCloudBaseApi { Total = 1 };
            api.Pages[0] = """[{"id":"a1","title":"A","slug":"a","content":"x","tags":["t"],"price":9,"city":"吉隆坡"}]""";
            Func<CloudBaseContentSourceOptions, CloudBaseContentSourceOptions> tags = options => options with { AllowedFields = ["tags", "city"] };
            Func<CloudBaseContentSourceOptions, CloudBaseContentSourceOptions> price = options => options with { AllowedFields = ["price"] };

            using (var recording = Bukit.Content.Notion.Snapshot.NotionSnapshotSession.BeginWrite(directory))
            {
                using (Bukit.Config.NotionSnapshotScope.Enter(recording))
                {
                    await Create(api, tags).LoadRawAsync();
                    await Create(api, price).LoadRawAsync();
                }

                recording.Commit();
            }

            var entries = Directory.GetFiles(Path.Combine(directory, "cloudbase"), "*.json", SearchOption.AllDirectories);
            Assert.Equal(2, entries.Length);
            Assert.Contains(entries, entry => File.ReadAllText(entry).Contains("吉隆坡", StringComparison.Ordinal));

            using var offline = Bukit.Content.Notion.Snapshot.NotionSnapshotSession.OpenOffline(directory);
            using (Bukit.Config.NotionSnapshotScope.Enter(offline))
            {
                var offlineApi = new FakeCloudBaseApi();
                var tagged = Assert.Single((await Create(offlineApi, tags).LoadRawAsync()).Documents);
                Assert.Contains("tags", tagged.CustomFields!.Keys);
                Assert.DoesNotContain("price", tagged.CustomFields.Keys);
                var priced = Assert.Single((await Create(offlineApi, price).LoadRawAsync()).Documents);
                Assert.Contains("price", priced.CustomFields!.Keys);
                Assert.DoesNotContain("tags", priced.CustomFields.Keys);
                Assert.Empty(offlineApi.Requests);

                var changed = await Assert.ThrowsAsync<Bukit.Content.Notion.Snapshot.NotionSnapshotException>(
                    () => Create(offlineApi, options => tags(options) with { MaxItems = 1 }).LoadRawAsync());
                Assert.Equal(DiagnosticCode.NotionSnapshotQueryMismatch, changed.Code);
                Assert.Contains("different source settings", changed.Message, StringComparison.Ordinal);
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Snapshot_AllModeMatchesTheIdPublication()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-cloudbase-snapshot-" + Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, "snapshot");
        try
        {
            var api = new FakeCloudBaseApi { Total = 1 };
            api.Pages[0] = """[{"id":"a1","title":"A","slug":"a","content":"x","city":"KL"}]""";
            Func<CloudBaseContentSourceOptions, CloudBaseContentSourceOptions> all = options => options with { FieldPolicyMode = "all", AllowedFields = ["city"] };
            using (var recording = Bukit.Content.Notion.Snapshot.NotionSnapshotSession.BeginWrite(directory))
            {
                using (Bukit.Config.NotionSnapshotScope.Enter(recording))
                {
                    Assert.Single((await Create(api, all).LoadRawAsync()).Documents);
                }

                recording.Commit();
            }

            using var offline = Bukit.Content.Notion.Snapshot.NotionSnapshotSession.OpenOffline(directory);
            Bukit.Content.CloudBase.CloudBaseSnapshot.EnsureSource(offline, all(Options()) with { AllowedFields = ["tags"] });
            var check = Assert.Throws<Bukit.Content.Notion.Snapshot.NotionSnapshotException>(
                () => Bukit.Content.CloudBase.CloudBaseSnapshot.EnsureSource(offline, all(Options()) with { AllowedFields = ["ID"] }));
            Assert.Contains("different source settings", check.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Snapshot_MatchesFieldListsIgnoringOrderButNotCaseOrTheMode()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-cloudbase-snapshot-" + Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, "snapshot");
        try
        {
            // Nine levels: deeper than a structured field may be, so it is dropped online and left out of the entry.
            var deep = string.Concat(Enumerable.Repeat("{\"a\":", 9)) + "1" + new string('}', 9);
            var api = new FakeCloudBaseApi { Total = 1 };
            api.Pages[0] = $$"""[{"id":"a1","title":"A","slug":"a","content":"x","tags":["t"],"city":"KL","deep":{{deep}}}]""";
            using (var recording = Bukit.Content.Notion.Snapshot.NotionSnapshotSession.BeginWrite(directory))
            {
                using (Bukit.Config.NotionSnapshotScope.Enter(recording))
                {
                    var online = Assert.Single((await Create(api, options => options with { AllowedFields = ["tags", "city", "deep"] }).LoadRawAsync()).Documents);
                    Assert.DoesNotContain("deep", online.CustomFields!.Keys);
                    Assert.Contains(online.Diagnostics, d => d.Code == CloudBaseContentSource.StructuredFieldInvalidCode && d.Field == "deep");
                }

                recording.Commit();
            }

            Assert.DoesNotContain("\"deep\":{", File.ReadAllText(Assert.Single(Directory.GetFiles(Path.Combine(directory, "cloudbase"), "*.json", SearchOption.AllDirectories))), StringComparison.Ordinal);
            using var offline = Bukit.Content.Notion.Snapshot.NotionSnapshotSession.OpenOffline(directory);
            using (Bukit.Config.NotionSnapshotScope.Enter(offline))
            {
                var offlineApi = new FakeCloudBaseApi();
                Func<CloudBaseContentSourceOptions, CloudBaseContentSourceOptions> reordered = options => options with { AllowedFields = ["deep", "city", "tags"] };
                var document = Assert.Single((await Create(offlineApi, reordered).LoadRawAsync()).Documents);
                Assert.Equal("KL", document.CustomFields!["city"].Value);
                Assert.DoesNotContain("deep", document.CustomFields.Keys);
                // The rejection is replayed, so schemaFailMode: strict fails offline as online.
                var diagnostic = Assert.Single(document.Diagnostics, d => d.Code == CloudBaseContentSource.StructuredFieldInvalidCode);
                Assert.Equal("deep", diagnostic.Field);
                Assert.Equal("error", diagnostic.Severity);
                Bukit.Content.CloudBase.CloudBaseSnapshot.EnsureSource(offline, reordered(Options()));
                Assert.Throws<Bukit.Content.Notion.Snapshot.NotionSnapshotException>(
                    () => Bukit.Content.CloudBase.CloudBaseSnapshot.EnsureSource(offline, reordered(Options()) with { AllowedFields = ["deep", "CITY", "tags"] }));

                var data = await Assert.ThrowsAsync<Bukit.Content.Notion.Snapshot.NotionSnapshotException>(
                    () => Create(offlineApi, options => reordered(options) with { Mode = "data" }).LoadRawAsync());
                Assert.Equal(DiagnosticCode.NotionSnapshotQueryMismatch, data.Code);
                var check = Assert.Throws<Bukit.Content.Notion.Snapshot.NotionSnapshotException>(
                    () => Bukit.Content.CloudBase.CloudBaseSnapshot.EnsureSource(offline, reordered(Options()) with { MaxItems = 1 }));
                Assert.Contains("different source settings", check.Message, StringComparison.Ordinal);
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Snapshot_EditedFileIdentityIsNotPublished()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-cloudbase-snapshot-" + Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, "snapshot");
        try
        {
            var api = new FakeCloudBaseApi { Total = 1 };
            api.Pages[0] = """[{"id":"a1","title":"A","slug":"a","content":"x","cover":"posts/a.png"}]""";
            Func<CloudBaseContentSourceOptions, CloudBaseContentSourceOptions> configure = options => options with
            {
                FileFields = ["cover"],
                StorageBucket = "media",
                DefaultImageUrl = "/ph.jpg"
            };
            using (var recording = Bukit.Content.Notion.Snapshot.NotionSnapshotSession.BeginWrite(directory))
            {
                using (Bukit.Config.NotionSnapshotScope.Enter(recording))
                {
                    await Create(api, configure).LoadRawAsync();
                }

                recording.Commit();
            }

            var path = Assert.Single(Directory.GetFiles(Path.Combine(directory, "cloudbase"), "*.json", SearchOption.AllDirectories));
            File.WriteAllText(path, File.ReadAllText(path).Replace(
                "https://env-abc.api.tcloudbasegateway.com/v1/storages/object/sign/media/posts/a.png",
                "https://elsewhere.example/a.png",
                StringComparison.Ordinal));

            using var offline = Bukit.Content.Notion.Snapshot.NotionSnapshotSession.OpenOffline(directory);
            using (Bukit.Config.NotionSnapshotScope.Enter(offline))
            {
                var document = Assert.Single((await Create(new FakeCloudBaseApi(), options => configure(options) with { PublishableKey = string.Empty }).LoadRawAsync()).Documents);
                Assert.Equal("/ph.jpg", document.CustomFields!["cover"].Value);
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static CloudBaseContentSourceOptions Options() => new()
    {
        EnvId = "env-abc",
        Table = "articles",
        BodyField = "content",
        PublishableKey = "pk_test"
    };

    private static CloudBaseContentSource Create(
        FakeCloudBaseApi api,
        Func<CloudBaseContentSourceOptions, CloudBaseContentSourceOptions> configure,
        ILogger? logger = null)
    {
        var options = configure(new CloudBaseContentSourceOptions
        {
            EnvId = "env-abc",
            Table = "articles",
            BodyField = "content",
            PublishableKey = "pk_test"
        });
        return new CloudBaseContentSource(
            options,
            logger,
            () => new Bukit.CloudBase.Transport.CloudBaseClient(
                new Bukit.CloudBase.Transport.CloudBaseClientOptions
                {
                    EnvId = options.EnvId,
                    Region = options.Region,
                    PublishableKey = options.PublishableKey,
                    MaxRps = 1000,
                    MaxRetries = 0
                },
                api));
    }

    private sealed class ListLogger(List<string> messages) : ILogger
    {
        public void Debug(string message) => messages.Add(message);
        public void Info(string message) => messages.Add(message);
        public void Warn(string message) => messages.Add(message);
        public void Error(string message) => messages.Add(message);
    }

    private sealed class FakeCloudBaseApi : HttpMessageHandler
    {
        public Dictionary<int, string> Pages { get; } = new();
        public List<Uri> Requests { get; } = [];
        public int Total { get; set; } = 3;
        public bool Unauthorized { get; set; }
        public string? SignError { get; set; }
        public HttpStatusCode? SignStatus { get; set; }
        public bool RejectSelect { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            if (request.Method == HttpMethod.Post &&
                request.RequestUri!.AbsolutePath == "/v1/storages/object/sign/media")
            {
                if (SignStatus is { } signStatus)
                {
                    return new HttpResponseMessage(signStatus)
                    {
                        Content = new StringContent("""{"code":"DENIED"}""", Encoding.UTF8, "application/json")
                    };
                }

                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                using var payload = System.Text.Json.JsonDocument.Parse(body);
                var items = payload.RootElement.GetProperty("paths").EnumerateArray()
                    .Select(path => SignError is null
                        ? $$"""{"path":"{{path.GetString()}}","signedURL":"/v1/storages/object/sign/media/{{path.GetString()}}?token=x"}"""
                        : $$"""{"path":"{{path.GetString()}}","error":"{{SignError}}"}""");
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"[{string.Join(",", items)}]", Encoding.UTF8, "application/json")
                };
            }

            if (Unauthorized)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("""{"code":"AUTH"}""", Encoding.UTF8, "application/json")
                };
            }

            if (RejectSelect && request.RequestUri!.Query.Contains("select=", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("""{"code":"DATABASE_42703"}""", Encoding.UTF8, "application/json")
                };
            }

            var offset = ParseOffset(request.RequestUri!.Query);
            var pageBody = Pages.TryGetValue(offset, out var page) ? page : "[]";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(pageBody, Encoding.UTF8, "application/json")
            };
            response.Content.Headers.TryAddWithoutValidation("Content-Range", $"*/{Total}");
            return response;
        }

        private static int ParseOffset(string query)
        {
            foreach (var part in query.TrimStart('?').Split('&'))
            {
                if (part.StartsWith("offset=", StringComparison.Ordinal))
                {
                    return int.TryParse(part["offset=".Length..], out var value) ? value : 0;
                }
            }

            return 0;
        }
    }
}
