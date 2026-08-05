using System.Reflection;
using System.Text.Json;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Rendering;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class CompanyEntityAndEmptyCollectionTests : IDisposable
{
    private readonly List<string> _tempDirectories = new();

    [Fact]
    public void BuildForContent_CompanyEntity_DefaultsToOrganization_AndDoesNotInferRawFields()
    {
        var document = CompanyDocument("Organization") with
        {
            Record = CompanyDocument("Organization").Record with
            {
                Entities =
                [
                    new EntityRecord(
                        "company",
                        "Acme Malaysia",
                        "Verified company description.",
                        Url: "https://example.com/companies/acme/",
                        SameAs: ["https://www.linkedin.com/company/acme/"])
                ]
            }
        };

        var model = SeoModelBuilder.BuildForContent(CreateConfig(), "/", document, CompanyRoute());
        using var organization = JsonDocuments(model.JsonLd)
            .Single(json => json.RootElement.GetProperty("@type").GetString() == "Organization");

        Assert.Equal("Acme Malaysia", organization.RootElement.GetProperty("name").GetString());
        Assert.Equal("Verified company description.", organization.RootElement.GetProperty("description").GetString());
        Assert.Equal("https://example.com/companies/acme/", organization.RootElement.GetProperty("url").GetString());
        Assert.False(organization.RootElement.TryGetProperty("telephone", out _));
        Assert.False(organization.RootElement.TryGetProperty("address", out _));
        Assert.False(organization.RootElement.TryGetProperty("numberOfEmployees", out _));
        Assert.DoesNotContain("+60", string.Concat(model.JsonLd), StringComparison.Ordinal);
        Assert.DoesNotContain("raw-address", string.Concat(model.JsonLd), StringComparison.Ordinal);
    }

    [Fact]
    public void BuildForContent_LocalBusinessWithoutVerifiedCanonicalProfile_FallsBackToOrganization()
    {
        var document = CompanyDocument("LocalBusiness") with
        {
            Record = CompanyDocument("LocalBusiness").Record with
            {
                Entities = [new EntityRecord("company", "Acme Malaysia", "Verified company description.")]
            }
        };

        var model = SeoModelBuilder.BuildForContent(CreateConfig(), "/", document, CompanyRoute());
        var types = JsonDocuments(model.JsonLd)
            .Select(json => json.RootElement.GetProperty("@type").GetString())
            .ToArray();

        Assert.Contains("Organization", types);
        Assert.DoesNotContain("LocalBusiness", types);
    }

    [Fact]
    public void BuildForContent_LocalBusinessWithCompleteVerifiedCanonicalProfile_EmitsLocalBusiness()
    {
        var company = new EntityRecord("company", "Acme Malaysia", "Corporate description.")
        {
            LocalBusinessProfile = new LocalBusinessProfile
            {
                AddressVerified = true,
                LocalOperationsVerified = true,
                StreetAddress = "10 Jalan Example",
                AddressLocality = "Kuala Lumpur",
                AddressRegion = "Kuala Lumpur",
                PostalCode = "50000",
                AddressCountry = "MY",
                LocalOperationsDescription = "Provides verified local operations in Kuala Lumpur."
            }
        };
        var document = CompanyDocument("LocalBusiness") with
        {
            Record = CompanyDocument("LocalBusiness").Record with { Entities = [company] }
        };

        var model = SeoModelBuilder.BuildForContent(CreateConfig(), "/", document, CompanyRoute());
        using var localBusiness = JsonDocuments(model.JsonLd)
            .Single(json => json.RootElement.GetProperty("@type").GetString() == "LocalBusiness");

        Assert.Equal("10 Jalan Example", localBusiness.RootElement.GetProperty("address").GetProperty("streetAddress").GetString());
        Assert.Equal("Provides verified local operations in Kuala Lumpur.", localBusiness.RootElement.GetProperty("description").GetString());
    }

    [Fact]
    public void BuildForContent_MappedLocalBusinessProfile_PropagatesVerifiedCanonicalFieldsToJsonLd()
    {
        var document = NormalizedCompanyDocument(localOperationsVerified: true);

        var model = SeoModelBuilder.BuildForContent(CreateConfig(), "/", document, CompanyRoute());
        using var localBusiness = JsonDocuments(model.JsonLd)
            .Single(json => json.RootElement.GetProperty("@type").GetString() == "LocalBusiness");

        Assert.Equal("10 Jalan Example", localBusiness.RootElement.GetProperty("address").GetProperty("streetAddress").GetString());
        Assert.Equal("Provides verified local operations in Kuala Lumpur.", localBusiness.RootElement.GetProperty("description").GetString());
    }

    [Fact]
    public void BuildForContent_NullableMappedLocalBusinessProfile_PropagatesToJsonLd()
    {
        var document = NormalizedCompanyDocument(localOperationsVerified: true, nullableMaps: true);

        var model = SeoModelBuilder.BuildForContent(CreateConfig(), "/", document, CompanyRoute());
        using var localBusiness = JsonDocuments(model.JsonLd)
            .Single(json => json.RootElement.GetProperty("@type").GetString() == "LocalBusiness");

        Assert.Equal("10 Jalan Example", localBusiness.RootElement.GetProperty("address").GetProperty("streetAddress").GetString());
    }

    [Fact]
    public void BuildForContent_MappedLocalBusinessProfileWithoutBothVerifications_FallsBackToOrganization()
    {
        var document = NormalizedCompanyDocument(localOperationsVerified: false);

        var model = SeoModelBuilder.BuildForContent(CreateConfig(), "/", document, CompanyRoute());
        var types = JsonDocuments(model.JsonLd)
            .Select(json => json.RootElement.GetProperty("@type").GetString())
            .ToArray();

        Assert.Contains("Organization", types);
        Assert.DoesNotContain("LocalBusiness", types);
        Assert.DoesNotContain("10 Jalan Example", string.Concat(model.JsonLd), StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyCollectionIndexability_ExcludesSearchAndLlmsConsumers_AndContentRestoresThem()
    {
        var config = CreateConfig(noindexWhenEmpty: true);
        var emptyGraph = CollectionGraph(totalItems: 0);
        var emptyIndex = SeoIndexBuilder.Build(
            config,
            "/",
            Array.Empty<RoutedContentDocument>(),
            Array.Empty<RouteInfo>(),
            new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            emptyGraph);
        var emptyOutput = CreateTempDirectory();

        SearchIndexBuilder.GenerateSingleSearchIndex(
            emptyOutput,
            "/",
            includeDerived: false,
            emitSnippet: false,
            maxContentLength: 120,
            routed: Array.Empty<RoutedContentDocument>(),
            derivedRouted: Array.Empty<RoutedContentDocument>(),
            emptyIndex.Entries,
            NullContentBodyStore.Instance,
            emptyGraph,
            emptyIndex.Models);
        LlmsTxtPlugin.WriteLlmsTxt(
            config,
            emptyOutput,
            "/",
            Array.Empty<RoutedContentDocument>(),
            Array.Empty<RoutedContentDocument>(),
            emptyIndex.Entries,
            emptyIndex.Models,
            config.Site.Seo.Geo);
        LlmsTxtPlugin.WriteLlmsFullTxt(
            config,
            emptyOutput,
            "/",
            Array.Empty<RoutedContentDocument>(),
            Array.Empty<RoutedContentDocument>(),
            CanonicalContentGraph.Empty,
            emptyIndex.Entries,
            NullContentBodyStore.Instance);

        using (var search = JsonDocument.Parse(File.ReadAllText(Path.Combine(emptyOutput, "search.json"))))
        {
            Assert.Empty(search.RootElement.EnumerateArray());
        }
        Assert.DoesNotContain("/companies/", File.ReadAllText(Path.Combine(emptyOutput, "llms.txt")), StringComparison.Ordinal);
        Assert.DoesNotContain("/companies/", File.ReadAllText(Path.Combine(emptyOutput, "llms-full.txt")), StringComparison.Ordinal);

        var document = CompanyDocument("Organization");
        var routed = new[] { new RoutedContentDocument(document, CompanyRoute()) };
        var contentGraph = CollectionGraph(totalItems: 1);
        var contentIndex = SeoIndexBuilder.Build(
            config,
            "/",
            routed,
            Array.Empty<RouteInfo>(),
            new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            contentGraph);
        var contentOutput = CreateTempDirectory();

        SearchIndexBuilder.GenerateSingleSearchIndex(
            contentOutput,
            "/",
            includeDerived: false,
            emitSnippet: false,
            maxContentLength: 120,
            routed,
            Array.Empty<RoutedContentDocument>(),
            contentIndex.Entries,
            NullContentBodyStore.Instance,
            contentGraph,
            contentIndex.Models);
        LlmsTxtPlugin.WriteLlmsTxt(config, contentOutput, "/", routed, Array.Empty<RoutedContentDocument>(), contentIndex.Entries, contentIndex.Models, config.Site.Seo.Geo);
        LlmsTxtPlugin.WriteLlmsFullTxt(config, contentOutput, "/", routed, Array.Empty<RoutedContentDocument>(), CanonicalContentGraphBuilder.BuildFromDocuments([document]), contentIndex.Entries, NullContentBodyStore.Instance);

        using (var search = JsonDocument.Parse(File.ReadAllText(Path.Combine(contentOutput, "search.json"))))
        {
            Assert.Contains(search.RootElement.EnumerateArray(), item => item.GetProperty("url").GetString() == "/companies/");
        }
        Assert.Contains("/companies/acme/", File.ReadAllText(Path.Combine(contentOutput, "llms.txt")), StringComparison.Ordinal);
        Assert.Contains("/companies/acme/", File.ReadAllText(Path.Combine(contentOutput, "llms-full.txt")), StringComparison.Ordinal);
    }

    [Fact]
    public void BuildForContent_CompanyOrganizationMatchingSiteOrganization_IsDeduplicatedByAbsoluteUrl()
    {
        var document = CompanyDocument("Organization") with
        {
            Record = CompanyDocument("Organization").Record with
            {
                Entities = [new EntityRecord("company", "Acme Malaysia", Url: "https://example.com/companies/acme/")]
            }
        };
        var config = CreateConfig(organization: new SeoOrganizationConfig
        {
            Name = "Acme Malaysia",
            Url = "/companies/acme/"
        });

        var model = SeoModelBuilder.BuildForContent(config, "/", document, CompanyRoute());

        Assert.Equal(1, JsonDocuments(model.JsonLd).Count(json => json.RootElement.GetProperty("@type").GetString() == "Organization"));
    }

    [Fact]
    public void BuildForContent_DifferentCompanyOrganization_DoesNotMergeWithSiteOrganization()
    {
        var document = CompanyDocument("Organization") with
        {
            Record = CompanyDocument("Organization").Record with
            {
                Entities = [new EntityRecord("company", "Acme Malaysia", Url: "https://example.com/companies/acme/")]
            }
        };
        var config = CreateConfig(organization: new SeoOrganizationConfig
        {
            Name = "Bukit",
            Url = "https://example.com/"
        });

        var model = SeoModelBuilder.BuildForContent(config, "/", document, CompanyRoute());

        Assert.Equal(2, JsonDocuments(model.JsonLd).Count(json => json.RootElement.GetProperty("@type").GetString() == "Organization"));
    }

    [Fact]
    public void BuildForContent_SameNameOrganizationsWithoutUrls_AreNotMerged()
    {
        var document = CompanyDocument("Organization") with
        {
            Record = CompanyDocument("Organization").Record with
            {
                Entities = [new EntityRecord("company", "Acme Malaysia")]
            }
        };
        var config = CreateConfig(organization: new SeoOrganizationConfig { Name = "Acme Malaysia" });

        var model = SeoModelBuilder.BuildForContent(config, "/", document, CompanyRoute());

        Assert.Equal(2, JsonDocuments(model.JsonLd).Count(json => json.RootElement.GetProperty("@type").GetString() == "Organization"));
    }

    [Fact]
    public void CanonicalEntity_KeepsInternalInitOnlyVerifiedLocalBusinessProfile()
    {
        var property = typeof(EntityRecord).GetProperty(
            "LocalBusinessProfile",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        Assert.True(property.GetMethod?.IsAssembly);

        Assert.True(property.SetMethod?.ReturnParameter.GetRequiredCustomModifiers()
            .Any(modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit"));
    }

    [Fact]
    public void Build_EmptyPrimaryCollectionWithNoindexPolicy_UsesNoindexAndEpochLastModifiedSentinel()
    {
        var config = CreateConfig(noindexWhenEmpty: true);
        var route = new RouteInfo("/companies/", "companies/index.html", "pages/list.html");

        var result = SeoIndexBuilder.Build(
            config,
            "/",
            Array.Empty<RoutedContentDocument>(),
            [route],
            new Dictionary<string, IReadOnlyList<SeoAlternateModel>>());
        var entry = result.Entries["companies/index.html"];
        var model = result.Models["companies/index.html"];

        Assert.Equal("noindex,follow", model.Robots);
        Assert.False(entry.Indexable);
        Assert.Equal(typeof(DateTimeOffset), typeof(SeoIndexEntry).GetProperty(nameof(entry.LastModified))?.PropertyType);
        Assert.Equal(DateTimeOffset.UnixEpoch, entry.LastModified);
    }

    [Fact]
    public void Build_EmptyFilteredCollectionInheritsNoindexPolicyAndAggregateExclusions()
    {
        var config = CreateConfig(noindexWhenEmpty: true);
        var graph = FilteredCollectionGraph(totalItems: 0);
        var result = SeoIndexBuilder.Build(
            config,
            "/",
            Array.Empty<RoutedContentDocument>(),
            Array.Empty<RouteInfo>(),
            new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            graph);
        var entry = result.Entries["companies/malaysia/index.html"];
        var model = result.Models["companies/malaysia/index.html"];

        Assert.Equal("noindex,follow", model.Robots);
        Assert.False(entry.Indexable);
        Assert.Equal(DateTimeOffset.UnixEpoch, entry.LastModified);

        var outputDir = CreateTempDirectory();
        var context = new PublishProjectionContext(
            config,
            outputDir,
            CanonicalContentGraph.Empty,
            result.Entries,
            result.Models,
            Array.Empty<RoutedContentDocument>(),
            NullContentBodyStore.Instance,
            ListRouteGraph: graph);
        foreach (var kind in new[] { "sitemap", "search", "llms", "llms-full" })
        {
            PublishRepresentationRegistry.AggregateProjectionAdapters()
                .Single(adapter => adapter.Representation.Kind == kind)
                .Project(context);
        }

        Assert.DoesNotContain("/companies/malaysia/", File.ReadAllText(Path.Combine(outputDir, "sitemap.xml")), StringComparison.Ordinal);
        Assert.DoesNotContain("/companies/malaysia/", File.ReadAllText(Path.Combine(outputDir, "search.json")), StringComparison.Ordinal);
        Assert.DoesNotContain("/companies/malaysia/", File.ReadAllText(Path.Combine(outputDir, "llms.txt")), StringComparison.Ordinal);
        var llmsFullPath = Path.Combine(outputDir, "llms-full.txt");
        Assert.False(
            File.Exists(llmsFullPath) &&
            File.ReadAllText(llmsFullPath).Contains("/companies/malaysia/", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_ThinCollectionBelowMinimum_ExcludesListFromProjectionsButKeepsContent()
    {
        var config = CreateConfig(indexPolicy: new CollectionIndexPolicyConfig
        {
            MinimumItems = 3,
            BelowMinimum = "noindex-follow"
        });
        var acme = CompanyDocument("Organization");
        var beta = ContentDocument.Create(
            "beta",
            "Beta Malaysia",
            "beta",
            new DateTimeOffset(2026, 7, 26, 9, 30, 0, TimeSpan.Zero),
            "<p>Beta</p>",
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["collection"] = "companies",
                ["type"] = "company"
            }));
        var routed = new[]
        {
            new RoutedContentDocument(acme, CompanyRoute()),
            new RoutedContentDocument(beta, new RouteInfo("/companies/beta/", "companies/beta/index.html", "pages/company.html"))
        };
        var graph = CollectionGraph(totalItems: 2);

        var result = SeoIndexBuilder.Build(
            config,
            "/",
            routed,
            Array.Empty<RouteInfo>(),
            new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            graph);

        var listEntry = result.Entries["companies/index.html"];
        Assert.Equal("noindex,follow", result.Models["companies/index.html"].Robots);
        Assert.False(listEntry.Indexable);
        Assert.True(result.Entries["companies/acme/index.html"].Indexable);
        Assert.True(result.Entries["companies/beta/index.html"].Indexable);

        var outputDir = CreateTempDirectory();
        var context = new PublishProjectionContext(
            config,
            outputDir,
            CanonicalContentGraphBuilder.BuildFromDocuments([acme, beta]),
            result.Entries,
            result.Models,
            routed,
            NullContentBodyStore.Instance,
            ListRouteGraph: graph);
        foreach (var kind in new[] { "sitemap", "search", "llms", "llms-full" })
        {
            PublishRepresentationRegistry.AggregateProjectionAdapters()
                .Single(adapter => adapter.Representation.Kind == kind)
                .Project(context);
        }

        var sitemap = File.ReadAllText(Path.Combine(outputDir, "sitemap.xml"));
        Assert.DoesNotContain("<loc>https://example.com/companies/</loc>", sitemap, StringComparison.Ordinal);
        Assert.Contains("https://example.com/companies/acme/", sitemap, StringComparison.Ordinal);
        Assert.Contains("https://example.com/companies/beta/", sitemap, StringComparison.Ordinal);

        using (var search = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputDir, "search.json"))))
        {
            Assert.DoesNotContain(search.RootElement.EnumerateArray(), item => item.GetProperty("url").GetString() == "/companies/");
            Assert.Contains(search.RootElement.EnumerateArray(), item => item.GetProperty("url").GetString() == "/companies/acme/");
        }

        var llmsText = File.ReadAllText(Path.Combine(outputDir, "llms.txt"));
        Assert.DoesNotContain("](https://example.com/companies/)", llmsText, StringComparison.Ordinal);
        Assert.Contains("https://example.com/companies/acme/", llmsText, StringComparison.Ordinal);

        var llmsFullPath = Path.Combine(outputDir, "llms-full.txt");
        var llmsFull = File.Exists(llmsFullPath) ? File.ReadAllText(llmsFullPath) : string.Empty;
        Assert.DoesNotContain("URL: https://example.com/companies/" + "\n", llmsFull, StringComparison.Ordinal);
        if (llmsFull.Length > 0)
        {
            Assert.Contains("https://example.com/companies/acme/", llmsFull, StringComparison.Ordinal);
            Assert.Contains("https://example.com/companies/beta/", llmsFull, StringComparison.Ordinal);
        }

        var audit = MachineReadabilityTrustAuditBuilder.Build(
            config,
            outputDir,
            result.Entries,
            result.Models,
            CanonicalContentGraphBuilder.BuildFromDocuments([acme, beta]),
            requireHreflangTargets: false);
        var listRoute = audit.SeoReport.Routes.Single(route => route.Url == "/companies/");
        Assert.False(listRoute.Indexable);
        Assert.Equal("noindex,follow", listRoute.Robots);
        Assert.False(listRoute.SearchIncluded);
        Assert.DoesNotContain(audit.SeoReport.Issues, issue =>
            issue.Route == "/companies/" &&
            issue.Code is "seo.sitemap_missing_url" or "publish.llms_excluded_route_present");
    }

    [Fact]
    public void Build_EmptyListEpochSentinelIsNullInSeoAndPublishAuditModels()
    {
        var config = CreateConfig(noindexWhenEmpty: true);
        var graph = FilteredCollectionGraph(totalItems: 0);
        var result = SeoIndexBuilder.Build(
            config,
            "/",
            Array.Empty<RoutedContentDocument>(),
            Array.Empty<RouteInfo>(),
            new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            graph);
        var audit = MachineReadabilityTrustAuditBuilder.Build(
            config,
            CreateTempDirectory(),
            result.Entries,
            result.Models,
            CanonicalContentGraph.Empty,
            requireHreflangTargets: false);

        Assert.Equal(DateTimeOffset.UnixEpoch, Assert.Single(result.Entries).Value.LastModified);
        Assert.Null(Assert.Single(audit.SeoReport.Routes).LastModified);
        Assert.Null(Assert.Single(audit.PublishReport.Documents).LastModified);
    }

    [Fact]
    public void Build_PrimaryCollectionWithContent_RecoversIndexabilityAndUsesContentDate()
    {
        var publishedAt = new DateTimeOffset(2026, 7, 25, 9, 30, 0, TimeSpan.Zero);
        var document = ContentDocument.Create(
            "acme",
            "Acme Malaysia",
            "acme",
            publishedAt,
            "<p>Acme</p>",
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["collection"] = "companies",
                ["type"] = "company"
            }));
        var route = new RouteInfo("/companies/", "companies/index.html", "pages/list.html");
        var result = SeoIndexBuilder.Build(
            CreateConfig(noindexWhenEmpty: true),
            "/",
            [new RoutedContentDocument(document, new RouteInfo("/companies/acme/", "companies/acme/index.html", "pages/company.html"))],
            [route],
            new Dictionary<string, IReadOnlyList<SeoAlternateModel>>());
        var entry = result.Entries["companies/index.html"];
        var model = result.Models["companies/index.html"];

        Assert.Null(model.Robots);
        Assert.True(entry.Indexable);
        Assert.Equal(publishedAt, entry.LastModified);
    }

    [Theory]
    [InlineData("CollectionList", 0)]
    [InlineData("CollectionList", 1)]
    [InlineData("CollectionList", 2)]
    [InlineData("CollectionPage", 0)]
    [InlineData("CollectionPage", 1)]
    [InlineData("CollectionPage", 2)]
    [InlineData("FilteredListPage", 0)]
    [InlineData("FilteredListPage", 1)]
    [InlineData("FilteredListPage", 2)]
    public void Build_BelowMinimumItems_NoindexesCollectionListRoutes(string kindName, int totalItems)
    {
        var kind = Enum.Parse<ListRouteKind>(kindName);
        var config = CreateConfig(indexPolicy: new CollectionIndexPolicyConfig
        {
            MinimumItems = 3,
            BelowMinimum = "noindex-follow"
        });
        var graph = PolicyGraph(kind, totalItems);

        var result = SeoIndexBuilder.Build(
            config,
            "/",
            Array.Empty<RoutedContentDocument>(),
            Array.Empty<RouteInfo>(),
            new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            graph);

        var key = PlanOutputPath(kind);
        Assert.Equal("noindex,follow", result.Models[key].Robots);
        Assert.False(result.Entries[key].Indexable);
    }

    [Theory]
    [InlineData("CollectionList")]
    [InlineData("CollectionPage")]
    [InlineData("FilteredListPage")]
    public void Build_AtMinimumItems_RestoresIndexability(string kindName)
    {
        var kind = Enum.Parse<ListRouteKind>(kindName);
        var config = CreateConfig(indexPolicy: new CollectionIndexPolicyConfig
        {
            MinimumItems = 3,
            BelowMinimum = "noindex-follow"
        });
        var graph = PolicyGraph(kind, totalItems: 3);

        var result = SeoIndexBuilder.Build(
            config,
            "/",
            Array.Empty<RoutedContentDocument>(),
            Array.Empty<RouteInfo>(),
            new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            graph);

        var key = PlanOutputPath(kind);
        Assert.Null(result.Models[key].Robots);
        Assert.True(result.Entries[key].Indexable);
    }

    [Theory]
    [InlineData("CollectionList")]
    [InlineData("CollectionPage")]
    [InlineData("FilteredListPage")]
    public void Build_BelowMinimumWithIndexBehavior_StaysIndexable(string kindName)
    {
        var kind = Enum.Parse<ListRouteKind>(kindName);
        var config = CreateConfig(indexPolicy: new CollectionIndexPolicyConfig
        {
            MinimumItems = 3,
            BelowMinimum = "index"
        });
        var graph = PolicyGraph(kind, totalItems: 1);

        var result = SeoIndexBuilder.Build(
            config,
            "/",
            Array.Empty<RoutedContentDocument>(),
            Array.Empty<RouteInfo>(),
            new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            graph);

        var key = PlanOutputPath(kind);
        Assert.Null(result.Models[key].Robots);
        Assert.True(result.Entries[key].Indexable);
    }

    [Fact]
    public void Build_MinimumItemsPolicy_DoesNotTouchContentDetailOrHomeRoutes()
    {
        var config = CreateConfig(indexPolicy: new CollectionIndexPolicyConfig
        {
            MinimumItems = 3,
            BelowMinimum = "noindex-follow"
        });
        var document = CompanyDocument("Organization");

        var result = SeoIndexBuilder.Build(
            config,
            "/",
            [new RoutedContentDocument(document, CompanyRoute())],
            Array.Empty<RouteInfo>(),
            new Dictionary<string, IReadOnlyList<SeoAlternateModel>>());

        var detail = result.Entries["companies/acme/index.html"];
        Assert.True(detail.Indexable);
        Assert.Null(result.Models["companies/acme/index.html"].Robots);
    }

    [Fact]
    public void Build_DefaultIndexPolicy_KeepsEmptyCollectionIndexable()
    {
        var graph = PolicyGraph(ListRouteKind.CollectionList, totalItems: 0);

        var result = SeoIndexBuilder.Build(
            CreateConfig(),
            "/",
            Array.Empty<RoutedContentDocument>(),
            Array.Empty<RouteInfo>(),
            new Dictionary<string, IReadOnlyList<SeoAlternateModel>>(),
            graph);

        Assert.Null(result.Models["companies/index.html"].Robots);
        Assert.True(result.Entries["companies/index.html"].Indexable);
    }

    [Fact]
    public void SitemapSerializers_OmitNullAndEpochLastModified()
    {
        var relativeOutput = CreateTempDirectory();
        var absoluteOutput = CreateTempDirectory();
        var alternateOutput = CreateTempDirectory();
        SitemapGenerator.Generate(
            relativeOutput,
            "https://example.com",
            "/",
            new (RouteInfo Route, DateTimeOffset? LastModified)[]
            {
                (new RouteInfo("/companies/", "companies/index.html", "pages/list.html"), null)
            });
        SitemapGenerator.GenerateAbsolute(
            absoluteOutput,
            new (string AbsoluteUrl, DateTimeOffset? LastModified)[] { ("https://example.com/companies/", null) });
        SitemapGenerator.GenerateAbsoluteWithAlternates(
            alternateOutput,
            [new SitemapGenerator.UrlEntry("https://example.com/companies/", DateTimeOffset.UnixEpoch, [new SitemapGenerator.Alternate("en", "https://example.com/companies/")])]);

        foreach (var output in new[] { relativeOutput, absoluteOutput, alternateOutput })
        {
            var xml = File.ReadAllText(Path.Combine(output, "sitemap.xml"));
            Assert.DoesNotContain("<lastmod>", xml, StringComparison.Ordinal);
            Assert.DoesNotContain("1970-01-01", xml, StringComparison.Ordinal);
        }
    }

    public void Dispose()
    {
        foreach (var directory in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    private static AppConfig CreateConfig(bool noindexWhenEmpty = false, SeoOrganizationConfig? organization = null, CollectionIndexPolicyConfig? indexPolicy = null)
    {
        var collection = new CollectionConfig
        {
            Permalink = "/companies/:slug/",
            ListRoute = "/companies/"
        };
        var property = typeof(CollectionConfig).GetProperty("NoindexWhenEmpty");
        Assert.NotNull(property);
        property.SetValue(collection, noindexWhenEmpty);
        if (indexPolicy is not null)
        {
            collection = collection with { IndexPolicy = indexPolicy };
        }

        return new AppConfig
        {
            Site = new SiteConfig
            {
                Name = "company-test",
                Title = "Company test",
                Url = "https://example.com",
                Seo = new SeoConfig
                {
                    Organization = organization,
                    Schema = new SeoSchemaConfig { WebPage = false, CollectionPage = false, SearchAction = false }
                },
                Collections = new Dictionary<string, CollectionConfig> { ["companies"] = collection }
            },
            Content = TestContent.Markdown()
        };
    }

    private static ContentDocument CompanyDocument(string schemaType)
        => ContentDocument.Create(
            "acme",
            "Acme Malaysia",
            "acme",
            new DateTimeOffset(2026, 7, 25, 9, 30, 0, TimeSpan.Zero),
            "<p>Acme</p>",
            ContentFieldReader.ToFieldMap(new Dictionary<string, object>
            {
                ["collection"] = "companies",
                ["type"] = "company",
                ["schema_type"] = schemaType,
                ["phone"] = "+60 123456789",
                ["address"] = "raw-address",
                ["customer_count"] = "1000",
                ["employee_count"] = "200",
                ["social"] = "https://untrusted.example/social"
            }));

    private static RouteInfo CompanyRoute()
        => new("/companies/acme/", "companies/acme/index.html", "pages/company.html");

    private static ListRouteGraph CollectionGraph(int totalItems)
        => ListRouteGraph.Create(
        [
            new ListRoutePlan
            {
                RouteId = "collection:companies:1",
                Kind = ListRouteKind.CollectionList,
                Url = "/companies/",
                OutputPath = "companies/index.html",
                Template = "pages/list.html",
                Collection = "companies",
                TotalItems = totalItems,
                CanonicalUrl = "/companies/"
            }
        ]);

    private static ListRouteGraph FilteredCollectionGraph(int totalItems)
        => ListRouteGraph.Create(
        [
            new ListRoutePlan
            {
                RouteId = "filter:companies:country:malaysia:1",
                Kind = ListRouteKind.FilteredListPage,
                Url = "/companies/malaysia/",
                OutputPath = "companies/malaysia/index.html",
                Template = "pages/company-list.html",
                Collection = "companies",
                PageNumber = 1,
                PageSize = 10,
                TotalItems = totalItems,
                CanonicalUrl = "/companies/malaysia/",
                FilterContext = new ListRouteFilterContext
                {
                    Field = "country",
                    Value = "Malaysia"
                }
            }
        ]);

    private static ListRouteGraph PolicyGraph(ListRouteKind kind, int totalItems)
    {
        var plan = new ListRoutePlan
        {
            RouteId = $"policy:{kind}:{totalItems}",
            Kind = kind,
            Url = PlanUrl(kind),
            OutputPath = PlanOutputPath(kind),
            Template = "pages/list.html",
            Collection = "companies",
            TotalItems = totalItems,
            CanonicalUrl = PlanUrl(kind)
        };
        if (kind is ListRouteKind.CollectionPage)
        {
            plan = plan with { PageNumber = 2, PageSize = 10 };
        }

        if (kind is ListRouteKind.FilteredListPage)
        {
            plan = plan with
            {
                PageNumber = 1,
                PageSize = 10,
                FilterContext = new ListRouteFilterContext
                {
                    Field = "country",
                    Value = "Malaysia"
                }
            };
        }

        return ListRouteGraph.Create([plan]);
    }

    private static string PlanUrl(ListRouteKind kind)
        => kind switch
        {
            ListRouteKind.CollectionPage => "/companies/page/2/",
            ListRouteKind.FilteredListPage => "/companies/malaysia/",
            _ => "/companies/"
        };

    private static string PlanOutputPath(ListRouteKind kind)
        => kind switch
        {
            ListRouteKind.CollectionPage => "companies/page/2/index.html",
            ListRouteKind.FilteredListPage => "companies/malaysia/index.html",
            _ => "companies/index.html"
        };

    private static ContentDocument NormalizedCompanyDocument(bool localOperationsVerified, bool nullableMaps = false)
    {
        object localBusinessProfile = nullableMaps
            ? new Dictionary<string, object?>
            {
                ["addressVerified"] = true,
                ["localOperationsVerified"] = localOperationsVerified,
                ["streetAddress"] = "10 Jalan Example",
                ["addressLocality"] = "Kuala Lumpur",
                ["addressRegion"] = "Kuala Lumpur",
                ["postalCode"] = "50000",
                ["addressCountry"] = "MY",
                ["localOperationsDescription"] = "Provides verified local operations in Kuala Lumpur."
            }
            : new Dictionary<string, object?>
            {
                ["addressVerified"] = true,
                ["localOperationsVerified"] = localOperationsVerified,
                ["streetAddress"] = "10 Jalan Example",
                ["addressLocality"] = "Kuala Lumpur",
                ["addressRegion"] = "Kuala Lumpur",
                ["postalCode"] = "50000",
                ["addressCountry"] = "MY",
                ["localOperationsDescription"] = "Provides verified local operations in Kuala Lumpur."
            };
        object company = nullableMaps
            ? (object)new Dictionary<string, object?>
            {
                ["name"] = "Acme Malaysia",
                ["localBusinessProfile"] = localBusinessProfile
            }
            : (object)new Dictionary<string, object>
            {
                ["name"] = "Acme Malaysia",
                ["localBusinessProfile"] = localBusinessProfile
            };
        var fields = ContentFieldReader.ToFieldMap(new Dictionary<string, object>
        {
            ["collection"] = "companies",
            ["type"] = "company",
            ["schema_type"] = "LocalBusiness",
            ["company"] = company,
            ["phone"] = "+60 123456789",
            ["address"] = "untrusted standalone address"
        });
        var raw = new RawContentDocument(
            "acme",
            "Acme Malaysia",
            "acme",
            new DateTimeOffset(2026, 7, 25, 9, 30, 0, TimeSpan.Zero),
            new RawBody("<p>Acme</p>"),
            CustomFields: fields);
        var schema = new ContentModelSchema(EntityMappings: new Dictionary<string, EntityMapping>
        {
            ["company"] = new EntityMapping("company", "company")
        });

        return ContentDocumentNormalizer.ToDocument(raw, schema);
    }

    private static IEnumerable<JsonDocument> JsonDocuments(IEnumerable<string> json)
    {
        foreach (var item in json)
        {
            yield return JsonDocument.Parse(item);
        }
    }

    private string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"bukit-company-seo-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        _tempDirectories.Add(directory);
        return directory;
    }
}
