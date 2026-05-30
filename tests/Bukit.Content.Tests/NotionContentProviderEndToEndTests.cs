using Bukit.Engine.Abstractions.Content;
using System.Net;
using System.Text;
using System.Text.Json;
using Bukit.Content.Notion;
using Bukit.Shared;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class NotionContentProviderEndToEndTests
{
    [Fact]
    public async Task LoadAsync_WithFakeNotionApi_LoadsItemsAndRendersBodyOnDemand()
    {
        var handler = new FakeNotionHandler();
        var options = new NotionProviderOptions
        {
            DatabaseId = "db-123",
            Token = "secret-token",
            RequestDelayMs = 0,
            FilterType = "checkbox_true",
            FilterProperty = "Published",
            SortProperty = "PublishAt",
            SortDirection = "descending",
            FieldPolicyMode = "all"
        };

        NotionApiClient CreateClient() =>
            new(options, new HttpClient(handler), (_, _) => Task.CompletedTask);

        var provider = new NotionContentProvider(options, logger: null, CreateClient);

        var result = await provider.LoadAsync();

        var item = Assert.Single(result.Items);
        Assert.Equal("page-1", item.Id);
        Assert.Equal("Hello Notion", item.Title);
        Assert.Equal("hello-notion", item.Slug);
        Assert.Equal(new DateTimeOffset(2026, 5, 15, 10, 30, 0, TimeSpan.Zero), item.PublishAt);
        Assert.Equal("post", item.Meta["type"]);
        Assert.Equal("notion", item.Meta["source"]);
        Assert.Equal("page-1", item.Meta["notionPageId"]);
        Assert.Equal("en", item.Meta["language"]);
        var tags = Assert.IsAssignableFrom<IEnumerable<object>>(item.Meta["tags"]);
        Assert.Equal(new[] { "docs", "release" }, tags.Select(x => x.ToString()).ToArray());
        Assert.Null(item.ContentHtml);
        Assert.Equal("page-1", item.BodyKey);

        Assert.NotNull(item.Fields);
        Assert.True(item.Fields.ContainsKey("language"));
        Assert.True(item.Fields.ContainsKey("tags"));
        Assert.True(item.Fields.ContainsKey("summary"));

        var body = await result.BodyStore.GetAsync(item);

        Assert.Equal("<p>Rendered body</p>", body.Html.Trim());
        Assert.Equal(3, handler.Invocations.Count);
        Assert.Contains(handler.Invocations, req =>
            req.Method == HttpMethod.Get &&
            req.RequestUri!.AbsoluteUri == "https://api.notion.com/v1/databases/db-123");
        Assert.Contains(handler.Invocations, req =>
            req.Method == HttpMethod.Post &&
            req.RequestUri!.AbsoluteUri == "https://api.notion.com/v1/databases/db-123/query");
        Assert.Contains(handler.Invocations, req =>
            req.Method == HttpMethod.Get &&
            req.RequestUri!.AbsoluteUri == "https://api.notion.com/v1/blocks/page-1/children?page_size=100");

        var queryRequest = handler.Invocations.Single(req => req.Method == HttpMethod.Post);
        var queryJson = handler.ReadBody(queryRequest);
        using var queryDoc = JsonDocument.Parse(queryJson);
        var queryRoot = queryDoc.RootElement;
        Assert.Equal(50, queryRoot.GetProperty("page_size").GetInt32());
        Assert.Equal("Published", queryRoot.GetProperty("filter").GetProperty("property").GetString());
        Assert.Equal("PublishAt", queryRoot.GetProperty("sorts")[0].GetProperty("property").GetString());
        Assert.Equal("descending", queryRoot.GetProperty("sorts")[0].GetProperty("direction").GetString());
    }

    [Fact]
    public async Task LoadAsync_WithWhitelistFieldPolicy_FiltersUnallowedFieldsBeforeMetaPromotion()
    {
        var handler = new FieldPolicyHandler();
        var options = new NotionProviderOptions
        {
            DatabaseId = "db-123",
            Token = "secret-token",
            RequestDelayMs = 0,
            FilterType = "none",
            RenderContent = false,
            FieldPolicyMode = "whitelist",
            AllowedFields = new[] { "language", "tags" }
        };

        NotionApiClient CreateClient() =>
            new(options, new HttpClient(handler), (_, _) => Task.CompletedTask);

        var provider = new NotionContentProvider(options, logger: null, CreateClient);

        var result = await provider.LoadAsync();

        var item = Assert.Single(result.Items);
        Assert.NotNull(item.Fields);
        Assert.True(item.Fields.ContainsKey("language"));
        Assert.True(item.Fields.ContainsKey("tags"));
        Assert.False(item.Fields.ContainsKey("summary"));
        Assert.False(item.Fields.ContainsKey("secret"));
        Assert.Equal("en", item.Meta["language"]);
        Assert.False(item.Meta.ContainsKey("summary"));
    }

    [Fact]
    public async Task BodyStore_WithReadwriteCache_ReusesCachedPageHtmlOnSecondProvider()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "bukit-notion-cache-" + Guid.NewGuid().ToString("N"));
        try
        {
            var handler = new CacheHandler();
            var options = new NotionProviderOptions
            {
                DatabaseId = "db-123",
                Token = "secret-token",
                RequestDelayMs = 0,
                FilterType = "none",
                CacheMode = "readwrite",
                CacheDir = cacheDir
            };

            NotionApiClient CreateClient() =>
                new(options, new HttpClient(handler), (_, _) => Task.CompletedTask);

            var firstProvider = new NotionContentProvider(options, logger: null, CreateClient);
            var firstResult = await firstProvider.LoadAsync();
            var firstItem = Assert.Single(firstResult.Items);
            var firstBody = await firstResult.BodyStore.GetAsync(firstItem);

            var secondProvider = new NotionContentProvider(options, logger: null, CreateClient);
            var secondResult = await secondProvider.LoadAsync();
            var secondItem = Assert.Single(secondResult.Items);
            var secondBody = await secondResult.BodyStore.GetAsync(secondItem);

            Assert.Equal("<p>Cached body</p>", firstBody.Html.Trim());
            Assert.Equal("<p>Cached body</p>", secondBody.Html.Trim());
            Assert.Equal(2, handler.Count(HttpMethod.Post, "https://api.notion.com/v1/databases/db-123/query"));
            Assert.Equal(1, handler.Count(HttpMethod.Get, "https://api.notion.com/v1/blocks/page-1/children?page_size=100"));
            Assert.True(File.Exists(Path.Combine(cacheDir, "pages", "page-1.json")));
        }
        finally
        {
            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_WithRelationTags_ResolvesMissingTargetAndPromotesTaxonomy()
    {
        var handler = new RelationHandler();
        var options = new NotionProviderOptions
        {
            DatabaseId = "db-123",
            Token = "secret-token",
            RequestDelayMs = 0,
            FilterType = "none",
            RenderContent = false,
            FieldPolicyMode = "all"
        };

        NotionApiClient CreateClient() =>
            new(options, new HttpClient(handler), (_, _) => Task.CompletedTask);

        var provider = new NotionContentProvider(options, logger: null, CreateClient);

        var result = await provider.LoadAsync();

        var item = Assert.Single(result.Items);
        Assert.NotNull(item.Fields);
        var linksField = Assert.Contains("tags_links", item.Fields);
        var links = Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(linksField.Value);
        var link = Assert.Single(links);
        Assert.Equal("tag-1", link["id"]);
        Assert.Equal("Resolved Tag", link["title"]);
        Assert.Equal("resolved-tag", link["slug"]);
        Assert.Equal("page", link["type"]);
        Assert.Equal("https://example.test/tags/resolved-tag", link["url"]);

        var tags = Assert.IsAssignableFrom<IEnumerable<string>>(item.Meta["tags"]);
        Assert.Equal(new[] { "Resolved Tag" }, tags.ToArray());
        Assert.Equal(1, handler.Count(HttpMethod.Get, "https://api.notion.com/v1/pages/tag-1"));
    }

    [Fact]
    public async Task LoadAsync_WhenRequiredOptionsMissing_ThrowsBeforeHttp()
    {
        var missingDatabase = new NotionContentProvider(new NotionProviderOptions
        {
            DatabaseId = " ",
            Token = "secret-token"
        });
        var missingToken = new NotionContentProvider(new NotionProviderOptions
        {
            DatabaseId = "db-123",
            Token = ""
        });

        var dbEx = await Assert.ThrowsAsync<ContentException>(() => missingDatabase.LoadAsync());
        var tokenEx = await Assert.ThrowsAsync<ContentException>(() => missingToken.LoadAsync());

        Assert.Contains("DatabaseId is required", dbEx.Message);
        Assert.Contains("Token is required", tokenEx.Message);
    }

    [Fact]
    public async Task LoadAsync_WhenQueryResponseMissingResults_ThrowsContentException()
    {
        var handler = new MissingResultsHandler();
        var options = new NotionProviderOptions
        {
            DatabaseId = "db-123",
            Token = "secret-token",
            FilterType = "none",
            RequestDelayMs = 0
        };
        NotionApiClient CreateClient() =>
            new(options, new HttpClient(handler), (_, _) => Task.CompletedTask);
        var provider = new NotionContentProvider(options, logger: null, CreateClient);

        var ex = await Assert.ThrowsAsync<ContentException>(() => provider.LoadAsync());

        Assert.Contains("missing results", ex.Message);
    }

    [Fact]
    public async Task LoadAsync_FollowsPaginationSkipsMissingIdsAndHonorsMaxItems()
    {
        var handler = new PaginationHandler();
        var options = new NotionProviderOptions
        {
            DatabaseId = "db-123",
            Token = "secret-token",
            FilterType = "none",
            RenderContent = false,
            MaxItems = 2,
            RequestDelayMs = 0
        };
        NotionApiClient CreateClient() =>
            new(options, new HttpClient(handler), (_, _) => Task.CompletedTask);
        var provider = new NotionContentProvider(options, logger: null, CreateClient);

        var result = await provider.LoadAsync();

        Assert.Equal(new[] { "page-1", "page-2" }, result.Items.Select(x => x.Id).ToArray());
        Assert.Equal(2, handler.Count(HttpMethod.Post, "https://api.notion.com/v1/databases/db-123/query"));
        Assert.Contains(handler.QueryBodies, body => body.Contains("\"start_cursor\":\"cursor-1\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BodyStore_WithReadonlyCacheMiss_ThrowsContentException()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "bukit-notion-cache-readonly-" + Guid.NewGuid().ToString("N"));
        try
        {
            var handler = new CacheHandler();
            var options = new NotionProviderOptions
            {
                DatabaseId = "db-123",
                Token = "secret-token",
                RequestDelayMs = 0,
                FilterType = "none",
                CacheMode = "readonly",
                CacheDir = cacheDir
            };
            NotionApiClient CreateClient() =>
                new(options, new HttpClient(handler), (_, _) => Task.CompletedTask);
            var provider = new NotionContentProvider(options, logger: null, CreateClient);
            var result = await provider.LoadAsync();
            var item = Assert.Single(result.Items);

            var ex = await Assert.ThrowsAsync<ContentException>(() => result.BodyStore.GetAsync(item));

            Assert.Contains("cache miss in readonly mode", ex.Message);
            Assert.Equal(0, handler.Count(HttpMethod.Get, "https://api.notion.com/v1/blocks/page-1/children?page_size=100"));
        }
        finally
        {
            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task BodyStore_WithStaleCache_ReRendersAndUpdatesCache()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "bukit-notion-cache-stale-" + Guid.NewGuid().ToString("N"));
        try
        {
            var pagesDir = Path.Combine(cacheDir, "pages");
            Directory.CreateDirectory(pagesDir);
            await File.WriteAllTextAsync(Path.Combine(pagesDir, "page-1.json"), """
            {"version":1,"lastEditedTime":"2026-05-14T00:00:00.000Z","html":"<p>stale</p>"}
            """);
            var handler = new CacheHandler();
            var options = new NotionProviderOptions
            {
                DatabaseId = "db-123",
                Token = "secret-token",
                RequestDelayMs = 0,
                FilterType = "none",
                CacheMode = "readwrite",
                CacheDir = cacheDir
            };
            NotionApiClient CreateClient() =>
                new(options, new HttpClient(handler), (_, _) => Task.CompletedTask);
            var provider = new NotionContentProvider(options, logger: null, CreateClient);
            var result = await provider.LoadAsync();
            var item = Assert.Single(result.Items);

            var body = await result.BodyStore.GetAsync(item);

            Assert.Equal("<p>Cached body</p>", body.Html.Trim());
            Assert.Equal(1, handler.Count(HttpMethod.Get, "https://api.notion.com/v1/blocks/page-1/children?page_size=100"));
            Assert.Contains("2026-05-15T12:00:00.000Z", await File.ReadAllTextAsync(Path.Combine(pagesDir, "page-1.json")));
        }
        finally
        {
            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_WhenDatabaseSchemaMissingProperties_Throws()
    {
        var handler = new SchemaHandler("{}");
        var options = new NotionProviderOptions
        {
            DatabaseId = "db-123",
            Token = "secret-token",
            FilterType = "checkbox_true",
            FilterProperty = "Published",
            RequestDelayMs = 0
        };
        NotionApiClient CreateClient() =>
            new(options, new HttpClient(handler), (_, _) => Task.CompletedTask);
        var provider = new NotionContentProvider(options, logger: null, CreateClient);

        var ex = await Assert.ThrowsAsync<ContentException>(() => provider.LoadAsync());

        Assert.Contains("database schema missing properties", ex.Message);
    }

    [Fact]
    public async Task LoadAsync_WhenDatabaseSchemaHasCaseInsensitiveConflict_Throws()
    {
        var handler = new SchemaHandler("""
        {
          "properties": {
            "Published": {},
            "published": {}
          }
        }
        """);
        var options = new NotionProviderOptions
        {
            DatabaseId = "db-123",
            Token = "secret-token",
            FilterType = "checkbox_true",
            FilterProperty = "Published",
            RequestDelayMs = 0
        };
        NotionApiClient CreateClient() =>
            new(options, new HttpClient(handler), (_, _) => Task.CompletedTask);
        var provider = new NotionContentProvider(options, logger: null, CreateClient);

        var ex = await Assert.ThrowsAsync<ContentException>(() => provider.LoadAsync());

        Assert.Contains("conflicting property names", ex.Message);
        Assert.Contains("Published", ex.Message);
        Assert.Contains("published", ex.Message);
    }

    [Fact]
    public async Task LoadAsync_WhenConfiguredDatabasePropertyMissing_ListsAvailableProperties()
    {
        var handler = new SchemaHandler("""
        {
          "properties": {
            "Published": {},
            "Status": {}
          }
        }
        """);
        var options = new NotionProviderOptions
        {
            DatabaseId = "db-123",
            Token = "secret-token",
            FilterType = "checkbox_true",
            FilterProperty = "Missing",
            RequestDelayMs = 0
        };
        NotionApiClient CreateClient() =>
            new(options, new HttpClient(handler), (_, _) => Task.CompletedTask);
        var provider = new NotionContentProvider(options, logger: null, CreateClient);

        var ex = await Assert.ThrowsAsync<ContentException>(() => provider.LoadAsync());

        Assert.Contains("property 'Missing' not found", ex.Message);
        Assert.Contains("Published", ex.Message);
        Assert.Contains("Status", ex.Message);
    }

    [Fact]
    public async Task LoadAsync_WhenPagePropertiesHaveCaseInsensitiveConflict_Throws()
    {
        var handler = new PageConflictHandler();
        var options = new NotionProviderOptions
        {
            DatabaseId = "db-123",
            Token = "secret-token",
            FilterType = "none",
            RequestDelayMs = 0
        };
        NotionApiClient CreateClient() =>
            new(options, new HttpClient(handler), (_, _) => Task.CompletedTask);
        var provider = new NotionContentProvider(options, logger: null, CreateClient);

        var ex = await Assert.ThrowsAsync<ContentException>(() => provider.LoadAsync());

        Assert.Contains("conflicting names ignoring case", ex.Message);
        Assert.Contains("Title", ex.Message);
        Assert.Contains("title", ex.Message);
        Assert.Contains("page-1", ex.Message);
    }

    [Fact]
    public async Task LoadAsync_WhenHasMoreWithoutCursor_StopsAfterFirstPage()
    {
        var handler = new HasMoreWithoutCursorHandler();
        var options = new NotionProviderOptions
        {
            DatabaseId = "db-123",
            Token = "secret-token",
            FilterType = "none",
            RenderContent = false,
            RequestDelayMs = 0
        };
        NotionApiClient CreateClient() =>
            new(options, new HttpClient(handler), (_, _) => Task.CompletedTask);
        var provider = new NotionContentProvider(options, logger: null, CreateClient);

        var result = await provider.LoadAsync();

        var item = Assert.Single(result.Items);
        Assert.Equal("page-1", item.Id);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task BodyStore_WithAutoSummaryEnabled_ExtractsSummaryFromRenderedHtml()
    {
        var handler = new AutoSummaryHandler();
        var options = new NotionProviderOptions
        {
            DatabaseId = "db-123",
            Token = "secret-token",
            RequestDelayMs = 0,
            FilterType = "none",
            AutoSummary = true,
            AutoSummaryMaxLength = 42
        };
        NotionApiClient CreateClient() =>
            new(options, new HttpClient(handler), (_, _) => Task.CompletedTask);
        var provider = new NotionContentProvider(options, logger: null, CreateClient);
        var result = await provider.LoadAsync();
        var item = Assert.Single(result.Items);

        Assert.False(item.Meta.ContainsKey("summary"));

        var body = await result.BodyStore.GetAsync(item);

        Assert.Contains("<p>Alpha", body.Html);
        var summary = Assert.IsType<string>(item.Meta["summary"]);
        Assert.StartsWith("Alpha beta & gamma text that should stop", summary);
        Assert.True(summary.Length <= 42);
    }

    [Fact]
    public async Task LoadAsync_WithComplexPageProperties_MapsFieldsCoverIconAndMeta()
    {
        var handler = new ComplexPropertiesHandler();
        var options = new NotionProviderOptions
        {
            DatabaseId = "db-123",
            Token = "secret-token",
            RequestDelayMs = 0,
            FilterType = "none",
            RenderContent = false,
            FieldPolicyMode = "all"
        };
        NotionApiClient CreateClient() =>
            new(options, new HttpClient(handler), (_, _) => Task.CompletedTask);
        var provider = new NotionContentProvider(options, logger: null, CreateClient);

        var result = await provider.LoadAsync();

        var item = Assert.Single(result.Items);
        Assert.Equal("formula-slug", item.Slug);
        Assert.Equal("page", item.Meta["type"]);
        Assert.Equal("ms-MY", item.Meta["language"]);
        Assert.Equal("article-template", item.Meta["template"]);
        Assert.Equal("/custom/output.html", item.Meta["outputPath"]);
        Assert.Equal("https://example.test/page", item.Meta["url"]);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), item.PublishAt);

        var fields = Assert.IsAssignableFrom<IReadOnlyDictionary<string, ContentField>>(item.Fields);
        Assert.Equal("https://cdn.test/cover.jpg", fields["cover"].Value);
        Assert.Equal("https://cdn.test/icon.png", fields["icon"].Value);
        Assert.Equal("ali@example.test", fields["contact_email"].Value);
        Assert.Equal("+6012345", fields["phone"].Value);
        Assert.Equal(12.5d, fields["score"].Value);
        Assert.Equal(true, fields["featured"].Value);
        Assert.Equal("person-1", fields["owner"].Value);
        Assert.Equal("Editor", fields["last_editor"].Value);
        Assert.Equal("BK-42", fields["issue_id"].Value);
        Assert.Equal("verified", fields["verification_state"].Value);
        Assert.Equal("https://files.test/one.pdf", fields["attachment"].Value);
        Assert.Equal(new[] { "https://files.test/a.png", "https://files.test/b.png" }, Assert.IsAssignableFrom<IReadOnlyList<string>>(fields["gallery"].Value));
        Assert.Equal(new[] { "Ali", "user-2" }, Assert.IsAssignableFrom<IReadOnlyList<string>>(fields["authors"].Value));
        Assert.Equal(new[] { "Rollup text", "Done" }, Assert.IsAssignableFrom<IReadOnlyList<object>>(fields["rollup_values"].Value).Select(x => x.ToString()).ToArray());
    }

    [Fact]
    public async Task LoadAsync_WithMixedInvalidProviderProperties_SkipsInvalidFieldsAndKeepsFallbackText()
    {
        var handler = new MixedInvalidPropertiesHandler();
        var options = new NotionProviderOptions
        {
            DatabaseId = "db-123",
            Token = "secret-token",
            RequestDelayMs = 0,
            FilterType = "none",
            RenderContent = false,
            FieldPolicyMode = "all"
        };
        NotionApiClient CreateClient() =>
            new(options, new HttpClient(handler), (_, _) => Task.CompletedTask);
        var provider = new NotionContentProvider(options, logger: null, CreateClient);

        var result = await provider.LoadAsync();

        var item = Assert.Single(result.Items);
        var fields = Assert.IsAssignableFrom<IReadOnlyDictionary<string, ContentField>>(item.Fields);
        Assert.Equal("invalid-field-page", item.Slug);
        Assert.Equal("not-a-date", fields["created_time_text"].Value);
        Assert.Equal("still-not-a-date", fields["last_edited_time_text"].Value);
        Assert.Equal("prefix-only", fields["unique_without_number"].Value);
        Assert.Equal("123", fields["unique_string_number"].Value);

        Assert.DoesNotContain("bad_url", fields.Keys);
        Assert.DoesNotContain("bad_email", fields.Keys);
        Assert.DoesNotContain("bad_phone", fields.Keys);
        Assert.DoesNotContain("bad_number", fields.Keys);
        Assert.DoesNotContain("bad_checkbox", fields.Keys);
        Assert.DoesNotContain("bad_date", fields.Keys);
        Assert.DoesNotContain("bad_created_by", fields.Keys);
        Assert.DoesNotContain("bad_last_editor", fields.Keys);
        Assert.DoesNotContain("bad_multi_select", fields.Keys);
        Assert.DoesNotContain("bad_select", fields.Keys);
        Assert.DoesNotContain("bad_status", fields.Keys);
        Assert.DoesNotContain("bad_formula", fields.Keys);
        Assert.DoesNotContain("bad_people", fields.Keys);
        Assert.DoesNotContain("bad_relation", fields.Keys);
        Assert.DoesNotContain("bad_rollup", fields.Keys);
        Assert.DoesNotContain("bad_files", fields.Keys);
    }

    private sealed class FakeNotionHandler : HttpMessageHandler
    {
        private readonly Dictionary<HttpRequestMessage, string> _requestBodies = new();

        public List<HttpRequestMessage> Invocations { get; } = new();

        public string ReadBody(HttpRequestMessage request) => _requestBodies[request];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Invocations.Add(request);
            if (request.Content is not null)
            {
                _requestBodies[request] = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            var url = request.RequestUri!.AbsoluteUri;
            if (request.Method == HttpMethod.Get && url == "https://api.notion.com/v1/databases/db-123")
            {
                return Json("""
                {
                  "properties": {
                    "Published": {},
                    "PublishAt": {},
                    "Title": {},
                    "Slug": {},
                    "Type": {},
                    "Language": {},
                    "Tags": {},
                    "Summary": {}
                  }
                }
                """);
            }

            if (request.Method == HttpMethod.Post && url == "https://api.notion.com/v1/databases/db-123/query")
            {
                return Json("""
                {
                  "has_more": false,
                  "results": [
                    {
                      "id": "page-1",
                      "last_edited_time": "2026-05-15T12:00:00.000Z",
                      "properties": {
                        "Title": {
                          "type": "title",
                          "title": [{ "plain_text": "Hello Notion" }]
                        },
                        "Slug": {
                          "type": "rich_text",
                          "rich_text": [{ "plain_text": "hello-notion" }]
                        },
                        "Type": {
                          "type": "select",
                          "select": { "name": "post" }
                        },
                        "PublishAt": {
                          "type": "date",
                          "date": { "start": "2026-05-15T10:30:00+00:00" }
                        },
                        "Published": {
                          "type": "checkbox",
                          "checkbox": true
                        },
                        "Language": {
                          "type": "rich_text",
                          "rich_text": [{ "plain_text": "en" }]
                        },
                        "Tags": {
                          "type": "multi_select",
                          "multi_select": [
                            { "name": "docs" },
                            { "name": "release" }
                          ]
                        },
                        "Summary": {
                          "type": "rich_text",
                          "rich_text": [{ "plain_text": "Short summary" }]
                        }
                      }
                    }
                  ]
                }
                """);
            }

            if (request.Method == HttpMethod.Get && url == "https://api.notion.com/v1/blocks/page-1/children?page_size=100")
            {
                return Json("""
                {
                  "has_more": false,
                  "results": [
                    {
                      "type": "paragraph",
                      "id": "block-1",
                      "paragraph": {
                        "rich_text": [{ "plain_text": "Rendered body" }]
                      }
                    }
                  ]
                }
                """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        }

        private static HttpResponseMessage Json(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class FieldPolicyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post &&
                request.RequestUri!.AbsoluteUri == "https://api.notion.com/v1/databases/db-123/query")
            {
                return Task.FromResult(Json("""
                {
                  "has_more": false,
                  "results": [
                    {
                      "id": "page-1",
                      "properties": {
                        "Title": {
                          "type": "title",
                          "title": [{ "plain_text": "Policy Page" }]
                        },
                        "Language": {
                          "type": "rich_text",
                          "rich_text": [{ "plain_text": "en" }]
                        },
                        "Tags": {
                          "type": "multi_select",
                          "multi_select": [{ "name": "docs" }]
                        },
                        "Summary": {
                          "type": "rich_text",
                          "rich_text": [{ "plain_text": "Filtered summary" }]
                        },
                        "Secret": {
                          "type": "rich_text",
                          "rich_text": [{ "plain_text": "do-not-export" }]
                        }
                      }
                    }
                  ]
                }
                """));
            }

            return Task.FromResult(NotFound());
        }
    }

    private sealed class CacheHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Invocations { get; } = new();

        public int Count(HttpMethod method, string url) =>
            Invocations.Count(req => req.Method == method && req.RequestUri!.AbsoluteUri == url);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Invocations.Add(request);
            if (request.Method == HttpMethod.Post &&
                request.RequestUri!.AbsoluteUri == "https://api.notion.com/v1/databases/db-123/query")
            {
                return Task.FromResult(Json("""
                {
                  "has_more": false,
                  "results": [
                    {
                      "id": "page-1",
                      "last_edited_time": "2026-05-15T12:00:00.000Z",
                      "properties": {
                        "Title": {
                          "type": "title",
                          "title": [{ "plain_text": "Cached Page" }]
                        }
                      }
                    }
                  ]
                }
                """));
            }

            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.AbsoluteUri == "https://api.notion.com/v1/blocks/page-1/children?page_size=100")
            {
                return Task.FromResult(Json("""
                {
                  "has_more": false,
                  "results": [
                    {
                      "type": "paragraph",
                      "id": "block-1",
                      "paragraph": {
                        "rich_text": [{ "plain_text": "Cached body" }]
                      }
                    }
                  ]
                }
                """));
            }

            return Task.FromResult(NotFound());
        }
    }

    private sealed class RelationHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Invocations { get; } = new();

        public int Count(HttpMethod method, string url) =>
            Invocations.Count(req => req.Method == method && req.RequestUri!.AbsoluteUri == url);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Invocations.Add(request);
            if (request.Method == HttpMethod.Post &&
                request.RequestUri!.AbsoluteUri == "https://api.notion.com/v1/databases/db-123/query")
            {
                return Task.FromResult(Json("""
                {
                  "has_more": false,
                  "results": [
                    {
                      "id": "page-1",
                      "properties": {
                        "Title": {
                          "type": "title",
                          "title": [{ "plain_text": "Relation Page" }]
                        },
                        "Tags": {
                          "type": "relation",
                          "relation": [{ "id": "tag-1" }]
                        }
                      }
                    }
                  ]
                }
                """));
            }

            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.AbsoluteUri == "https://api.notion.com/v1/pages/tag-1")
            {
                return Task.FromResult(Json("""
                {
                  "id": "tag-1",
                  "url": "https://example.test/tags/resolved-tag",
                  "properties": {
                    "Title": {
                      "type": "title",
                      "title": [{ "plain_text": "Resolved Tag" }]
                    },
                    "Slug": {
                      "type": "rich_text",
                      "rich_text": [{ "plain_text": "resolved-tag" }]
                    },
                    "Type": {
                      "type": "select",
                      "select": { "name": "page" }
                    }
                  }
                }
                """));
            }

            return Task.FromResult(NotFound());
        }
    }

    private sealed class MissingResultsHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Json("{}"));
        }
    }

    private sealed class PaginationHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Invocations { get; } = new();
        public List<string> QueryBodies { get; } = new();

        public int Count(HttpMethod method, string url) =>
            Invocations.Count(req => req.Method == method && req.RequestUri!.AbsoluteUri == url);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Invocations.Add(request);
            if (request.Content is not null)
            {
                QueryBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            if (QueryBodies.Count == 1)
            {
                return Json("""
                {
                  "has_more": true,
                  "next_cursor": "cursor-1",
                  "results": [
                    { "properties": {} },
                    {
                      "id": "page-1",
                      "properties": {
                        "Title": {
                          "type": "title",
                          "title": [{ "plain_text": "Page 1" }]
                        }
                      }
                    }
                  ]
                }
                """);
            }

            return Json("""
            {
              "has_more": true,
              "next_cursor": "cursor-2",
              "results": [
                {
                  "id": "page-2",
                  "properties": {
                    "Title": {
                      "type": "title",
                      "title": [{ "plain_text": "Page 2" }]
                    }
                  }
                },
                {
                  "id": "page-3",
                  "properties": {
                    "Title": {
                      "type": "title",
                      "title": [{ "plain_text": "Page 3" }]
                    }
                  }
                }
              ]
            }
            """);
        }
    }

    private sealed class SchemaHandler : HttpMessageHandler
    {
        private readonly string _schemaJson;

        public SchemaHandler(string schemaJson)
        {
            _schemaJson = schemaJson;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Json(_schemaJson));
        }
    }

    private sealed class PageConflictHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Json("""
            {
              "has_more": false,
              "results": [
                {
                  "id": "page-1",
                  "properties": {
                    "Title": {
                      "type": "title",
                      "title": [{ "plain_text": "Upper" }]
                    },
                    "title": {
                      "type": "title",
                      "title": [{ "plain_text": "Lower" }]
                    }
                  }
                }
              ]
            }
            """));
        }
    }

    private sealed class HasMoreWithoutCursorHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(Json("""
            {
              "has_more": true,
              "next_cursor": "",
              "results": [
                {
                  "id": "page-1",
                  "properties": {
                    "Title": {
                      "type": "title",
                      "title": [{ "plain_text": "Page 1" }]
                    }
                  }
                }
              ]
            }
            """));
        }
    }

    private sealed class AutoSummaryHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post &&
                request.RequestUri!.AbsoluteUri == "https://api.notion.com/v1/databases/db-123/query")
            {
                return Task.FromResult(Json("""
                {
                  "has_more": false,
                  "results": [
                    {
                      "id": "page-1",
                      "properties": {
                        "Title": {
                          "type": "title",
                          "title": [{ "plain_text": "Auto Summary" }]
                        }
                      }
                    }
                  ]
                }
                """));
            }

            if (request.Method == HttpMethod.Get &&
                request.RequestUri!.AbsoluteUri == "https://api.notion.com/v1/blocks/page-1/children?page_size=100")
            {
                return Task.FromResult(Json("""
                {
                  "has_more": false,
                  "results": [
                    {
                      "type": "paragraph",
                      "id": "block-1",
                      "paragraph": {
                        "rich_text": [
                          { "plain_text": "Alpha beta & gamma text that should stop at a word boundary before the configured maximum length." }
                        ]
                      }
                    }
                  ]
                }
                """));
            }

            return Task.FromResult(NotFound());
        }
    }

    private sealed class ComplexPropertiesHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post &&
                request.RequestUri!.AbsoluteUri == "https://api.notion.com/v1/databases/db-123/query")
            {
                return Task.FromResult(Json("""
                {
                  "has_more": false,
                  "results": [
                    {
                      "id": "page-1",
                      "cover": {
                        "type": "external",
                        "external": { "url": "https://cdn.test/cover.jpg" }
                      },
                      "icon": {
                        "type": "file",
                        "file": { "url": "https://cdn.test/icon.png" }
                      },
                      "properties": {
                        "Title": {
                          "type": "title",
                          "title": [{ "plain_text": "Complex Page" }]
                        },
                        "Slug": {
                          "type": "formula",
                          "formula": { "type": "string", "string": "formula-slug" }
                        },
                        "Type": {
                          "type": "multi_select",
                          "multi_select": [{ "name": "page" }]
                        },
                        "Date": {
                          "type": "date",
                          "date": { "start": "2026-01-02T03:04:05+00:00" }
                        },
                        "Language": {
                          "type": "rich_text",
                          "rich_text": [{ "plain_text": "ms-MY" }]
                        },
                        "Template": {
                          "type": "rich_text",
                          "rich_text": [{ "plain_text": "article-template" }]
                        },
                        "OutputPath": {
                          "type": "rich_text",
                          "rich_text": [{ "plain_text": "/custom/output.html" }]
                        },
                        "Url": {
                          "type": "url",
                          "url": "https://example.test/page"
                        },
                        "Contact Email": {
                          "type": "email",
                          "email": "ali@example.test"
                        },
                        "Phone": {
                          "type": "phone_number",
                          "phone_number": "+6012345"
                        },
                        "Score": {
                          "type": "number",
                          "number": 12.5
                        },
                        "Featured": {
                          "type": "checkbox",
                          "checkbox": true
                        },
                        "Owner": {
                          "type": "created_by",
                          "created_by": { "id": "person-1" }
                        },
                        "Last Editor": {
                          "type": "last_edited_by",
                          "last_edited_by": { "name": "Editor" }
                        },
                        "Issue Id": {
                          "type": "unique_id",
                          "unique_id": { "prefix": "BK", "number": 42 }
                        },
                        "Verification State": {
                          "type": "verification",
                          "verification": { "state": "verified" }
                        },
                        "Attachment": {
                          "type": "files",
                          "files": [
                            { "type": "external", "external": { "url": "https://files.test/one.pdf" } }
                          ]
                        },
                        "Gallery": {
                          "type": "files",
                          "files": [
                            { "type": "external", "external": { "url": "https://files.test/a.png" } },
                            { "type": "file", "file": { "url": "https://files.test/b.png" } }
                          ]
                        },
                        "Authors": {
                          "type": "people",
                          "people": [
                            { "name": "Ali" },
                            { "id": "user-2" }
                          ]
                        },
                        "Rollup Values": {
                          "type": "rollup",
                          "rollup": {
                            "type": "array",
                            "array": [
                              {
                                "type": "rich_text",
                                "rich_text": [{ "plain_text": "Rollup text" }]
                              },
                              {
                                "type": "status",
                                "status": { "name": "Done" }
                              }
                            ]
                          }
                        }
                      }
                    }
                  ]
                }
                """));
            }

            return Task.FromResult(NotFound());
        }
    }

    private sealed class MixedInvalidPropertiesHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post &&
                request.RequestUri!.AbsoluteUri == "https://api.notion.com/v1/databases/db-123/query")
            {
                return Task.FromResult(Json("""
                {
                  "has_more": false,
                  "results": [
                    {
                      "id": "page-1",
                      "properties": {
                        "Title": {
                          "type": "title",
                          "title": [{ "plain_text": "Invalid Field Page" }]
                        },
                        "Slug": {
                          "type": "rich_text",
                          "rich_text": [{ "plain_text": "invalid-field-page" }]
                        },
                        "Bad Url": {
                          "type": "url",
                          "url": 123
                        },
                        "Bad Email": {
                          "type": "email",
                          "email": 123
                        },
                        "Bad Phone": {
                          "type": "phone_number",
                          "phone_number": 123
                        },
                        "Bad Number": {
                          "type": "number",
                          "number": null
                        },
                        "Bad Checkbox": {
                          "type": "checkbox",
                          "checkbox": "true"
                        },
                        "Bad Date": {
                          "type": "date",
                          "date": { "start": "not-a-date" }
                        },
                        "Created Time Text": {
                          "type": "created_time",
                          "created_time": "not-a-date"
                        },
                        "Last Edited Time Text": {
                          "type": "last_edited_time",
                          "last_edited_time": "still-not-a-date"
                        },
                        "Bad Created By": {
                          "type": "created_by",
                          "created_by": {}
                        },
                        "Bad Last Editor": {
                          "type": "last_edited_by",
                          "last_edited_by": {}
                        },
                        "Bad Multi Select": {
                          "type": "multi_select",
                          "multi_select": [{ "name": "   " }]
                        },
                        "Bad Select": {
                          "type": "select",
                          "select": { "name": "   " }
                        },
                        "Bad Status": {
                          "type": "status",
                          "status": { "name": "   " }
                        },
                        "Bad Formula": {
                          "type": "formula",
                          "formula": { "type": "unknown" }
                        },
                        "Bad People": {
                          "type": "people",
                          "people": [123, {}]
                        },
                        "Bad Relation": {
                          "type": "relation",
                          "relation": [{ "id": "   " }]
                        },
                        "Bad Rollup": {
                          "type": "rollup",
                          "rollup": { "type": "date", "date": { "start": "not-a-date" } }
                        },
                        "Bad Files": {
                          "type": "files",
                          "files": [
                            123,
                            { "type": "external", "external": {} },
                            { "type": "file", "file": { "url": "   " } }
                          ]
                        },
                        "Unique Without Number": {
                          "type": "unique_id",
                          "unique_id": { "prefix": "prefix-only" }
                        },
                        "Unique String Number": {
                          "type": "unique_id",
                          "unique_id": { "number": "123" }
                        }
                      }
                    }
                  ]
                }
                """));
            }

            return Task.FromResult(NotFound());
        }
    }

    private static HttpResponseMessage Json(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage NotFound()
    {
        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
    }
}
