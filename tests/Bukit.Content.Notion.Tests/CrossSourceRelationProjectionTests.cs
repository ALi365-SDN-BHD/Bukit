using Bukit.Engine.Abstractions.Content;
using Bukit.Content;
using System.Net;
using System.Text;
using Xunit;

namespace Bukit.Content.Notion.Tests;

public sealed class CrossSourceRelationProjectionTests
{
    [Fact]
    public async Task CompositeProvider_ProjectsRelationsAfterAllSourcesHaveLoaded()
    {
        var schema = Schema(Mapping("authoredBy", "authoredBy", "person"));
        var article = Document("article-1", "Article", "article", "article", ("authoredby", Relation("author-1")));
        var author = Target("author-1", "Aisha Tan", "aisha-tan", "Person", "/authors/aisha-tan/", "/images/aisha.jpg", ["https://example.social/aisha"]);
        var composite = new CompositeContentProvider(
        [
            ("articles", "content", (IContentProvider)new StaticProvider(article)),
            ("authors", "content", (IContentProvider)new StaticProvider(author))
        ],
        schema);

        var result = await composite.LoadRawAsync();

        AssertProjection(result.Documents.Single(document => document.SourceId == "articles:article-1"), "authoredby", "author-1", "Aisha Tan", "aisha-tan", "Person", "/authors/aisha-tan/", "/images/aisha.jpg", ["https://example.social/aisha"]);
    }

    [Fact]
    public async Task CompositeProvider_UsesSourceBoundedFallbackAndPreservesPermissionDiagnostic()
    {
        var schema = Schema(Mapping("authoredBy", "authoredBy", "person"));
        var resolver = new RecordingResolver(
            targets: [new RelationTargetInfo("remote-author", "Remote Author", "remote-author", "Person", "/authors/remote-author/")],
            failures: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["private-author"] = "notion.relation.permission_denied"
            });
        var article = Document("article-1", "Article", "article", "article", ("authoredby", Relation("remote-author", "private-author")));
        var composite = new CompositeContentProvider(
        [
            ("articles", "content", (IContentProvider)new FallbackStaticProvider(resolver, article))
        ],
        schema);

        var result = await composite.LoadRawAsync();

