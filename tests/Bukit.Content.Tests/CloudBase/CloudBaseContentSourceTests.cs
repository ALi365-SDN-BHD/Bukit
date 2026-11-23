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
        Assert.Equal(["last_edited_time", "tags", "title"], first.CustomFields!.Keys.Order(StringComparer.Ordinal));
        Assert.DoesNotContain("user_id", first.CustomFields.Keys);
        Assert.Equal("date", first.CustomFields["last_edited_time"].Type);

        var body = await result.BodyStore.GetAsync(first);
        Assert.Contains("<h1", body.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript:", body.Html, StringComparison.OrdinalIgnoreCase);
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
    public async Task LoadRawAsync_BlankSlug_FallsBackToId()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"  ","slug":"   ","content":"x"}]""";
        api.Total = 1;
        var source = Create(api, options => options);

        var document = Assert.Single((await source.LoadRawAsync()).Documents);

        Assert.Equal("a1", document.Title);
        Assert.Equal("a1", document.Slug);
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
        var cover = Assert.IsType<string>(document.CustomFields!["cover"].Value);
        Assert.Equal(
            "https://env-abc.api.tcloudbasegateway.com/v1/storages/object/sign/media/posts/a.png?token=x",
            cover);
        Assert.Equal(
            "![x](https://env-abc.api.tcloudbasegateway.com/v1/storages/object/sign/media/posts/b.png?token=x)",
            document.Body.Markdown);
        Assert.DoesNotContain("cloud://", document.Body.Markdown!, StringComparison.Ordinal);
        Assert.DoesNotContain("other", document.CustomFields.Keys);
        Assert.Contains(api.Requests, uri => uri.AbsolutePath == "/v1/storages/object/sign/media");
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

    [Fact]
    public async Task LoadRawAsync_InvalidBodyCloudImage_UsesPlaceholder()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"A","slug":"a","content":"![x](cloud://other-env.media/posts/a.png)"}]""";
        api.Total = 1;
        var source = Create(api, options => options with { DefaultImageUrl = "/ph.jpg" });

        var document = Assert.Single((await source.LoadRawAsync()).Documents);

        Assert.Equal("![x](/ph.jpg)", document.Body.Markdown);
        Assert.DoesNotContain("cloud://", document.Body.Markdown!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadRawAsync_NonImageCloudReference_DegradesToText()
    {
        var api = new FakeCloudBaseApi();
        api.Pages[0] = """[{"id":"a1","title":"A","slug":"a","content":"[pdf][r]\n\n[r]: cloud://env-abc.media/posts/a.pdf"}]""";
        api.Total = 1;
        var source = Create(api, options => options with { DefaultImageUrl = "/ph.jpg" });

        var document = Assert.Single((await source.LoadRawAsync()).Documents);

        Assert.Equal("pdf", document.Body.Markdown!.TrimEnd());
        Assert.DoesNotContain("cloud://", document.Body.Markdown!, StringComparison.Ordinal);
        Assert.DoesNotContain("a.pdf", document.Body.Markdown!, StringComparison.Ordinal);
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

        var document = Assert.Single((await source.LoadRawAsync()).Documents);

        Assert.Contains("![a](https://example.com/a.png)", document.Body.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("cloud://", document.Body.Markdown!, StringComparison.Ordinal);
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

        var document = Assert.Single((await source.LoadRawAsync()).Documents);
        Assert.DoesNotContain("cloud://", document.Body.Markdown!, StringComparison.Ordinal);
    }

    private static CloudBaseContentSource Create(
        FakeCloudBaseApi api,
        Func<CloudBaseContentSourceOptions, CloudBaseContentSourceOptions> configure)
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
            logger: null,
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

    private sealed class FakeCloudBaseApi : HttpMessageHandler
    {
        public Dictionary<int, string> Pages { get; } = new();
        public List<Uri> Requests { get; } = [];
        public int Total { get; set; } = 3;
        public bool Unauthorized { get; set; }
        public string? SignError { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            if (request.Method == HttpMethod.Post &&
                request.RequestUri!.AbsolutePath == "/v1/storages/object/sign/media")
            {
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
