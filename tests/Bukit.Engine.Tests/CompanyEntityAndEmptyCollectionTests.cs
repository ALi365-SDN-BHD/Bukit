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
    public void CanonicalEntity_ExposesInitOnlyVerifiedLocalBusinessProfile()
    {
        var property = typeof(EntityRecord).GetProperty("LocalBusinessProfile");
        Assert.NotNull(property);

        Assert.True(property.SetMethod?.ReturnParameter.GetRequiredCustomModifiers()
            .Any(modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit"));
    }

    [Fact]
    public void Build_EmptyPrimaryCollectionWithNoindexPolicy_UsesNoindexAndOmitsLastModified()
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
        Assert.Equal(typeof(DateTimeOffset?), typeof(SeoIndexEntry).GetProperty(nameof(entry.LastModified))?.PropertyType);
        Assert.Null(typeof(SeoIndexEntry).GetProperty(nameof(entry.LastModified))?.GetValue(entry));
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

    private static AppConfig CreateConfig(bool noindexWhenEmpty = false, SeoOrganizationConfig? organization = null)
    {
        var collection = new CollectionConfig
        {
            Permalink = "/companies/:slug/",
            ListRoute = "/companies/"
        };
        var property = typeof(CollectionConfig).GetProperty("NoindexWhenEmpty");
        Assert.NotNull(property);
        property.SetValue(collection, noindexWhenEmpty);

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
                Kind = ListRouteKind.CollectionPage,
                Url = "/companies/",
                OutputPath = "companies/index.html",
                Template = "pages/list.html",
                Collection = "companies",
                TotalItems = totalItems,
                CanonicalUrl = "/companies/"
            }
        ]);

    private static ContentDocument NormalizedCompanyDocument(bool localOperationsVerified)
    {
        var fields = ContentFieldReader.ToFieldMap(new Dictionary<string, object>
        {
            ["collection"] = "companies",
            ["type"] = "company",
            ["schema_type"] = "LocalBusiness",
            ["company"] = new Dictionary<string, object>
            {
                ["name"] = "Acme Malaysia",
                ["localBusinessProfile"] = new Dictionary<string, object>
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
            },
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
