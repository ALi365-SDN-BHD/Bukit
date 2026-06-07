using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Stages;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ContentStagesTests
{
    private static ContentDocument Document(string id, string slug, IReadOnlyDictionary<string, object> fields) =>
        ContentDocumentNormalizer.ToDocument(new RawContentDocument(
            id,
            id,
            slug,
            DateTimeOffset.UnixEpoch,
            $"<p>{id}</p>",
            ContentFieldReader.ToFieldMap(fields)));

    private static AppConfig Config(bool draft = false) => new()
    {
        Site = new SiteConfig { Name = "test", Title = "Test" },
        Content = new ContentConfig { Provider = "markdown", Markdown = new MarkdownConfig { Dir = "content" } },
        Build = new BuildConfig { Draft = draft }
    };

    private static ConfigOverrides NoOverrides => new();

    [Fact]
    public async Task ContentLoadStage_RoutesToProviderFactory()
    {
        var loadResult = new RawContentLoadResult(
            new[]
            {
                new RawContentDocument(
                    "a",
                    "a",
                    "a",
                    DateTimeOffset.UnixEpoch,
                    "<p>a</p>",
                    ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["type"] = "page" }))
            },
            EmptyContentBodyStore.Instance);
        var factory = new StubContentProviderFactory(loadResult);
        var stage = new ContentLoadStage(factory);
        var input = new ContentStageInput(Array.Empty<ContentDocument>(), EmptyContentBodyStore.Instance, Config(), NoOverrides, "/root", "/cache", new NoOpLogger());

        var output = await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Single(output.Documents);
        Assert.Equal("a", output.Documents[0].Id);
        Assert.True(output.DurationMs >= 0);
        Assert.Equal(stage.Name, output.StageName);
    }

    [Fact]
    public async Task ContentLoadStage_PassesContentModelSchemaToNormalizer()
    {
        var loadResult = new RawContentLoadResult(
            new[]
            {
                new RawContentDocument(
                    Id: "a",
                    Title: "A",
                    Slug: "a",
                    PublishAt: DateTimeOffset.UnixEpoch,
                    Body: new RawBody(InlineHtml: "<p>a</p>"),
                    Properties: new Dictionary<string, RawContentValue>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["type"] = new("text", "page"),
                        ["unknown"] = new("text", "value")
                    })
            },
            EmptyContentBodyStore.Instance);
        var factory = new StubContentProviderFactory(loadResult);
        var stage = new ContentLoadStage(factory);
        var config = Config() with
        {
            Content = Config().Content with
            {
                ModelSchema = new ContentModelSchemaConfig
                {
                    RejectUnknownRawKeys = true,
                    CustomFields = new[]
                    {
                        new CustomFieldDefinitionConfig { Name = "type" }
                    }
                }
            }
        };
        var input = new ContentStageInput(Array.Empty<ContentDocument>(), EmptyContentBodyStore.Instance, config, NoOverrides, "/root", "/cache", new NoOpLogger());

        var output = await stage.ExecuteAsync(input, CancellationToken.None);

        var document = Assert.Single(output.Documents);
        Assert.Contains(document.Diagnostics, diagnostic =>
            diagnostic.Code == "content.unknown_raw_key" &&
            diagnostic.Field == "unknown" &&
            diagnostic.SourceId == "a");
    }

    [Fact]
    public async Task ContentLoadStage_ProjectsConfiguredCanonicalMappings()
    {
        var loadResult = new RawContentLoadResult(
            new[]
            {
                new RawContentDocument(
                    Id: "a",
                    Title: "A",
                    Slug: "a",
                    PublishAt: DateTimeOffset.UnixEpoch,
                    Body: new RawBody(InlineHtml: "<p>a</p>"),
                    Properties: new Dictionary<string, RawContentValue>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["kind"] = new("text", "article"),
                        ["abstract"] = new("text", "Configured summary")
                    })
            },
            EmptyContentBodyStore.Instance);
        var factory = new StubContentProviderFactory(loadResult);
        var stage = new ContentLoadStage(factory);
        var config = Config() with
        {
            Content = Config().Content with
            {
                ModelSchema = new ContentModelSchemaConfig
                {
                    CanonicalMappings = new[]
                    {
                        new CanonicalFieldMappingConfig { CanonicalField = "type", RawKey = "kind" },
                        new CanonicalFieldMappingConfig { CanonicalField = "summary", RawKey = "abstract" }
                    }
                }
            }
        };
        var input = new ContentStageInput(Array.Empty<ContentDocument>(), EmptyContentBodyStore.Instance, config, NoOverrides, "/root", "/cache", new NoOpLogger());

        var output = await stage.ExecuteAsync(input, CancellationToken.None);

        var document = Assert.Single(output.Documents);
        Assert.Equal("article", document.Record.Identity.ContentType);
        Assert.Equal("Configured summary", document.Record.Presentation.Summary);
    }

    [Fact]
    public void ContentDocumentNormalizer_MapsRawBodySourcePoliciesAndDiagnostics()
    {
        var raw = new RawContentDocument(
            Id: "doc-1",
            Title: "Doc",
            Slug: "doc",
            PublishAt: DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            Body: new RawBody(
                InlineHtml: "<p>Doc</p>",
                BodyKey: "body-1",
                Markdown: "# Doc",
                PlainText: "Doc"),
            Properties: new Dictionary<string, RawContentValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", "post"),
                ["url"] = new("text", "/docs/doc/"),
                ["template"] = new("text", "article"),
                ["draft"] = new("bool", true),
                ["sourceMode"] = new("text", "data"),
                ["unknown"] = new("text", "value")
            },
            Source: new ContentSourceInfo(
                Provider: "markdown",
                SourceKey: "docs",
                SourcePath: "content/doc.md",
                ExternalId: "doc-1",
                ExternalUrl: new Uri("https://example.com/doc"),
                SyncedAt: DateTimeOffset.Parse("2026-06-02T00:00:00Z"),
                SyncStatus: "synced"));
        var schema = new ContentModelSchema(
            CanonicalMappings: new Dictionary<string, CanonicalFieldMapping>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("type")
            },
            CustomFields: new Dictionary<string, CustomFieldDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["url"] = new("url", "text"),
                ["template"] = new("template", "text"),
                ["draft"] = new("draft", "bool"),
                ["sourceMode"] = new("sourceMode", "text")
            },
            RejectUnknownRawKeys: true);

        var document = ContentDocumentNormalizer.ToDocument(raw, schema);

        Assert.Equal("<p>Doc</p>", document.Body.Html);
        Assert.Equal("body-1", document.Body.BodyKey);
        Assert.Equal("# Doc", document.Body.Markdown);
        Assert.Equal("Doc", document.Body.PlainText);
        Assert.Equal("markdown", document.Source.Provider);
        Assert.Equal("content/doc.md", document.Source.SourcePath);
        Assert.Equal("/docs/doc/", document.Route.Url);
        Assert.Equal("article", document.Route.Template);
        Assert.True(document.Publish.Draft);
        Assert.True(document.Publish.IsDataModule);
        Assert.Contains(document.Diagnostics, diagnostic =>
            diagnostic.Code == "content.unknown_raw_key" &&
            diagnostic.Field == "unknown" &&
            diagnostic.SourceId == "doc-1");
    }

    [Fact]
    public void ContentDocumentNormalizer_ProjectsCanonicalMappingsIntoContentRecord()
    {
        var raw = new RawContentDocument(
            Id: "doc-1",
            Title: "Doc",
            Slug: "doc",
            PublishAt: DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            Body: new RawBody(InlineHtml: "<p>Doc</p>"),
            Properties: new Dictionary<string, RawContentValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["kind"] = new("text", "article"),
                ["abstract"] = new("text", "Mapped summary"),
                ["writer"] = new("text", "Mapped Author"),
                ["publishedState"] = new("text", "reviewed"),
                ["sourceUrl"] = new("url", "https://example.com/original"),
                ["unknown"] = new("text", "value")
            });
        var schema = new ContentModelSchema(
            CanonicalMappings: new Dictionary<string, CanonicalFieldMapping>(StringComparer.OrdinalIgnoreCase)
            {
                ["kind"] = new("type", "kind", Required: true),
                ["abstract"] = new("summary", "abstract", Required: true),
                ["writer"] = new("author", "writer"),
                ["publishedState"] = new("review_status", "publishedState"),
                ["sourceUrl"] = new("original_url", "sourceUrl")
            },
            RejectUnknownRawKeys: true);

        var document = ContentDocumentNormalizer.ToDocument(raw, schema);

        Assert.Equal("article", document.Record.Identity.ContentType);
        Assert.Equal("Mapped summary", document.Record.Presentation.Summary);
        Assert.Equal("Mapped Author", document.Record.Ownership.Author);
        Assert.Equal("reviewed", document.Record.Trust.ReviewStatus);
        Assert.Equal("https://example.com/original", document.Record.Provenance.OriginalSource);
        Assert.Equal("article", ContentFieldReader.GetText(document.Fields, "type"));
        Assert.Equal("Mapped summary", ContentFieldReader.GetText(document.Fields, "summary"));
        Assert.Equal("article", ContentFieldReader.GetText(document.Fields, "kind"));
        Assert.DoesNotContain(document.Diagnostics, diagnostic =>
            diagnostic.Code == "content.required_canonical_field_missing");
        Assert.Contains(document.Diagnostics, diagnostic =>
            diagnostic.Code == "content.unknown_raw_key" &&
            diagnostic.Field == "unknown");
    }

    [Fact]
    public void ContentDocumentNormalizer_AppliesContentModelSchemaDefaults()
    {
        var raw = new RawContentDocument(
            Id: "doc-1",
            Title: "Doc",
            Slug: "doc",
            PublishAt: DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            Body: new RawBody(InlineHtml: "<p>Doc</p>"),
            Properties: new Dictionary<string, RawContentValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", "page")
            });
        var schema = new ContentModelSchema(
            CustomFields: new Dictionary<string, CustomFieldDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["audience"] = new("audience", "string", Default: "public"),
                ["priority"] = new("priority", "number", Default: 3d)
            });

        var document = ContentDocumentNormalizer.ToDocument(raw, schema);

        Assert.Equal("public", ContentFieldReader.GetText(document.Fields, "audience"));
        Assert.Equal(3d, ContentFieldReader.GetNumber(document.Fields, "priority"));
    }

    [Fact]
    public async Task ContentGraphValidateStage_ValidatesRichContentModelSchema()
    {
        var document = ContentDocumentNormalizer.ToDocument(new RawContentDocument(
            Id: "doc-1",
            Title: "Doc",
            Slug: "doc",
            PublishAt: DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            Body: new RawBody(InlineHtml: "<p>Doc</p>"),
            Properties: new Dictionary<string, RawContentValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new("text", "post"),
                ["audience"] = new("text", "private"),
                ["priority"] = new("number", 7),
                ["source_link"] = new("text", "not-a-url"),
                ["topic_refs"] = new("list", new object[]
                {
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["topicId"] = "topic-1"
                    }
                }),
                ["image"] = new("text", "https://example.com/image.png"),
                ["video"] = new("text", "https://example.com/video.mp4"),
                ["products"] = new("list", new[] { "Bukit Pro" })
            }));
        var config = Config() with
        {
            Content = Config().Content with
            {
                ModelSchema = new ContentModelSchemaConfig
                {
                    CustomFields = new[]
                    {
                        new CustomFieldDefinitionConfig
                        {
                            Name = "audience",
                            FieldType = "string",
                            Enum = new[] { "public", "internal" }
                        },
                        new CustomFieldDefinitionConfig
                        {
                            Name = "priority",
                            FieldType = "number",
                            Min = 1,
                            Max = 5
                        },
                        new CustomFieldDefinitionConfig
                        {
                            Name = "source_link",
                            FieldType = "string",
                            Format = "url"
                        },
                        new CustomFieldDefinitionConfig
                        {
                            Name = "topic_refs",
                            FieldType = "list",
                            SourcePolicy = "invalid-policy",
                            Reference = new ContentReferenceRuleConfig
                            {
                                TargetType = "topic",
                                IdField = "topicId",
                                LabelField = "title",
                                Required = true
                            }
                        }
                    },
                    EntityMappings = new[]
                    {
                        new EntityMappingConfig
                        {
                            RawKey = "companies",
                            EntityType = "company",
                            Required = true
                        }
                    },
                    RelationMappings = new[]
                    {
                        new RelationMappingConfig
                        {
                            RawKey = "related_to",
                            RelationType = "related-to",
                            TargetType = "content",
                            Required = true
                        }
                    },
                    Media = new MediaPolicyConfig
                    {
                        AllowedKinds = new[] { "image" }
                    }
                }
            },
            Build = Config().Build with { SchemaFailMode = "warn" }
        };
        var stage = new ContentGraphValidateStage();
        var input = new ContentStageInput(new[] { document }, EmptyContentBodyStore.Instance, config, NoOverrides, "/root", "/cache", new NoOpLogger());

        var output = await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.NotNull(output.SchemaErrors);
        Assert.Contains(output.SchemaErrors, e => e.Code == "content.custom_field_enum_mismatch" && e.Field == "fields.audience");
        Assert.Contains(output.SchemaErrors, e => e.Code == "content.custom_field_range_mismatch" && e.Field == "fields.priority");
        Assert.Contains(output.SchemaErrors, e => e.Code == "content.custom_field_format_mismatch" && e.Field == "fields.source_link");
        Assert.Contains(output.SchemaErrors, e => e.Code == "content.custom_field_source_policy_invalid" && e.Field == "fields.topic_refs");
        Assert.Contains(output.SchemaErrors, e => e.Code == "content.reference_field_missing" && e.Field == "fields.topic_refs");
        Assert.Contains(output.SchemaErrors, e => e.Code == "content.entity_mapping_required_missing" && e.Field == "entities.companies");
        Assert.Contains(output.SchemaErrors, e => e.Code == "content.relation_mapping_required_missing" && e.Field == "relations.related_to");
        Assert.Contains(output.SchemaErrors, e => e.Code == "content.media_kind_not_allowed" && e.Field == "media.kind");
    }

    [Fact]
    public async Task ContentGraphValidateStage_UsesCollectionSchemaProjection()
    {
        var loadResult = new RawContentLoadResult(
            new[]
            {
                new RawContentDocument(
                    Id: "post-1",
                    Title: "Post",
                    Slug: "post-1",
                    PublishAt: DateTimeOffset.UnixEpoch,
                    Body: new RawBody(InlineHtml: "<p>post</p>"),
                    Properties: new Dictionary<string, RawContentValue>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["collection"] = new("text", "posts"),
                        ["type"] = new("text", "post")
                    }),
                new RawContentDocument(
                    Id: "page-1",
                    Title: "Page",
                    Slug: "page-1",
                    PublishAt: DateTimeOffset.UnixEpoch,
                    Body: new RawBody(InlineHtml: "<p>page</p>"),
                    Properties: new Dictionary<string, RawContentValue>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["collection"] = new("text", "pages"),
                        ["type"] = new("text", "page")
                    })
            },
            EmptyContentBodyStore.Instance);
        var config = Config() with
        {
            Site = Config().Site with
            {
                Collections = new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["posts"] = new()
                    {
                        Permalink = "/posts/{slug}/",
                        Schema = new[]
                        {
                            new SchemaFieldDefinition { Name = "deck", Required = true }
                        }
                    },
                    ["pages"] = new()
                    {
                        Permalink = "/{slug}/"
                    }
                }
            },
            Build = Config().Build with { SchemaFailMode = "warn" }
        };
        var loadStage = new ContentLoadStage(new StubContentProviderFactory(loadResult));
        var validateStage = new ContentGraphValidateStage();
        var input = new ContentStageInput(Array.Empty<ContentDocument>(), EmptyContentBodyStore.Instance, config, NoOverrides, "/root", "/cache", new NoOpLogger());

        var loaded = await loadStage.ExecuteAsync(input, CancellationToken.None);
        var validated = await validateStage.ExecuteAsync(new ContentStageInput(loaded.Documents, loaded.BodyStore, config, NoOverrides, "/root", "/cache", new NoOpLogger()), CancellationToken.None);

        Assert.NotNull(validated.SchemaErrors);
        Assert.Contains(validated.SchemaErrors, error =>
            error.Code == "content.required_collection_field_missing" &&
            error.Field == "deck" &&
            error.SourcePath == "post-1");
        Assert.DoesNotContain(validated.SchemaErrors, error =>
            error.Code == "content.required_collection_field_missing" &&
            error.SourcePath == "page-1");
    }

    [Fact]
    public async Task DraftFilterStage_RemovesDraftItems()
    {
        var published = Document("published", "pub", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase));
        var draft = Document("draft", "draft", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["draft"] = true
        });
        var stage = new DraftFilterStage();
        var input = new ContentStageInput(new[] { published, draft }, EmptyContentBodyStore.Instance, Config(draft: false), NoOverrides, "/root", "/cache", new NoOpLogger());

        var output = await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Single(output.Documents);
        Assert.Equal("published", output.Documents[0].Id);
        Assert.Equal("DraftFilter", output.StageName);
    }

    [Fact]
    public async Task DraftFilterStage_DraftMode_KeepsAll()
    {
        var published = Document("published", "pub", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase));
        var draft = Document("draft", "draft", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["draft"] = true
        });
        var stage = new DraftFilterStage();
        var input = new ContentStageInput(new[] { published, draft }, EmptyContentBodyStore.Instance, Config(draft: true), NoOverrides, "/root", "/cache", new NoOpLogger());

        var output = await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.Equal(2, output.Documents.Count);
    }

    [Fact]
    public async Task ContentGraphValidateStage_WarnMode_CollectsCanonicalErrors()
    {
        var document = Document("a", "a", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post",
            ["status"] = "bad-status"
        });
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test"
            },
            Content = new ContentConfig { Provider = "markdown", Markdown = new MarkdownConfig() },
            Build = new BuildConfig { SchemaFailMode = "warn" }
        };
        var stage = new ContentGraphValidateStage();
        var input = new ContentStageInput(new[] { document }, EmptyContentBodyStore.Instance, config, NoOverrides, "/root", "/cache", new NoOpLogger());

        var output = await stage.ExecuteAsync(input, CancellationToken.None);

        Assert.NotNull(output.SchemaErrors);
        Assert.Contains(output.SchemaErrors, e => e.Code == "canonical_status_invalid");
        Assert.Equal("ContentGraphValidate", output.StageName);
    }

    [Fact]
    public async Task ContentGraphValidateStage_StrictMode_Throws()
    {
        var document = Document("a", "a", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "post",
            ["status"] = "bad-status"
        });
        var config = new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test"
            },
            Content = new ContentConfig { Provider = "markdown", Markdown = new MarkdownConfig() },
            Build = new BuildConfig { SchemaFailMode = "strict" }
        };
        var stage = new ContentGraphValidateStage();
        var input = new ContentStageInput(new[] { document }, EmptyContentBodyStore.Instance, config, NoOverrides, "/root", "/cache", new NoOpLogger());

        await Assert.ThrowsAsync<ConfigException>(() => stage.ExecuteAsync(input, CancellationToken.None));
    }

    [Fact]
    public async Task ContentPipeline_WithExplicitStages_ExecutesInOrder()
    {
        var order = new List<string>();
        var stages = new IContentStage[]
        {
            new RecordingStage("stage1", order),
            new RecordingStage("stage2", order),
            new RecordingStage("stage3", order)
        };
        var pipeline = new ContentPipeline(stages, new NoOpLogger());
        var config = Config(draft: true);
        var document = Document("a", "a", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase));
        var input = new ContentStageInput(new[] { document }, EmptyContentBodyStore.Instance, config, NoOverrides, "/root", "/cache", new NoOpLogger());

        var result = await pipeline.ExecuteAsync(input, CancellationToken.None);

        Assert.Empty(result.SchemaErrors);
        Assert.Equal(3, order.Count);
        Assert.Equal("stage1", order[0]);
        Assert.Equal("stage2", order[1]);
        Assert.Equal("stage3", order[2]);
    }

    [Fact]
    public async Task ContentPipeline_WithDiagnosticCode()
    {
        var stages = new IContentStage[]
        {
            new ThrowingStage(DiagnosticCode.ContentLoadFailed)
        };
        var pipeline = new ContentPipeline(stages, new NoOpLogger());
        var config = Config();
        var input = new ContentStageInput(Array.Empty<ContentDocument>(), EmptyContentBodyStore.Instance, config, NoOverrides, "/root", "/cache", new NoOpLogger());

        var ex = await Assert.ThrowsAsync<ConfigException>(() => pipeline.ExecuteAsync(input, CancellationToken.None));

        Assert.Equal(DiagnosticCode.ContentLoadFailed, ex.Code);
    }

    private sealed class RecordingStage : IContentStage
    {
        private readonly string _name;
        private readonly List<string> _order;

        public RecordingStage(string name, List<string> order)
        {
            _name = name;
            _order = order;
        }

        public string Name => _name;

        public Task<ContentStageOutput> ExecuteAsync(ContentStageInput input, CancellationToken cancellationToken)
        {
            _order.Add(_name);
            return Task.FromResult(new ContentStageOutput(input.Documents, input.BodyStore, Name, 0, null));
        }
    }

    private sealed class ThrowingStage : IContentStage
    {
        private readonly DiagnosticCode _code;

        public ThrowingStage(DiagnosticCode code) => _code = code;

        public string Name => "thrower";

        public Task<ContentStageOutput> ExecuteAsync(ContentStageInput input, CancellationToken cancellationToken)
        {
            throw new ConfigException("test failure", _code);
        }
    }

    private sealed class StubContentProviderFactory : IContentProviderFactory
    {
        private readonly RawContentLoadResult _result;

        public StubContentProviderFactory(RawContentLoadResult result) => _result = result;

        public IContentProvider Create(AppConfig config, string rootDir, bool isCi, ILogger logger)
        {
            return new StubContentProvider(_result);
        }

        public Task<RawContentLoadResult> LocalizeContentImagesAsync(RawContentLoadResult result, MediaConfig media, string rootDir, string cacheDir, ILogger logger, CancellationToken cancellationToken)
        {
            return Task.FromResult(result);
        }
    }

    private sealed class StubContentProvider : IContentProvider
    {
        private readonly RawContentLoadResult _result;

        public StubContentProvider(RawContentLoadResult result) => _result = result;

        public Task<RawContentLoadResult> LoadRawAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ToRawResult(_result));
        }
    }

    private static RawContentLoadResult ToRawResult(RawContentLoadResult result) => result;

    private sealed class NoOpLogger : ILogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