        Assert.Equal(1, resolver.CallCount);
        Assert.Equal(["remote-author", "private-author"], resolver.LastRequestedIds);
        var document = Assert.Single(result.Documents);
        var links = Projection(document, "authoredby");
        Assert.Equal("Remote Author", links[0]["title"]);
        Assert.Null(links[1]["title"]);
        var diagnostic = Assert.Single(document.Diagnostics);
        Assert.Equal("notion.relation.permission_denied", diagnostic.Code);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "notion.relation.permission_denied")]
    [InlineData(HttpStatusCode.NotFound, "notion.relation.permission_denied")]
    [InlineData(HttpStatusCode.Unauthorized, "notion.relation.unresolved")]
    [InlineData(HttpStatusCode.InternalServerError, "notion.relation.unresolved")]
    public async Task CompositeProvider_UsesRealSourceFallbackForOrderedRemoteRelations(
        HttpStatusCode missingStatus,
        string expectedDiagnostic)
    {
        var options = new NotionContentSourceOptions
        {
            DatabaseId = "articles-db", Token = "dummy-articles", FilterType = "none",
            FieldPolicyMode = "all", RenderContent = false, MaxRetries = 0
        };
        var handler = new RelationSourceHandler(missingStatus);
        NotionContentClient CreateClient() => new(options, handler, static (_, _) => Task.CompletedTask);
        var source = new NotionContentSource(options, logger: null, CreateClient);
        var composite = new CompositeContentProvider(
            [("articles", "content", (IContentProvider)source)],
            Schema(Mapping("authoredBy", "authoredBy", "person")));

        var result = await composite.LoadRawAsync();
        try
        {
            var document = Assert.Single(result.Documents);
            Assert.Equal("articles:article-1", document.SourceId);
            var links = Projection(document, "authoredby");
            Assert.Equal(["remote-author", "missing-author", "remote-author"], links.Select(LinkId));
            foreach (var link in new[] { links[0], links[2] })
            {
                Assert.Equal("Remote Author", link["title"]);
                Assert.Equal("remote-author", link["slug"]);
                Assert.Equal("person", link["type"]);
                Assert.Equal("/authors/remote-author/", link["url"]);
                Assert.Equal("remote-author", link["profileId"]);
                Assert.Equal("Remote Author", link["displayName"]);
                Assert.Equal("/authors/remote-author/", link["profileUrl"]);
            }
            Assert.Null(links[1]["title"]);
            Assert.Null(links[1]["slug"]);
            Assert.Null(links[1]["url"]);
            var diagnostic = Assert.Single(document.Diagnostics);
            Assert.Equal(expectedDiagnostic, diagnostic.Code);
            Assert.Equal("authoredBy", diagnostic.Field);
            Assert.Contains("missing-author", diagnostic.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("private-response-body-sentinel", diagnostic.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(options.Token, diagnostic.Message, StringComparison.Ordinal);
            Assert.Equal(
                ["POST /v1/databases/articles-db/query", "GET /v1/pages/remote-author", "GET /v1/pages/missing-author"],
                handler.Requests);
        }
        finally
        {
            await ((IAsyncDisposable)result.BodyStore).DisposeAsync();
        }
    }

    [Fact]
    public async Task SourceFallback_PropagatesCallerCancellationDuringRequest()
    {
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var options = new NotionContentSourceOptions
        {
            DatabaseId = "articles-db", Token = "dummy-articles", MaxRetries = 0
        };
        var handler = new BlockingRelationHandler(requestStarted);
        NotionContentClient CreateClient() => new(options, handler, static (_, _) => Task.CompletedTask);
        var source = new NotionContentSource(options, logger: null, CreateClient);
        using var cancellation = new CancellationTokenSource();

        var resolveTask = source.CreateRelationFallbackResolver().ResolveAsync(["missing-author"], cancellation.Token);
        await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resolveTask);
        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task ProjectAsync_UsesFirstLoadedTargetWhenSourcesContainTheSamePageId()
    {
        var schema = Schema(Mapping("authoredBy", "authoredBy", "person"));
        var article = Document("article-1", "Article", "article", "article", ("authoredby", Relation("author-1")));
        var first = Target("author-1", "First Author", "first-author", "Person", "/authors/first/", "/images/first.jpg", ["https://social.example/first"]);
        var second = Target("author-1", "Second Author", "second-author", "Person", "/authors/second/", "/images/second.jpg", ["https://social.example/second"]);

        var projected = await NotionCrossSourceRelationProjector.ProjectAsync(
            [Batch("articles", article), Batch("authors-primary", first), Batch("authors-secondary", second)],
            schema,
            CancellationToken.None);

        AssertProjection(projected[0].Documents[0], "authoredby", "author-1", "First Author", "first-author", "Person", "/authors/first/", "/images/first.jpg", ["https://social.example/first"]);
    }

    [Fact]
    public async Task ProjectAsync_IndexesAllLoadedSourcesAndProjectsConfiguredRelationTargets()
    {
        var schema = Schema(
            Mapping("authoredBy", "authoredBy", "person"),
            Mapping("sources", "cites", "source"),
            Mapping("companies", "mentions", "company"));
        var article = Document(
            "article-1",
            "Article",
            "article",
            "article",
            ("authoredby", Relation("author-1")),
            ("sources", Relation("source-1")),
            ("companies", Relation("company-1")),
            ("tags", Relation("tag-1")),
            ("tags_links", Links(("tag-1", "Policy"))));
        var sources = new[]
        {
            Batch("articles", article),
            Batch("authors", Target("author-1", "Aisha Tan", "aisha-tan", "Person", "/authors/aisha-tan/", "/images/aisha.jpg", ["https://example.social/aisha"])),
            Batch("sources", Target("source-1", "Official Gazette", "official-gazette", "CreativeWork", "https://gazette.example/source", "/images/gazette.png", ["https://wikidata.example/source"])),
            Batch("companies", Target("company-1", "Example Sdn Bhd", "example-sdn-bhd", "Organization", "/companies/example/", "/images/company.png", ["https://example.com/company"], status: "draft"))
        };

        var projected = await NotionCrossSourceRelationProjector.ProjectAsync(
            sources,
            schema,
            CancellationToken.None);

        var projectedArticle = Assert.Single(projected[0].Documents);
        AssertProjection(projectedArticle, "authoredby", "author-1", "Aisha Tan", "aisha-tan", "Person", "/authors/aisha-tan/", "/images/aisha.jpg", ["https://example.social/aisha"]);
        AssertProjection(projectedArticle, "sources", "source-1", "Official Gazette", "official-gazette", "CreativeWork", "https://gazette.example/source", "/images/gazette.png", ["https://wikidata.example/source"]);
        AssertProjection(projectedArticle, "companies", "company-1", "Example Sdn Bhd", "example-sdn-bhd", "Organization", "/companies/example/", "/images/company.png", ["https://example.com/company"]);

        var tags = Assert.IsAssignableFrom<IEnumerable<string>>(projectedArticle.CustomFields!["tags"].Value);
        Assert.Equal(["tag-1"], tags);
        Assert.True(projectedArticle.CustomFields.ContainsKey("tags_links"));
        Assert.Empty(projectedArticle.Diagnostics);
    }

    [Fact]
    public async Task ProjectAsync_DeduplicatesTargetsPreservesOrderAndStopsAtLoadedCycles()
    {
        var resolver = new RecordingResolver();
        var schema = Schema(Mapping("related", "related", "content", withReference: false));
        var a = Document("a", "A", "a", "article", ("related", Relation("b", "b")));
        var b = Document("b", "B", "b", "article", ("related", Relation("a")));

        var projected = await NotionCrossSourceRelationProjector.ProjectAsync(
            [new NotionRelationProjectionSource("cycle", [a, b], resolver)],
            schema,
            CancellationToken.None);

        var aLinks = Projection(projected[0].Documents[0], "related");
        Assert.Equal(["b", "b"], aLinks.Select(LinkId));
        Assert.All(aLinks, link => Assert.Equal("B", link["title"]));
        var bLink = Assert.Single(Projection(projected[0].Documents[1], "related"));
        Assert.Equal("A", bLink["title"]);
        Assert.Equal(0, resolver.CallCount);
    }

    [Fact]
    public async Task ProjectAsync_WithoutReferenceDoesNotFetchAndKeepsUnresolvedIdWithDiagnostic()
    {
        var resolver = new RecordingResolver(
            targets: [new RelationTargetInfo("missing-author", "Invented", "invented", "Person", "/authors/invented/")]);
        var schema = Schema(Mapping("authoredBy", "authoredBy", "person", withReference: false));
        var article = Document("article-1", "Article", "article", "article", ("authoredby", Relation("missing-author")));

        var projected = await NotionCrossSourceRelationProjector.ProjectAsync(
            [new NotionRelationProjectionSource("articles", [article], resolver)],
            schema,
            CancellationToken.None);

        Assert.Equal(0, resolver.CallCount);
        var link = Assert.Single(Projection(projected[0].Documents[0], "authoredby"));
        Assert.Equal("missing-author", link["id"]);
        Assert.Null(link["title"]);
        Assert.Null(link["slug"]);
        Assert.Null(link["url"]);
        Assert.Null(link["image"]);
        Assert.Null(link["sameAs"]);
        var diagnostic = Assert.Single(projected[0].Documents[0].Diagnostics);
        Assert.Equal("notion.relation.unresolved", diagnostic.Code);
        Assert.Equal("authoredBy", diagnostic.Field);
        Assert.Equal("article-1", diagnostic.SourceId);
    }

    [Fact]
    public async Task ProjectAsync_WithReferencePerformsBoundedFetchAndDiagnosesPermissionFailure()
    {
        var resolver = new RecordingResolver(
            targets:
            [
                new RelationTargetInfo(
                    "remote-author",
                    "Remote Author",
                    "remote-author",
                    "Person",
                    "/authors/remote-author/",
                    "/images/remote.jpg",
                    ["https://social.example/remote"])
            ],
            failures: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["private-author"] = "notion.relation.permission_denied"
            });
        var schema = Schema(Mapping("authoredBy", "authoredBy", "person"));
        var article = Document(
            "article-1",
            "Article",
            "article",
            "article",
            ("authoredby", Relation("remote-author", "private-author", "remote-author")));

        var projected = await NotionCrossSourceRelationProjector.ProjectAsync(
            [new NotionRelationProjectionSource("articles", [article], resolver)],
            schema,
            CancellationToken.None);

        Assert.Equal(1, resolver.CallCount);
        Assert.Equal(["remote-author", "private-author"], resolver.LastRequestedIds);
        var links = Projection(projected[0].Documents[0], "authoredby");
        Assert.Equal(["remote-author", "private-author", "remote-author"], links.Select(LinkId));
        Assert.Equal("Remote Author", links[0]["title"]);
        Assert.Null(links[1]["title"]);
        Assert.Equal("Remote Author", links[2]["title"]);
        var diagnostic = Assert.Single(projected[0].Documents[0].Diagnostics);
        Assert.Equal("notion.relation.permission_denied", diagnostic.Code);
        Assert.Contains("private-author", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RelationTargetCache_VersionTwoScopesAndRoundTripsImageAndSameAs()
    {
        var root = Path.Combine(Path.GetTempPath(), $"bukit-relation-v2-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "relations"));
        try
        {
            var authors = Assert.IsType<NotionRelationTargetCache>(
                NotionRelationTargetCache.Create("readwrite", root, "authors-db"));
            var companies = Assert.IsType<NotionRelationTargetCache>(
                NotionRelationTargetCache.Create("readwrite", root, "companies-db"));
            var target = new RelationTargetInfo(
                "shared-id",
                "Aisha",
                "aisha",
                "Person",
                "/authors/aisha/",
                "/images/aisha.jpg",
                ["https://social.example/aisha"]);

            await authors.WriteAsync(target, CancellationToken.None);
            await companies.WriteAsync(target with { Title = "Different Scope" }, CancellationToken.None);

            var cached = Assert.IsType<RelationTargetInfo>(
                await authors.TryReadAsync("shared-id", CancellationToken.None));
            Assert.Equal(target.PageId, cached.PageId);
            Assert.Equal(target.Title, cached.Title);
            Assert.Equal(target.Slug, cached.Slug);
            Assert.Equal(target.Type, cached.Type);
            Assert.Equal(target.Url, cached.Url);
            Assert.Equal(target.Image, cached.Image);
            Assert.Equal(target.SameAs!.ToArray(), cached.SameAs!.ToArray());
            Assert.Equal("Different Scope", (await companies.TryReadAsync("shared-id", CancellationToken.None))!.Title);

            var cacheFiles = Directory.GetFiles(Path.Combine(root, "relations"), "*.json", SearchOption.AllDirectories);
            Assert.Equal(2, cacheFiles.Length);
            Assert.All(cacheFiles, path => Assert.Contains("\"version\":2", File.ReadAllText(path), StringComparison.Ordinal));

            var oldScope = Path.Combine(root, "relations", "legacy-db");
            Directory.CreateDirectory(oldScope);
            await File.WriteAllTextAsync(
                Path.Combine(oldScope, "legacy.json"),
                """{"version":1,"pageId":"legacy","title":"Legacy","slug":"legacy","type":"Person","url":null}""");
            var legacy = Assert.IsType<NotionRelationTargetCache>(
                NotionRelationTargetCache.Create("readonly", root, "legacy-db"));
            Assert.Null(await legacy.TryReadAsync("legacy", CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static RelationMapping Mapping(
        string rawKey,
        string relationType,
        string targetType,
        bool withReference = true)
        => new(
            RawKey: rawKey,
            RelationType: relationType,
            TargetType: targetType,
            Reference: withReference
                ? new ContentReferenceRule(
                    TargetType: targetType,
                    IdField: "profileId",
                    LabelField: "displayName",
                    UrlField: "profileUrl",
                    Required: true)
                : null);

    private static ContentModelSchema Schema(params RelationMapping[] mappings)
        => new(RelationMappings: mappings.ToDictionary(x => x.RawKey, StringComparer.OrdinalIgnoreCase));

    private static NotionRelationProjectionSource Batch(string sourceKey, params RawContentDocument[] documents)
        => new(sourceKey, documents);

    private static RawContentDocument Target(
        string id,
        string title,
        string slug,
        string type,
        string url,
        string image,
        IReadOnlyList<string> sameAs,
        string status = "published")
        => Document(
            id,
            title,
            slug,
            type,
            ("url", new ContentField("url", url)),
            ("image", new ContentField("url", image)),
            ("sameAs", new ContentField("list", sameAs)),
            ("status", new ContentField("text", status)));

    private static RawContentDocument Document(
        string id,
        string title,
        string slug,
        string type,
        params (string Key, ContentField Field)[] fields)
    {
        var map = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = new("text", type),
            ["notionPageId"] = new("text", id)
        };
        foreach (var (key, field) in fields)
        {
            map[key] = field;
        }

        return new RawContentDocument(
            Id: id,
            Title: title,
            Slug: slug,
            PublishAt: DateTimeOffset.Parse("2026-07-25T00:00:00Z"),
            Body: new RawBody(),
            Properties: RawContentValue.FromFields(map),
            Source: new ContentSourceInfo("notion", ExternalId: id),
            CustomFields: map);
    }

    private static ContentField Relation(params string[] ids) => new("relation", ids);

    private static ContentField Links(params (string Id, string Title)[] links)
        => new(
            "list",
            links.Select(x => new Dictionary<string, object?>
            {
                ["id"] = x.Id,
                ["title"] = x.Title
            }).ToList());

    private static List<Dictionary<string, object?>> Projection(RawContentDocument document, string key)
        => Assert.IsType<List<Dictionary<string, object?>>>(document.CustomFields![key].Value);

    private static string LinkId(Dictionary<string, object?> link) => Assert.IsType<string>(link["id"]);

    private static void AssertProjection(
        RawContentDocument document,
        string key,
        string id,
        string title,
        string slug,
        string type,
        string url,
        string image,
        IReadOnlyList<string> sameAs)
    {
        var link = Assert.Single(Projection(document, key));
        Assert.Equal(id, link["id"]);
        Assert.Equal(title, link["title"]);
        Assert.Equal(slug, link["slug"]);
        Assert.Equal(type, link["type"]);
        Assert.Equal(url, link["url"]);
        Assert.Equal(image, link["image"]);
        Assert.Equal(sameAs, Assert.IsAssignableFrom<IReadOnlyList<string>>(link["sameAs"]));
        Assert.Equal(id, link["profileId"]);
        Assert.Equal(title, link["displayName"]);
        Assert.Equal(url, link["profileUrl"]);
    }

    private sealed class RelationSourceHandler(HttpStatusCode missingStatus) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal("api.notion.com", request.RequestUri!.Host);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("dummy-articles", request.Headers.Authorization?.Parameter);
            var key = $"{request.Method} {request.RequestUri.AbsolutePath}";
            Requests.Add(key);
            var (status, json) = key switch
            {
                "POST /v1/databases/articles-db/query" => (HttpStatusCode.OK, """
                    {"results":[{"id":"article-1","created_time":"2026-07-25T00:00:00Z","properties":{
                      "Title":{"type":"title","title":[{"plain_text":"Article"}]},
                      "authoredBy":{"type":"relation","relation":[{"id":"remote-author"},{"id":"missing-author"},{"id":"remote-author"}]}
                    }}],"has_more":false}
                    """),
                "GET /v1/pages/remote-author" => (HttpStatusCode.OK, """
                    {"id":"remote-author","properties":{
                      "Title":{"type":"title","title":[{"plain_text":"Remote Author"}]},
                      "Slug":{"type":"rich_text","rich_text":[{"plain_text":"remote-author"}]},
                      "Type":{"type":"select","select":{"name":"person"}},
                      "Url":{"type":"url","url":"/authors/remote-author/"}
                    }}
                    """),
                "GET /v1/pages/missing-author" => (missingStatus, $$"""
                    {"object":"error","status":{{(int)missingStatus}},"code":"fixture_error","message":"private-response-body-sentinel dummy-articles"}
                    """),
                _ => throw new InvalidOperationException($"Unexpected fixture request: {key}")
            };
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class BlockingRelationHandler(TaskCompletionSource requestStarted) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://api.notion.com/v1/pages/missing-author", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("dummy-articles", request.Headers.Authorization?.Parameter);
            requestStarted.TrySetResult();
            return await new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously)
                .Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class RecordingResolver(
        IReadOnlyList<RelationTargetInfo>? targets = null,
        IReadOnlyDictionary<string, string>? failures = null) : INotionRelationFallbackResolver
    {
        public int CallCount { get; private set; }
        public IReadOnlyList<string> LastRequestedIds { get; private set; } = [];

        public Task<NotionRelationFallbackResult> ResolveAsync(
            IReadOnlyList<string> pageIds,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastRequestedIds = pageIds.ToArray();
            return Task.FromResult(new NotionRelationFallbackResult(
                targets ?? Array.Empty<RelationTargetInfo>(),
                failures ?? new Dictionary<string, string>()));
        }
    }

    private sealed class StaticProvider(params RawContentDocument[] documents) : IContentProvider
    {
        public Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new RawContentLoadResult(documents, EmptyContentBodyStore.Instance));
    }

    private sealed class FallbackStaticProvider(
        INotionRelationFallbackResolver resolver,
        params RawContentDocument[] documents) : IContentProvider, INotionRelationFallbackResolverProvider
    {
        public INotionRelationFallbackResolver RelationFallbackResolver => resolver;

        public Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new RawContentLoadResult(documents, EmptyContentBodyStore.Instance));
    }
}
