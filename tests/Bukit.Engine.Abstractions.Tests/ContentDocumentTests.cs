using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;
using Xunit;

namespace Bukit.Engine.Abstractions.Tests;

[Trait("Category", "Unit")]
public sealed class ContentDocumentTests
{
    private static ContentDocument CreateDoc(
        string? status = null,
        bool draft = false,
        IReadOnlyDictionary<string, ContentField>? extraFields = null)
    {
        var fields = new Dictionary<string, ContentField>();
        if (extraFields != null)
        {
            foreach (var kv in extraFields) fields[kv.Key] = kv.Value;
        }
        if (draft)
        {
            fields["draft"] = new ContentField("bool", true);
        }
        if (status != null)
        {
            fields["status"] = new ContentField("text", status);
        }
        return ContentDocument.Create("post-1", "Test Post", "test-post",
            new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero),
            "<p>Hello</p>", fields, "markdown");
    }

    [Fact]
    public void Create_DefaultsToPublishedAndPageType()
    {
        var doc = CreateDoc();
        Assert.Equal("published", doc.Record.Identity.Status);
        // collection field is not set in test fields, so type defaults to "page"
        Assert.Equal("page", doc.Record.Identity.ContentType);
        Assert.Equal("test-post", doc.Record.Identity.Slug);
        Assert.Equal("Test Post", doc.Record.Presentation.Title);
    }

    [Fact]
    public void Create_DraftField_SetsStatusToDraft()
    {
        var doc = CreateDoc(draft: true);
        Assert.Equal("draft", doc.Record.Identity.Status);
    }

    [Fact]
    public void Create_DraftTrue_TakesPrecedenceOverExplicitStatus()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["draft"] = new ContentField("bool", true),
            ["status"] = new ContentField("text", "published")
        };
        var doc = CreateDoc(extraFields: fields);
        Assert.Equal("draft", doc.Record.Identity.Status);
    }

    [Fact]
    public void Create_ExplicitStatus_UsedWhenDraftIsFalse()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["draft"] = new ContentField("bool", false),
            ["status"] = new ContentField("text", "review")
        };
        var doc = CreateDoc(extraFields: fields);
        Assert.Equal("review", doc.Record.Identity.Status);
    }

    [Fact]
    public void Create_DataMode_DefaultsToModuleType()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["sourceMode"] = new ContentField("text", "data")
        };
        var doc = ContentDocument.Create("mod-1", "Module", "module",
            new DateTimeOffset(2026, 6, 9, 0, 0, 0, TimeSpan.Zero),
            null, fields);
        Assert.Equal("module", doc.Record.Identity.ContentType);
        Assert.Equal(string.Empty, doc.Record.Classification.Collection);
    }

    [Fact]
    public void Create_CollectionOnly_DefaultsTypeToPageWithoutChangingCollection()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["collection"] = new ContentField("text", "news")
        };

        var doc = CreateDoc(extraFields: fields);

        Assert.Equal("page", doc.Record.Identity.ContentType);
        Assert.Equal("news", doc.Record.Classification.Collection);
    }

    [Fact]
    public void Create_TypeField_OverridesCollectionDefault()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["type"] = new ContentField("text", "landing")
        };
        var doc = CreateDoc(extraFields: fields);
        Assert.Equal("landing", doc.Record.Identity.ContentType);
    }

    [Fact]
    public void Create_ProjectsCanonicalAuthorTypeWithoutChangingConstructorShape()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["author"] = new("text", "Silk Road Editorial Desk"),
            ["authorType"] = new("text", "Organization")
        };

        var doc = CreateDoc(extraFields: fields);

        Assert.Equal("Silk Road Editorial Desk", doc.Record.Ownership.Author);
        Assert.Equal("Organization", doc.Record.Ownership.AuthorType);
        var ownership = new ContentOwnership("Alice", "Editorial", "Owner", "Reviewer");
        Assert.Null(ownership.AuthorType);
        var (author, organization, owner, reviewer) = ownership;
        Assert.Equal("Alice", author);
        Assert.Equal("Editorial", organization);
        Assert.Equal("Owner", owner);
        Assert.Equal("Reviewer", reviewer);
    }

    [Fact]
    public void Create_ProjectsAuthoredByProfileMaps()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["authoredBy"] = new ContentField("list", new List<object>
            {
                new Dictionary<string, object?>
                {
                    ["id"] = "alice-1",
                    ["title"] = "Alice",
                    ["slug"] = "alice",
                    ["type"] = "person",
                    ["image"] = "/alice.png",
                    ["sameAs"] = new List<object> { "https://example.com/alice" }
                }
            })
        };

        var doc = CreateDoc(extraFields: fields);

        Assert.True(doc.Record.Ownership.UsesAuthorRelation);
        var profile = Assert.Single(doc.Record.Ownership.AuthorProfiles);
        Assert.Equal("alice-1", profile.Id);
        Assert.Equal("Alice", profile.Title);
        Assert.Equal("alice", profile.Slug);
        Assert.Equal("person", profile.Type);
        Assert.Equal("/alice.png", profile.Image);
        Assert.Equal(new[] { "https://example.com/alice" }, profile.SameAs);
    }

    [Fact]
    public void Create_ProjectsAuthoredByScalarId()
    {
        var doc = CreateDoc(extraFields: new Dictionary<string, ContentField>
        {
            ["authoredBy"] = new ContentField("text", "alice-1")
        });

        Assert.True(doc.Record.Ownership.UsesAuthorRelation);
        var profile = Assert.Single(doc.Record.Ownership.AuthorProfiles);
        Assert.Equal("alice-1", profile.Id);
        Assert.Null(profile.Title);
        Assert.Empty(profile.SameAs);
    }

    [Fact]
    public void Create_WithoutAuthoredBy_LeavesAuthorProfilesEmpty()
    {
        var doc = CreateDoc();

        Assert.False(doc.Record.Ownership.UsesAuthorRelation);
        Assert.Empty(doc.Record.Ownership.AuthorProfiles);
    }

    [Fact]
    public void Create_SetsClassificationFromSectionsAndTags()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["sections"] = new ContentField("list", new List<object> { new ContentField("text", "hero"), new ContentField("text", "footer") }),
            ["tags"] = new ContentField("list", new List<object> { new ContentField("text", "dotnet"), new ContentField("text", "ssg") })
        };
        var doc = CreateDoc(extraFields: fields);
        Assert.Equal(2, doc.Record.Classification.Sections.Count);
        Assert.Equal(2, doc.Record.Classification.Tags.Count);
        Assert.Equal(2, doc.Record.Classification.Sections.Count);
        Assert.Equal(2, doc.Record.Classification.Tags.Count);
    }

    [Theory]
    [InlineData("summary", "Summary text")]
    [InlineData("description", "Description text")]
    [InlineData("excerpt", "Excerpt text")]
    public void Create_Summary_FallsBackThroughDescriptionAndExcerpt(string field, string expected)
    {
        var doc = CreateDoc(extraFields: new Dictionary<string, ContentField>
        {
            [field] = new ContentField("text", expected)
        });

        Assert.Equal(expected, doc.Record.Presentation.Summary);
    }

    [Fact]
    public void Create_Summary_PrefersSummaryOverDescriptionAndExcerpt()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["summary"] = new ContentField("text", "summary"),
            ["description"] = new ContentField("text", "description"),
            ["excerpt"] = new ContentField("text", "excerpt")
        };

        var doc = CreateDoc(extraFields: fields);

        Assert.Equal("summary", doc.Record.Presentation.Summary);
    }

    [Fact]
    public void Create_Summary_PrefersDescriptionOverExcerpt()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["description"] = new ContentField("text", "description"),
            ["excerpt"] = new ContentField("text", "excerpt")
        };

        var doc = CreateDoc(extraFields: fields);

        Assert.Equal("description", doc.Record.Presentation.Summary);
    }

    [Fact]
    public void Create_Categories_ProvideSectionsFallbackAndMergeIntoTags()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["categories"] = new ContentField("list", new List<object>
            {
                "news",
                "dotnet"
            }),
            ["tags"] = new ContentField("list", new List<object>
            {
                "dotnet",
                "ssg"
            })
        };

        var doc = CreateDoc(extraFields: fields);

        Assert.Equal(new[] { "news", "dotnet" }, doc.Record.Classification.Sections);
        Assert.Equal(new[] { "dotnet", "ssg", "news" }, doc.Record.Classification.Tags);
    }

    [Fact]
    public void Create_ExplicitSections_TakePrecedenceOverCategories()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["sections"] = new ContentField("list", new List<object>
            {
                "hero"
            }),
            ["categories"] = new ContentField("list", new List<object>
            {
                "news"
            })
        };

        var doc = CreateDoc(extraFields: fields);

        Assert.Equal(new[] { "hero" }, doc.Record.Classification.Sections);
    }

    [Fact]
    public void Create_ProjectsImageAndCoverAsMediaAssets()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["image"] = new ContentField("text", "/assets/hero.png"),
            ["image_alt"] = new ContentField("text", "Hero"),
            ["image_caption"] = new ContentField("text", "Caption"),
            ["image_description"] = new ContentField("text", "Description"),
            ["image_license"] = new ContentField("text", "CC-BY"),
            ["cover"] = new ContentField("text", "/assets/cover.png")
        };

        var doc = CreateDoc(extraFields: fields);

        Assert.Equal(2, doc.Record.Media.Count);
        var image = Assert.Single(doc.Record.Media, item => item.Url == "/assets/hero.png");
        Assert.Equal("image", image.Kind);
        Assert.Equal("Hero", image.Alt);
        Assert.Equal("Caption", image.Caption);
        Assert.Equal("Description", image.Description);
        Assert.Equal("CC-BY", image.License);
        Assert.Contains(doc.Record.Media, item => item.Kind == "image" && item.Url == "/assets/cover.png");
    }

    [Fact]
    public void Create_ProjectsTranslationsRelatedContentAndNamedEntitiesAsDirectedRelations()
    {
        var fields = new Dictionary<string, ContentField>
        {
            ["translations"] = new ContentField("list", new List<object>
            {
                "fr"
            }),
            ["related_to"] = new ContentField("list", new List<object>
            {
                "post-2"
            }),
            ["products"] = new ContentField("list", new List<object>
            {
                "Widget"
            })
        };

        var doc = CreateDoc(extraFields: fields);

        Assert.Contains(doc.Record.Entities, entity => entity.Type == "product" && entity.Name == "Widget");
        Assert.Contains(doc.Record.Relations, relation =>
            relation.Type == "translation-of" &&
            relation.Target == "fr" &&
            relation.TargetType == "content" &&
            relation.TargetId == "fr");
        Assert.Contains(doc.Record.Relations, relation =>
            relation.Type == "related-to" &&
            relation.Target == "post-2" &&
            relation.TargetType == "content" &&
            relation.TargetId == "post-2");
        Assert.Contains(doc.Record.Relations, relation =>
            relation.Type == "mentions" &&
            relation.Target == "Widget" &&
            relation.TargetType == "product");
    }

    [Fact]
    public void Create_MapsReviewedAt_ButNotVerifiedAt()
    {
        var reviewed = DateTimeOffset.Parse("2026-05-01T00:00:00Z");
        var verified = DateTimeOffset.Parse("2026-06-01T00:00:00Z");

        var reviewedDoc = CreateDoc(extraFields: new Dictionary<string, ContentField>
        {
            ["reviewed_at"] = new ContentField("date", reviewed),
            ["verified_at"] = new ContentField("date", verified)
        });
        Assert.Equal(reviewed, reviewedDoc.Record.Lifecycle.ReviewedAt);

        var verifiedOnly = CreateDoc(extraFields: new Dictionary<string, ContentField>
        {
            ["verified_at"] = new ContentField("date", verified)
        });
        Assert.Null(verifiedOnly.Record.Lifecycle.ReviewedAt);
    }

    [Fact]
    public void ComputedProperties_MatchRecordValues()
    {
        var doc = CreateDoc();
        Assert.Equal("post-1", doc.Id);
        Assert.Equal("Test Post", doc.Title);
        Assert.Equal("test-post", doc.Slug);
        Assert.Equal(2026, doc.PublishAt.Year);
    }

    [Fact]
    public void Constructor_DefaultsRouteAndPublishAndSource()
    {
        var record = new ContentRecord(
            new ContentIdentity("x", "x", "x", "page", "published"),
            new ContentPresentation("X", null, null, "en", Array.Empty<string>()),
            new ContentClassification("page", "pages", Array.Empty<string>(), Array.Empty<string>()),
            new ContentOwnership(null, null, null, null),
            new ContentLifecycle(DateTimeOffset.UtcNow, null, null, null),
            new ProvenanceRecord(null, null, Array.Empty<string>(), Array.Empty<string>(), null),
            new TrustMetadata(null, "unchecked", Array.Empty<string>()),
            Array.Empty<EntityRecord>(),
            Array.Empty<ContentRelation>(),
            Array.Empty<MediaAsset>());
        var doc = new ContentDocument(record, new ContentBodyRef(""));
        Assert.NotNull(doc.Route);
        Assert.NotNull(doc.Publish);
        Assert.NotNull(doc.Source);
        Assert.Empty(doc.Diagnostics);
    }
}
